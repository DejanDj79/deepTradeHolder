using System.ComponentModel;
using VolSysAPI;
using VolSysAPI.Indicators;
using VolumetricaAPI.Chart;
using VolumetricaControls;
using VolumetricaCore;
using static VolSysAPI.ExternalStructure;
using static VolSysAPI.Structure;

namespace DeepTradeHolder;

public enum TradeHoldPanelPosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

/// <summary>
/// Trade Hold Timer for DeepCharts.
///
/// Tracks observed futures position entries through DeepCharts TradingApi.
/// Each increase in absolute net quantity creates an independent entry timer.
/// Because ITradingAPI does not expose individual open-lot identity, partial
/// reductions are matched FIFO for display purposes.
/// </summary>
public class TradeHoldTimer : Indicator
{
    public static IndicatorDescriptionBase Register()
    {
        return new IndicatorDescriptionBase
        {
            Name = "Trade Hold Timer",
            Description = "Tracks how long active trades have been open.",
            Tags = new List<string> { "Trading", "Timer", "Prop Firm", "Scalping" }
        };
    }

    #region Parameters

    [Category("Timing")]
    [DisplayName("Minimum hold (seconds)")]
    [Description("Minimum trade duration before the MIN REACHED state.")]
    [VolCustom(CategoryIndex = 0, PropertyIndex = 0, MinValue = 1, MaxValue = 3600, IncrementValue = 1)]
    public int MinimumHoldSeconds { get; set; } = 15;

    [Category("Timing")]
    [DisplayName("Safety buffer (seconds)")]
    [Description("Extra time after the minimum hold before the SAFE state.")]
    [VolCustom(CategoryIndex = 0, PropertyIndex = 1, MinValue = 0, MaxValue = 60, IncrementValue = 1)]
    public int SafetyBufferSeconds { get; set; } = 2;

    [Category("Alerts")]
    [DisplayName("Enable ALL SAFE alert")]
    [Description("Play the selected sound once when the whole position first becomes ALL SAFE.")]
    [VolCustom(CategoryIndex = 1, PropertyIndex = 0)]
    public bool EnableAllSafeAlert { get; set; } = true;

    [Category("Alerts")]
    [DisplayName("Alert sound")]
    [VolCustom(CategoryIndex = 1, PropertyIndex = 1, IsAlert = true)]
    public DynamicList AlertName { get; set; } = new DynamicList();

    [Category("Colors")]
    [DisplayName("Border color")]
    [VolCustom(CategoryIndex = 2, PropertyIndex = 0)]
    public ColorRef BorderColor { get; set; } = ColorRef.FromRgb(255, 193, 7);

    [Category("Layout")]
    [DisplayName("Visible rows")]
    [Description("Maximum number of active entry timers shown. When there are more, the newest rows are displayed.")]
    [VolCustom(CategoryIndex = 3, PropertyIndex = 0, MinValue = 1, MaxValue = 15, IncrementValue = 1)]
    public int VisibleRows { get; set; } = 5;

    [Category("Layout")]
    [DisplayName("Panel position")]
    [Description("Corner of the chart where the timer panel is anchored.")]
    [VolCustom(CategoryIndex = 3, PropertyIndex = 1)]
    public TradeHoldPanelPosition PanelPosition { get; set; } = TradeHoldPanelPosition.TopRight;

    [Category("Layout")]
    [DisplayName("Font size")]
    [VolCustom(CategoryIndex = 3, PropertyIndex = 2, MinValue = 8, MaxValue = 24, IncrementValue = 1)]
    public int FontSize { get; set; } = 11;

    #endregion

    private IAnnotation _panel;
    private IAnnotation _title;
    private IAnnotation _columns;
    private readonly List<IAnnotation> _rows = new List<IAnnotation>();
    private IAnnotation _overallStatus;
    private IAnnotation _scrollInfo;
    private IAnnotation _footer;

    private sealed class TradeLot
    {
        public int Direction;
        public decimal Quantity;
        public DateTime OpenedAtUtc;
    }

