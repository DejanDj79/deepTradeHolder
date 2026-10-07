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
/// Phase 1 verifies SDK loading and renders the timer shell as a fixed chart panel.
/// TradingApi position/fill integration comes next.
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

    [Category("Layout")]
    [DisplayName("Visible rows")]
    [Description("Target number of visible trade rows before internal scrolling is used.")]
    [VolCustom(CategoryIndex = 1, PropertyIndex = 0, MinValue = 1, MaxValue = 15, IncrementValue = 1)]
    public int VisibleRows { get; set; } = 5;

    [Category("Layout")]
    [DisplayName("Panel position")]
    [Description("Corner of the chart where the timer panel is anchored.")]
    [VolCustom(CategoryIndex = 1, PropertyIndex = 1)]
    public TradeHoldPanelPosition PanelPosition { get; set; } = TradeHoldPanelPosition.TopRight;

    [Category("Layout")]
    [DisplayName("Font size")]
    [VolCustom(CategoryIndex = 1, PropertyIndex = 2, MinValue = 8, MaxValue = 24, IncrementValue = 1)]
    public int FontSize { get; set; } = 11;

    #endregion

    private IAnnotation _panel;
    private IAnnotation _title;
    private IAnnotation _columns;
    private readonly List<IAnnotation> _rows = new List<IAnnotation>();
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

        _title = CreateText(true, FontSize + 1);
        _columns = CreateText(true, FontSize);

        _rows.Clear();
        for (int i = 0; i < VisibleRows; i++)
            _rows.Add(CreateText(false, FontSize));

        _footer = CreateText(false, Math.Max(8, FontSize - 1));

        _lots.Clear();
        _lastNetQuantity = 0;
        _positionStateInitialized = false;

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
        double height = bottom - top;
        double textX = left + width * 0.045;

        _title.X = textX;
        _title.Y = top + 0.018;
        _title.FontSize = FontSize + 1;
        _title.Text = "TRADE HOLD TIMER";

        _columns.X = textX;
        _columns.Y = top + 0.058;
        _columns.FontSize = FontSize;
        _columns.Text = "SIDE     QTY     ELAPSED     STATUS";

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
                row.Text = $"{side,-6} {lot.Quantity,-7} {FormatElapsed(elapsed),-10} {status}";
            }
            else if (_lots.Count == 0 && i == 0)
            {
                row.ForeColor = ColorRef.FromRgb(230, 230, 235);
                row.Text = "FLAT   --      00:00.0     WAITING";
            }
            else
            {
                row.Text = "";
            }
        }

        _footer.X = textX;
        _footer.Y = rowStartY + _rows.Count * rowSpacing + 0.008;
        _footer.FontSize = Math.Max(8, FontSize - 1);
        _footer.Text = $"Min {MinimumHoldSeconds}s · Safe {MinimumHoldSeconds + SafetyBufferSeconds}s";

        _title.ForeColor = ColorRef.FromRgb(245, 245, 248);
        _columns.ForeColor = ColorRef.FromRgb(175, 178, 188);
        _footer.ForeColor = ColorRef.FromRgb(150, 153, 163);
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
                // Scale-in/new entry: its added quantity gets an independent timer.
                AddLot(Math.Sign(net), currentAbs - previousAbs);
            }
            else if (currentAbs < previousAbs)
            {
                // The API does not identify which open lot was closed.
                // Match reductions FIFO: oldest open quantity is closed first.
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
        double panelHeight = 0.14 + VisibleRows * 0.034;

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
