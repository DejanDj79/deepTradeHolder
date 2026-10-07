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
    private IAnnotation _sampleRow;
    private IAnnotation _footer;

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
        _sampleRow = CreateText(false, FontSize);
        _footer = CreateText(false, Math.Max(8, FontSize - 1));

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
        _title.Y = top + height * 0.10;
        _title.FontSize = FontSize + 1;
        _title.Text = "TRADE HOLD TIMER";

        _columns.X = textX;
        _columns.Y = top + height * 0.34;
        _columns.FontSize = FontSize;
        _columns.Text = "SIDE     QTY     ELAPSED     STATUS";

        _sampleRow.X = textX;
        _sampleRow.Y = top + height * 0.55;
        _sampleRow.FontSize = FontSize;
        _sampleRow.Text = "--       --      00:00.0     WAITING";

        _footer.X = textX;
        _footer.Y = top + height * 0.78;
        _footer.FontSize = Math.Max(8, FontSize - 1);
        _footer.Text = $"Min {MinimumHoldSeconds}s · Safe {MinimumHoldSeconds + SafetyBufferSeconds}s";

        _title.ForeColor = ColorRef.FromRgb(245, 245, 248);
        _columns.ForeColor = ColorRef.FromRgb(175, 178, 188);
        _sampleRow.ForeColor = ColorRef.FromRgb(230, 230, 235);
        _footer.ForeColor = ColorRef.FromRgb(150, 153, 163);
    }

    private void GetPanelBounds(out double left, out double top, out double right, out double bottom)
    {
        const double marginX = 0.004;
        const double marginY = 0.025;
        const double panelWidth = 0.30;
        const double panelHeight = 0.18;

        switch (PanelPosition)
        {
            case TradeHoldPanelPosition.TopLeft:
                left = marginX;
                top = marginY;
                break;

            case TradeHoldPanelPosition.BottomLeft:
                left = marginX;
                top = 1.0 - marginY - panelHeight;
                break;

            case TradeHoldPanelPosition.BottomRight:
                left = 1.0 - marginX - panelWidth;
                top = 1.0 - marginY - panelHeight;
                break;

            case TradeHoldPanelPosition.TopRight:
            default:
                left = 1.0 - marginX - panelWidth;
                top = marginY;
                break;
        }

        right = left + panelWidth;
        bottom = top + panelHeight;
    }
}
