using System.ComponentModel;
using VolSysAPI;
using VolSysAPI.Indicators;
using VolumetricaAPI.Chart;
using VolumetricaControls;
using VolumetricaCore;
using static VolSysAPI.ExternalStructure;
using static VolSysAPI.Structure;

namespace DeepTradeHolder
{
    public enum FixedPanelPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    /// <summary>
    /// Fixed panel test built directly from the working official SessionRangeBox pattern.
    /// The only material change is Relative coordinates instead of bar/price coordinates.
    /// </summary>
    public class FixedPanelBox : Indicator
    {
        public static IndicatorDescriptionBase Register()
        {
            return new IndicatorDescriptionBase
            {
                Name = "Fixed Panel Box Test",
                Description = "Fixed Rectangle + Text using the same annotation-group pattern as SessionRangeBox.",
                Tags = new List<string> { "Example", "Panel", "Rectangle" }
            };
        }

        #region Parameters

        [Category("Layout")]
        [DisplayName("Panel position")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 0)]
        public FixedPanelPosition PanelPosition { get; set; } = FixedPanelPosition.TopRight;

        [Category("Colors")]
        [DisplayName("Border color")]
        [VolCustom(CategoryIndex = 1, PropertyIndex = 0)]
        public ColorRef BorderColor { get; set; } =
            IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Neutral, ColorTypeEnum.Stroke);

        [Category("Colors")]
        [DisplayName("Background opacity")]
        [VolCustom(CategoryIndex = 1, PropertyIndex = 1, MinValue = 0, MaxValue = 255, IncrementValue = 5)]
        public byte BackgroundOpacity { get; set; } = 45;

        #endregion

        private IAnnGroup boxes, labels;
        private IAnnotation box, header, body;

        public override void OnSet(bool setDefault, bool themeOverride)
        {
            OnEndCall = CallHandler.HistRT;
        }

        public override void OnLoad()
        {
            // EXACT SAME GROUP PATTERN AS THE WORKING SESSION RANGE BOX.
            boxes = VAn.CreateAnnGroup();
            labels = VAn.CreateAnnGroup();

            IndVars.Ann_List.AddGroup(boxes);
            IndVars.Ann_List.AddGroup(labels);

            box = VAn.CreateAnnotation(AnnotationType.Rectangle);
            box.CoordinateXType = CoordinateTypeEnum.Pixel;
            box.CoordinateYType = CoordinateTypeEnum.Pixel;
            box.LineWidth = 2;
            box.LineColor = BorderColor;
            box.BackColor = BorderColor.WithOpacity(BackgroundOpacity);
            VAn.AddAnnotation(boxes, box);

            header = VAn.CreateAnnotation(AnnotationType.Text);
            header.CoordinateXType = CoordinateTypeEnum.Pixel;
            header.CoordinateYType = CoordinateTypeEnum.Pixel;
            header.ForeColor = BorderColor;
            header.FontSize = 12;
            header.FontBold = true;
            VAn.AddAnnotation(labels, header);

            body = VAn.CreateAnnotation(AnnotationType.Text);
            body.CoordinateXType = CoordinateTypeEnum.Pixel;
            body.CoordinateYType = CoordinateTypeEnum.Pixel;
            body.ForeColor = ColorRef.FromRgb(230, 230, 235);
            body.FontSize = 11;
            VAn.AddAnnotation(labels, body);

            PlacePanel();
        }

        public override void OnEnd(bool isRt)
        {
            PlacePanel();

            box.LineColor = BorderColor;
            box.BackColor = BorderColor.WithOpacity(BackgroundOpacity);

            header.ForeColor = BorderColor;
            header.Text = "DEEP HOLDER TIMER";

            body.Text = "Fixed panel test\nRectangle is Relative\nPosition should stay fixed";
        }

        private void PlacePanel()
        {
            const double margin = 20;
            const double width = 320;
            const double height = 220;

            // First test only: fixed top-left pixel coordinates.
            // Pixel coordinates are measured from the edge of the chart area.
            double left = margin;
            double top = margin;
            double right = left + width;
            double bottom = top + height;

            box.X = left;
            box.X2 = right;
            box.Y = top;
            box.Y2 = bottom;

            header.X = left + 14;
            header.Y = top + 18;

            body.X = left + 14;
            body.Y = top + 58;
        }
    }
}
