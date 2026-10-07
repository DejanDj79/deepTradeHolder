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
/// The fixed panel intentionally uses one Text annotation because that is the
/// rendering primitive verified to work reliably with relative coordinates
/// in the current DeepCharts desktop build.
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
    [Description("Maximum number of trade rows shown in the panel.")]
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

    private IAnnotation _panelText;

    public override void OnSet(bool setDefault, bool themeOverride)
    {
        OnEndCall = CallHandler.HistRT;
        Description = $"Min {MinimumHoldSeconds}s · Safe {MinimumHoldSeconds + SafetyBufferSeconds}s";
    }

    public override void OnLoad()
    {
        _panelText = VAn.CreateAnnotation(AnnotationType.Text);
        _panelText.CoordinateXType = CoordinateTypeEnum.Relative;
        _panelText.CoordinateYType = CoordinateTypeEnum.Relative;
        _panelText.TextAlign = TextAlignment.VLeftHTop;
        _panelText.FontBold = false;
        _panelText.FontSize = FontSize;
        _panelText.ForeColor = ColorRef.FromRgb(232, 233, 238);
        _panelText.BackColor = ColorRef.FromRgb(24, 26, 31).WithOpacity(225);
        _panelText.LineColor = ColorRef.FromRgb(185, 188, 198);
        _panelText.LineWidth = 1;

        VAn.AddAnnotation(IndVars.FrontAnnList, _panelText);
        StatusMessage = null;
    }

    public override void OnEnd(bool isRt)
    {
        if (_panelText == null)
            return;

        GetPanelAnchor(out double x, out double y);

        _panelText.X = x;
        _panelText.Y = y;
        _panelText.FontSize = FontSize;
        _panelText.Text =
            " TRADE HOLD TIMER\n" +
            " ─────────────────────────────\n" +
            " SIDE    QTY    ELAPSED    STATUS\n" +
            " ─────────────────────────────\n" +
            " --      --     00:00.0    WAITING\n" +
            " ─────────────────────────────\n" +
            $" Min {MinimumHoldSeconds}s · Safe {MinimumHoldSeconds + SafetyBufferSeconds}s ";
    }

    private void GetPanelAnchor(out double x, out double y)
    {
        bool rightSide = PanelPosition is TradeHoldPanelPosition.TopRight or TradeHoldPanelPosition.BottomRight;
        bool bottomSide = PanelPosition is TradeHoldPanelPosition.BottomLeft or TradeHoldPanelPosition.BottomRight;

        // These anchors are based on the positions already verified visually
        // on the user's DeepCharts desktop build.
        x = rightSide ? 0.90 : -0.007;
        y = bottomSide ? 0.80 : 0.01;
    }
}
