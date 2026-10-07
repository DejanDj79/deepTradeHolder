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
            box.CoordinateXType = CoordinateTypeEnum.Relative;
            box.CoordinateYType = CoordinateTypeEnum.Relative;
            box.LineWidth = 2;
            box.LineColor = BorderColor;
            box.BackColor = BorderColor.WithOpacity(BackgroundOpacity);
            VAn.AddAnnotation(boxes, box);

            header = VAn.CreateAnnotation(AnnotationType.Text);
            header.CoordinateXType = CoordinateTypeEnum.Relative;
            header.CoordinateYType = CoordinateTypeEnum.Relative;
            header.ForeColor = BorderColor;
            header.FontSize = 12;
            header.FontBold = true;
            VAn.AddAnnotation(labels, header);

            body = VAn.CreateAnnotation(AnnotationType.Text);
            body.CoordinateXType = CoordinateTypeEnum.Relative;
            body.CoordinateYType = CoordinateTypeEnum.Relative;
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
            const double marginX = 0.02;
            const double marginY = 0.04;
            const double width = 0.30;
            const double height = 0.24;

            double left;
            double right;
            double top;
            double bottom;

            switch (PanelPosition)
            {
                case FixedPanelPosition.TopLeft:
                    left = marginX;
                    right = left + width;
                    top = 1.0 - marginY;
                    bottom = top - height;
                    break;

                case FixedPanelPosition.TopRight:
                    right = 1.0 - marginX;
                    left = right - width;
                    top = 1.0 - marginY;
                    bottom = top - height;
                    break;

                case FixedPanelPosition.BottomLeft:
                    left = marginX;
                    right = left + width;
                    bottom = marginY;
                    top = bottom + height;
                    break;

                case FixedPanelPosition.BottomRight:
                default:
                    right = 1.0 - marginX;
                    left = right - width;
                    bottom = marginY;
                    top = bottom + height;
                    break;
            }

            // SAME ORDER AS SESSION RANGE BOX:
            // X -> X2 from left to right, Y -> Y2 from top/high to bottom/low.
            box.X = left;
            box.X2 = right;
            box.Y = top;
            box.Y2 = bottom;

            header.X = left + 0.015;
            header.Y = top - 0.025;

            body.X = left + 0.015;
            body.Y = top - 0.075;
        }
    }
}