    private readonly List<TradeLot> _lots = new List<TradeLot>();
    private decimal _lastNetQuantity;
    private bool _positionStateInitialized;
    private bool _allSafeAlerted;

    public override void OnSet(bool setDefault, bool themeOverride)
    {
        OnEndCall = CallHandler.HistRT;
        Description = $"Min {MinimumHoldSeconds}s · Safe {MinimumHoldSeconds + SafetyBufferSeconds}s";
    }

    public override void OnLoad()
    {
        // DeepCharts reliably renders background + border on Text annotations
        // in this desktop build. Use one multiline Text annotation as the single
        // visual panel container, then draw all real labels over it.
        _panel = VAn.CreateAnnotation(AnnotationType.Text);
        _panel.CoordinateXType = CoordinateTypeEnum.Relative;
        _panel.CoordinateYType = CoordinateTypeEnum.Relative;
        _panel.TextAlign = TextAlignment.VLeftHTop;
        _panel.FontSize = FontSize;
        _panel.LineWidth = 2;
        _panel.LineColor = BorderColor;
        _panel.BackColor = ColorRef.FromRgb(18, 20, 24).WithOpacity(235);
        _panel.ForeColor = ColorRef.FromRgb(18, 20, 24).WithOpacity(235);
        _panel.Text = BuildPanelBackground(VisibleRows);
        VAn.AddAnnotation(IndVars.FrontAnnList, _panel);

        _title = CreateText(true, FontSize);
        _columns = CreateText(true, FontSize);

        _rows.Clear();
        for (int i = 0; i < VisibleRows; i++)
            _rows.Add(CreateText(false, FontSize));

        _overallStatus = CreateText(true, FontSize);
        _scrollInfo = CreateText(false, Math.Max(8, FontSize - 2));
        _footer = CreateText(false, Math.Max(8, FontSize - 1));

        _lots.Clear();
        _lastNetQuantity = 0;
        _positionStateInitialized = false;
        _allSafeAlerted = false;

        StatusMessage = null;
    }

    private IAnnotation CreateText(bool bold, int fontSize)
    {
        var label = VAn.CreateAnnotation(AnnotationType.Text);
        label.CoordinateXType = CoordinateTypeEnum.Relative;
        label.CoordinateYType = CoordinateTypeEnum.Relative;
        label.TextAlign = TextAlignment.VLeftHTop;
        label.ForeColor = ColorRef.FromRgb(230, 230, 235);
        label.FontBold = bold;
        label.FontSize = fontSize;
        VAn.AddAnnotation(IndVars.FrontAnnList, label);
        return label;
    }

