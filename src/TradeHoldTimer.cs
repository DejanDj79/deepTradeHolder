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
    /// <summary>
    /// Temporary exact-style test based on DeepCharts' official SessionRangeBox example.
    /// </summary>
    public class SessionRangeBox : Indicator
    {
        public static IndicatorDescriptionBase Register()
        {
            return new IndicatorDescriptionBase
            {
                Name = "Example - Session Range Box",
                Description = "Box around the high and the low of every session. Example of annotations and groups.",
                Tags = new List<string> { "Example", "Session" }
            };
        }

        #region Parameters

        [Category("General")]
        [DisplayName("Sessions shown")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 0, MinValue = 1, MaxValue = 100, IncrementValue = 1)]
        public int MaxSessions { get; set; } = 10;

        [Category("General")]
        [DisplayName("Show size")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 1)]
        public bool ShowSize { get; set; } = true;

        [Category("Colors")]
        [VolCustom(CategoryIndex = 1, PropertyIndex = 0)]
        public ColorRef BoxColor { get; set; } =
            IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Neutral, ColorTypeEnum.Stroke);

        #endregion

        class SessionBox
        {
            public IAnnotation Box, Label;
            public int FirstIndex;
            public double High, Low;
        }

        readonly List<SessionBox> sessions = new List<SessionBox>();

        IAnnGroup boxes, labels;

        public override void OnSet(bool setDefault, bool themeOverride)
        {
            OnOpenCall = CallHandler.HistRT;
            OnEndCall = CallHandler.HistRT;
        }

        public override void OnLoad()
        {
            sessions.Clear();

            boxes = VAn.CreateAnnGroup();
            labels = VAn.CreateAnnGroup();

            IndVars.Ann_List.AddGroup(boxes);
            IndVars.Ann_List.AddGroup(labels);
        }

        public override void OnOpen(bool isRt)
        {
            int index = VAn.BarIndex;
            var bar = VAn.BarVars[index];

            if (index > 0 && sessions.Count > 0)
                Extend(index - 1);

            if (bar.IsNewDay || sessions.Count == 0)
                StartSession(index);
        }

        public override void OnEnd(bool isRt)
        {
            if (sessions.Count > 0)
                Extend(VAn.LastIndex());
        }

        private void StartSession(int index)
        {
            var bar = VAn.BarVars[index];

            var session = new SessionBox
            {
                FirstIndex = index,
                High = bar.High,
                Low = bar.Low
            };

            session.Box = VAn.CreateAnnotation(AnnotationType.Rectangle);
            session.Box.LineColor = BoxColor;
            session.Box.BackColor = BoxColor.WithOpacity(25);
            session.Box.LineWidth = 1;

            VAn.AddAnnotation(boxes, session.Box);

            if (ShowSize)
            {
                session.Label = VAn.CreateAnnotation(AnnotationType.Text);
                session.Label.ForeColor = BoxColor;
                session.Label.FontSize = 11;

                VAn.AddAnnotation(labels, session.Label);
            }

            sessions.Add(session);
            Place(session, index);

            if (sessions.Count > MaxSessions)
            {
                var oldest = sessions[0];

                boxes.RemoveAnnotation(oldest.Box);

                if (oldest.Label != null)
                    labels.RemoveAnnotation(oldest.Label);

                sessions.RemoveAt(0);
            }
        }

        private void Extend(int index)
        {
            var session = sessions[sessions.Count - 1];
            var bar = VAn.BarVars[index];

            session.High = Math.Max(session.High, bar.High);
            session.Low = Math.Min(session.Low, bar.Low);

            Place(session, index);
        }

        private void Place(SessionBox session, int lastIndex)
        {
            session.Box.X = session.FirstIndex + 0.5;
            session.Box.X2 = lastIndex + 1.5;
            session.Box.Y = session.High;
            session.Box.Y2 = session.Low;

            if (session.Label != null)
            {
                session.Label.X = session.FirstIndex + 1;
                session.Label.Y = session.High;
                session.Label.Text =
                    $"{VAn.GetDoubleTicksDiff(session.Low, session.High)} ticks";
            }
        }
    }
}
