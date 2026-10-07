using System.ComponentModel;
using VolSysAPI;
using VolSysAPI.Indicators;
using VolumetricaAPI.Chart;
using VolumetricaControls;
using VolumetricaCore;
using static VolSysAPI.ExternalStructure;
using static VolSysAPI.Structure;

namespace DeepTradeHolder;

/// <summary>
/// Trade Hold Timer for DeepCharts.
///
/// Phase 1 intentionally verifies SDK loading, registration, settings persistence,
/// and fixed chart-overlay rendering before wiring the desktop-only TradingApi.
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
    [DisplayName("X offset (px)")]
    [Description("Panel distance in pixels from the left edge of the chart.")]
    [VolCustom(CategoryIndex = 1, PropertyIndex = 1, MinValue = 0, MaxValue = 3000, IncrementValue = 1)]
    public int XOffsetPx { get; set; } = 12;

    [Category("Layout")]
    [DisplayName("Y offset (px)")]
    [Description("Panel distance in pixels from the top edge of the chart.")]
    [VolCustom(CategoryIndex = 1, PropertyIndex = 2, MinValue = 0, MaxValue = 2000, IncrementValue = 1)]
    public int YOffsetPx { get; set; } = 12;

    [Category("Layout")]
    [DisplayName("Font size")]
    [VolCustom(CategoryIndex = 1, PropertyIndex = 3, MinValue = 8, MaxValue = 32, IncrementValue = 1)]
    public int FontSize { get; set; } = 12;

    #endregion

    private IAnnotation _statusLabel;

    public override void OnSet(bool setDefault, bool themeOverride)
    {
        // DeepCharts does not calculate/draw an indicator unless at least one
        // chart callback is requested. OnEnd is enough for this fixed overlay.
        OnEndCall = CallHandler.HistRT;

        // This is the small description shown next to the indicator name.
        Description = $"Min {MinimumHoldSeconds}s · Safe {MinimumHoldSeconds + SafetyBufferSeconds}s";
    }

    public override void OnLoad()
    {
        _statusLabel = VAn.CreateAnnotation(AnnotationType.Text);
        _statusLabel.CoordinateXType = CoordinateTypeEnum.Pixel;
        _statusLabel.CoordinateYType = CoordinateTypeEnum.Pixel;
        _statusLabel.TextAlign = TextAlignment.VLeftHTop;
        _statusLabel.LabelAlign = LabelAlignEnum.Left;

        VAn.AddAnnotation(IndVars.FrontAnnList, _statusLabel);

        // Phase-1 diagnostics are complete; do not show a warning/status line on the chart.
        StatusMessage = null;
    }

    public override void OnEnd(bool isRt)
    {
        if (_statusLabel == null)
            return;

        // Apply these on every calculation so layout settings immediately
        // affect the existing annotation.
        _statusLabel.X = XOffsetPx;
        _statusLabel.Y = YOffsetPx;
        _statusLabel.FontSize = FontSize;
        _statusLabel.FontBold = true;
        _statusLabel.ForeColor = ColorRef.FromRgb(230, 230, 230);
        _statusLabel.Text =
            $"TRADE HOLD TIMER\n" +
            $"SDK LINK OK · PHASE 1\n" +
            $"Min {MinimumHoldSeconds}s · Safe {MinimumHoldSeconds + SafetyBufferSeconds}s · Rows {VisibleRows}";
    }

}
