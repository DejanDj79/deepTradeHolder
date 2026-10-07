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

    [Category("Layout")]
    [DisplayName("Visible rows")]
    [Description("Maximum number of active entry timers shown. When there are more, the newest rows are displayed.")]
    [VolCustom(CategoryIndex = 2, PropertyIndex = 0, MinValue = 1, MaxValue = 15, IncrementValue = 1)]
    public int VisibleRows { get; set; } = 5;

    [Category("Layout")]
    [DisplayName("Panel position")]
    [Description("Corner of the chart where the timer panel is anchored.")]
    [VolCustom(CategoryIndex = 2, PropertyIndex = 1)]
    public TradeHoldPanelPosition PanelPosition { get; set; } = TradeHoldPanelPosition.TopRight;

    [Category("Layout")]
    [DisplayName("Font size")]
    [VolCustom(CategoryIndex = 2, PropertyIndex = 2, MinValue = 8, MaxValue = 24, IncrementValue = 1)]
    public int FontSize { get; set; } = 11;

    #endregion

    private IAnnotation _panel;
    private IAnnotation _panelBackdrop;
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
        _panel = VAn.CreateAnnotation(AnnotationType.Rectangle);
        _panel.CoordinateXType = CoordinateTypeEnum.Relative;
        _panel.CoordinateYType = CoordinateTypeEnum.Relative;
        _panel.LineWidth = 1;
        _panel.LineColor = ColorRef.FromRgb(110, 110, 120);
        _panel.BackColor = ColorRef.FromRgb(25, 27, 31).WithOpacity(88);
        VAn.AddAnnotation(IndVars.FrontAnnList, _panel);

        // Relative Rectangle/Line annotations are inconsistent in the current
        // desktop build, while Text background/border rendering is reliable.
        // Use a blank multiline Text annotation as the visible outer panel.
        _panelBackdrop = CreateText(false, FontSize);
        _panelBackdrop.BackColor = ColorRef.FromRgb(18, 20, 24).WithOpacity(235);
        _panelBackdrop.LineColor = ColorRef.FromRgb(175, 180, 192);
        _panelBackdrop.LineWidth = 2;

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

        GetPanelBounds(out double left, out double top, out double right, out double bottom);

        _panel.X = left;
        _panel.X2 = right;
        _panel.Y = top;
        _panel.Y2 = bottom;

        double width = right - left;
        double textX = left + width * 0.045;

        ColorRef border = ColorRef.FromRgb(115, 121, 134);
        ColorRef panelBg = ColorRef.FromRgb(18, 20, 24).WithOpacity(235);
        ColorRef headerBg = ColorRef.FromRgb(38, 42, 50).WithOpacity(235);
        ColorRef subHeaderBg = ColorRef.FromRgb(29, 32, 39).WithOpacity(230);
        ColorRef neutralBg = ColorRef.FromRgb(24, 27, 33).WithOpacity(225);
        ColorRef holdBg = ColorRef.FromRgb(62, 28, 32).WithOpacity(225);
        ColorRef minimumBg = ColorRef.FromRgb(65, 53, 22).WithOpacity(225);
        ColorRef safeBg = ColorRef.FromRgb(24, 57, 39).WithOpacity(225);

        _panelBackdrop.X = textX - 0.006;
        _panelBackdrop.Y = top + 0.006;
        _panelBackdrop.FontSize = FontSize;
        _panelBackdrop.BackColor = panelBg;
        _panelBackdrop.LineColor = ColorRef.FromRgb(185, 190, 202);
        _panelBackdrop.LineWidth = 2;
        _panelBackdrop.ForeColor = panelBg;
        _panelBackdrop.Text = BuildBackdropText(VisibleRows);

        _title.X = textX;
        _title.Y = top + 0.018;
        _title.FontSize = FontSize;
        _title.Text = PadCell("TRADE HOLD TIMER", 42);
        StyleCell(_title, headerBg, ColorRef.FromRgb(205, 210, 220), 1);

        _columns.X = textX;
        _columns.Y = top + 0.058;
        _columns.FontSize = FontSize;
        _columns.Text = PadCell("SIDE      QTY      ELAPSED      STATUS", 42);
        StyleCell(_columns, subHeaderBg, border, 1);

        decimal net = TradingApi.PositionNetQuantity;

        if (isRt)
            UpdateTradeLots(net);

        double rowStartY = top + 0.095;
        double rowSpacing = 0.034;

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
                row.Text = PadCell($"{side,-6} {lot.Quantity,-7} {FormatElapsed(elapsed),-10} {status}", 42);

                ColorRef rowBg = status == "SAFE"
                    ? safeBg
                    : status == "MIN REACHED"
                        ? minimumBg
                        : holdBg;
                StyleCell(row, rowBg, border, 1);
            }
            else if (_lots.Count == 0 && i == 0)
            {
                row.ForeColor = ColorRef.FromRgb(205, 208, 216);
                row.Text = PadCell("FLAT      --       00:00.0      WAITING", 42);
                StyleCell(row, neutralBg, border, 1);
            }
            else
            {
                row.Text = PadCell("", 42);
                row.ForeColor = ColorRef.FromRgb(120, 124, 134);
                StyleCell(row, neutralBg, border, 1);
            }
        }

        _overallStatus.X = textX;
        _overallStatus.Y = rowStartY + _rows.Count * rowSpacing + 0.006;
        _overallStatus.FontSize = FontSize;

        bool allSafeNow = false;

        if (_lots.Count == 0)
        {
            _overallStatus.Text = PadCell("POSITION: WAITING", 42);
            _overallStatus.ForeColor = ColorRef.FromRgb(190, 194, 204);
            StyleCell(_overallStatus, neutralBg, border, 1);
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
                _overallStatus.Text = PadCell("POSITION: NOT ALL SAFE", 42);
                _overallStatus.ForeColor = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Down, ColorTypeEnum.Text);
                StyleCell(_overallStatus, holdBg, border, 1);
            }
            else if (youngestSeconds < MinimumHoldSeconds + SafetyBufferSeconds)
            {
                _overallStatus.Text = PadCell("POSITION: MIN REACHED", 42);
                _overallStatus.ForeColor = ColorRef.Yellow;
                StyleCell(_overallStatus, minimumBg, border, 1);
            }
            else
            {
                allSafeNow = true;
                _overallStatus.Text = PadCell("POSITION: ALL SAFE", 42);
                _overallStatus.ForeColor = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Up, ColorTypeEnum.Text);
                StyleCell(_overallStatus, safeBg, ColorRef.FromRgb(120, 180, 145), 2);
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
            _scrollInfo.Text = PadCell($"ROWS {from}-{to} / {_lots.Count}   ▓▓▓▓▓", 42);
        }
        else
        {
            _scrollInfo.Text = PadCell($"ROWS {Math.Min(_lots.Count, _rows.Count)} / {_rows.Count}", 42);
        }
        _scrollInfo.ForeColor = ColorRef.FromRgb(145, 150, 162);
        StyleCell(_scrollInfo, subHeaderBg, border, 1);

        _footer.X = textX;
        _footer.Y = _scrollInfo.Y + 0.030;
        _footer.FontSize = Math.Max(8, FontSize - 1);
        _footer.Text = PadCell($"Min {MinimumHoldSeconds}s · Safe {MinimumHoldSeconds + SafetyBufferSeconds}s", 42);
        _footer.ForeColor = ColorRef.FromRgb(155, 160, 172);
        StyleCell(_footer, neutralBg, border, 1);

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

    private static void StyleCell(IAnnotation annotation, ColorRef backColor, ColorRef lineColor, int lineWidth)
    {
        annotation.BackColor = backColor;
        annotation.LineColor = lineColor;
        annotation.LineWidth = lineWidth;
    }

    private static string PadCell(string text, int width)
    {
        if (text == null)
            text = "";

        if (text.Length >= width)
            return " " + text + " ";

        // Non-breaking spaces are used so the chart renderer keeps the visual width.
        return " " + text + new string('\u00A0', width - text.Length) + " ";
    }

    private static string BuildBackdropText(int visibleRows)
    {
        int lineCount = Math.Max(6, visibleRows + 5);
        string line = new string('\u00A0', 46);
        return string.Join("\n", Enumerable.Repeat(line, lineCount));
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        int minutes = (int)elapsed.TotalMinutes;
        return $"{minutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds / 100}";
    }

    private void GetPanelBounds(out double left, out double top, out double right, out double bottom)
    {
        const double leftMarginX = -0.007;
        const double rightShiftX = 0.187;
        const double marginY = 0.025;
        const double panelWidth = 0.30;
        double panelHeight = 0.225 + VisibleRows * 0.034;

        switch (PanelPosition)
        {
            case TradeHoldPanelPosition.TopLeft:
                left = leftMarginX;
                top = marginY;
                break;

            case TradeHoldPanelPosition.BottomLeft:
                left = leftMarginX;
                top = 1.0 - marginY - panelHeight;
                break;

            case TradeHoldPanelPosition.BottomRight:
                left = 1.0 - panelWidth + rightShiftX;
                top = 1.0 - marginY - panelHeight;
                break;

            case TradeHoldPanelPosition.TopRight:
            default:
                left = 1.0 - panelWidth + rightShiftX;
                top = marginY;
                break;
        }

        right = left + panelWidth;
        bottom = top + panelHeight;
    }
}