    public override void OnEnd(bool isRt)
    {
        if (_panel == null)
            return;

        GetPanelAnchor(out double left, out double top);

        _panel.X = left;
        _panel.Y = top;
        _panel.FontSize = FontSize;
        _panel.LineColor = BorderColor;
        _panel.LineWidth = 2;
        _panel.BackColor = ColorRef.FromRgb(18, 20, 24).WithOpacity(235);
        _panel.ForeColor = ColorRef.FromRgb(18, 20, 24).WithOpacity(235);
        _panel.Text = BuildPanelBackground(VisibleRows);

        double textX = left + 0.012;
        const double rowSpacing = 0.034;

        _title.X = textX;
        _title.Y = top + 0.014;
        _title.FontSize = FontSize;
        _title.Text = "TRADE HOLD TIMER";

        _columns.X = textX;
        _columns.Y = top + 0.052;
        _columns.FontSize = FontSize;
        _columns.Text = "SIDE      QTY      ELAPSED      STATUS";

        decimal net = TradingApi.PositionNetQuantity;

        if (isRt)
            UpdateTradeLots(net);

        double rowStartY = top + 0.086;

        int firstLot = Math.Max(0, _lots.Count - _rows.Count);
        for (int i = 0; i < _rows.Count; i++)
        {
            IAnnotation row = _rows[i];
            row.X = textX;
            row.Y = rowStartY + i * rowSpacing;
            row.FontSize = FontSize;

            int lotIndex = firstLot + i;
            if (lotIndex < _lots.Count)
            {
                TradeLot lot = _lots[lotIndex];
                TimeSpan elapsed = DateTime.UtcNow - lot.OpenedAtUtc;
                if (elapsed < TimeSpan.Zero)
                    elapsed = TimeSpan.Zero;

                string side = lot.Direction > 0 ? "LONG" : "SHORT";
                string status = GetStatus(elapsed, out ColorRef color);
                row.ForeColor = color;
                row.Text = $"{side,-6} {lot.Quantity,-7} {FormatElapsed(elapsed),-10} {status}";
            }
            else if (_lots.Count == 0 && i == 0)
            {
                row.ForeColor = ColorRef.FromRgb(205, 208, 216);
                row.Text = "FLAT      --       00:00.0      WAITING";
            }
            else
            {
                row.Text = "";
                row.ForeColor = ColorRef.FromRgb(120, 124, 134);
            }
        }

        _overallStatus.X = textX;
        _overallStatus.Y = rowStartY + _rows.Count * rowSpacing + 0.006;
        _overallStatus.FontSize = FontSize;

        bool allSafeNow = false;

        if (_lots.Count == 0)
        {
            _overallStatus.Text = "POSITION: WAITING";
            _overallStatus.ForeColor = ColorRef.FromRgb(190, 194, 204);
        }
        else
        {
            DateTime newestOpen = _lots.Max(lot => lot.OpenedAtUtc);
            TimeSpan youngestElapsed = DateTime.UtcNow - newestOpen;
            if (youngestElapsed < TimeSpan.Zero)
                youngestElapsed = TimeSpan.Zero;

            double youngestSeconds = youngestElapsed.TotalSeconds;

            if (youngestSeconds < MinimumHoldSeconds)
            {
                _overallStatus.Text = "POSITION: NOT ALL SAFE";
                _overallStatus.ForeColor = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Down, ColorTypeEnum.Text);
            }
            else if (youngestSeconds < MinimumHoldSeconds + SafetyBufferSeconds)
            {
                _overallStatus.Text = "POSITION: MIN REACHED";
                _overallStatus.ForeColor = ColorRef.Yellow;
            }
            else
            {
                allSafeNow = true;
                _overallStatus.Text = "POSITION: ALL SAFE";
                _overallStatus.ForeColor = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Up, ColorTypeEnum.Text);
            }
        }

        if (allSafeNow)
        {
            if (isRt && EnableAllSafeAlert && !_allSafeAlerted)
            {
                if (!string.IsNullOrWhiteSpace(AlertName?.SelValue))
                    VAn.PlayAlert(AlertName.SelValue);

                _allSafeAlerted = true;
            }
        }
        else
        {
            _allSafeAlerted = false;
        }

        _scrollInfo.X = textX;
        _scrollInfo.Y = _overallStatus.Y + 0.031;
        _scrollInfo.FontSize = Math.Max(8, FontSize - 2);

        if (_lots.Count > _rows.Count)
        {
            int from = firstLot + 1;
            int to = Math.Min(firstLot + _rows.Count, _lots.Count);
            _scrollInfo.Text = $"ROWS {from}-{to} / {_lots.Count}   ▓▓▓▓▓";
        }
        else
        {
            _scrollInfo.Text = $"ROWS {Math.Min(_lots.Count, _rows.Count)} / {_rows.Count}";
        }
        _scrollInfo.ForeColor = ColorRef.FromRgb(145, 150, 162);

        _footer.X = textX;
        _footer.Y = _scrollInfo.Y + 0.030;
        _footer.FontSize = Math.Max(8, FontSize - 1);
        _footer.Text = $"Min {MinimumHoldSeconds}s · Safe {MinimumHoldSeconds + SafetyBufferSeconds}s";
        _footer.ForeColor = ColorRef.FromRgb(155, 160, 172);

        _title.ForeColor = ColorRef.FromRgb(245, 245, 248);
        _columns.ForeColor = ColorRef.FromRgb(180, 185, 196);
    }

    private void UpdateTradeLots(decimal net)
    {
        if (!_positionStateInitialized)
        {
            _positionStateInitialized = true;
            _lastNetQuantity = net;

            // If attached while already in a position, create one observed lot.
            if (net != 0)
                AddLot(Math.Sign(net), Math.Abs(net));

            return;
        }

        if (net == _lastNetQuantity)
            return;

        decimal previous = _lastNetQuantity;
        bool wasFlat = previous == 0;
        bool isFlat = net == 0;

        if (wasFlat && !isFlat)
        {
            _lots.Clear();
            AddLot(Math.Sign(net), Math.Abs(net));
        }
        else if (!wasFlat && isFlat)
        {
            _lots.Clear();
        }
        else if (Math.Sign(previous) != Math.Sign(net))
        {
            // Net reversal: the old side is fully closed and the opposite side starts now.
            _lots.Clear();
            AddLot(Math.Sign(net), Math.Abs(net));
        }
        else
        {
            decimal previousAbs = Math.Abs(previous);
            decimal currentAbs = Math.Abs(net);

            if (currentAbs > previousAbs)
            {
                // A position increase is treated as a new entry and gets its own timer.
                AddLot(Math.Sign(net), currentAbs - previousAbs);
            }
            else if (currentAbs < previousAbs)
            {
                // ITradingAPI exposes only aggregate position quantity, not which
                // specific open entry was reduced. FIFO is used only to keep the
                // displayed active-entry list conservative and deterministic.
                CloseQuantityFifo(previousAbs - currentAbs);
            }
        }

        _lastNetQuantity = net;
    }

    private void AddLot(int direction, decimal quantity)
    {
        if (quantity <= 0)
            return;

        _lots.Add(new TradeLot
        {
            Direction = direction,
            Quantity = quantity,
            OpenedAtUtc = DateTime.UtcNow
        });
    }

    private void CloseQuantityFifo(decimal quantity)
    {
        decimal remaining = quantity;

        while (remaining > 0 && _lots.Count > 0)
        {
            TradeLot lot = _lots[0];

            if (lot.Quantity <= remaining)
            {
                remaining -= lot.Quantity;
                _lots.RemoveAt(0);
            }
            else
            {
                lot.Quantity -= remaining;
                remaining = 0;
            }
        }
    }

    private string GetStatus(TimeSpan elapsed, out ColorRef color)
    {
        double seconds = elapsed.TotalSeconds;

        if (seconds < MinimumHoldSeconds)
        {
            color = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Down, ColorTypeEnum.Text);
            return "HOLD";
        }

        if (seconds < MinimumHoldSeconds + SafetyBufferSeconds)
        {
            color = ColorRef.Yellow;
            return "MIN REACHED";
        }

        color = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Up, ColorTypeEnum.Text);
        return "SAFE";
    }

    private static string BuildPanelBackground(int visibleRows)
    {
        // A non-empty text block is required for Text BackColor/LineColor to
        // produce a visible rectangular surface. Non-breaking spaces preserve width.
        int lineCount = visibleRows + 5;
        string line = new string('\u00A0', 44);
        return string.Join("\n", Enumerable.Repeat(line, lineCount));
    }

    private void GetPanelAnchor(out double left, out double top)
    {
        const double marginX = 0.015;
        const double marginY = 0.025;
        const double panelWidth = 0.30;
        double panelHeight = 0.19 + VisibleRows * 0.034;

        switch (PanelPosition)
        {
            case TradeHoldPanelPosition.TopLeft:
                left = marginX;
                top = marginY;
                break;

            case TradeHoldPanelPosition.TopRight:
                left = 1.0 - marginX - panelWidth;
                top = marginY;
                break;

            case TradeHoldPanelPosition.BottomLeft:
                left = marginX;
                top = 1.0 - marginY - panelHeight;
                break;

            case TradeHoldPanelPosition.BottomRight:
            default:
                left = 1.0 - marginX - panelWidth;
                top = 1.0 - marginY - panelHeight;
                break;
        }
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        int minutes = (int)elapsed.TotalMinutes;
        return $"{minutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds / 100}";
    }
}
