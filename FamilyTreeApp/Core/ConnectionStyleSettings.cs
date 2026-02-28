using System.Windows.Media;

namespace FamilyTreeApp.Core
{
    /// <summary>
    /// Connection style settings for customizing line appearance.
    /// </summary>
    public class ConnectionStyleSettings
    {
        public Color BiologicalColor { get; set; } = Color.FromRgb(0, 212, 170);
        public double BiologicalWidth { get; set; } = 2;
        public LineDashStyle BiologicalDashStyle { get; set; } = LineDashStyle.Solid;

        public Color AdoptedColor { get; set; } = Colors.Orange;
        public double AdoptedWidth { get; set; } = 2;
        public LineDashStyle AdoptedDashStyle { get; set; } = LineDashStyle.Dashed;

        public Color StepColor { get; set; } = Colors.MediumPurple;
        public double StepWidth { get; set; } = 2;
        public LineDashStyle StepDashStyle { get; set; } = LineDashStyle.Dotted;

        public Color PartnerColor { get; set; } = Colors.HotPink;
        public double PartnerWidth { get; set; } = 2;
        public LineDashStyle PartnerDashStyle { get; set; } = LineDashStyle.Solid;

        public Color FormerPartnerColor { get; set; } = Colors.Gray;
        public double FormerPartnerWidth { get; set; } = 2;
        public LineDashStyle FormerPartnerDashStyle { get; set; } = LineDashStyle.Dashed;

        public Color HiddenColor { get; set; } = Color.FromArgb(128, 128, 128, 128);
        public double HiddenWidth { get; set; } = 1;
        public LineDashStyle HiddenDashStyle { get; set; } = LineDashStyle.Dashed;

        public (Color color, double width, LineDashStyle style) GetStyleForType(ConnectionType type)
        {
            return type switch
            {
                ConnectionType.Biological => (BiologicalColor, BiologicalWidth, BiologicalDashStyle),
                ConnectionType.Adopted => (AdoptedColor, AdoptedWidth, AdoptedDashStyle),
                ConnectionType.Step => (StepColor, StepWidth, StepDashStyle),
                ConnectionType.Partner => (PartnerColor, PartnerWidth, PartnerDashStyle),
                ConnectionType.FormerPartner => (FormerPartnerColor, FormerPartnerWidth, FormerPartnerDashStyle),
                ConnectionType.Hidden => (HiddenColor, HiddenWidth, HiddenDashStyle),
                _ => (BiologicalColor, BiologicalWidth, BiologicalDashStyle)
            };
        }
    }

    /// <summary>
    /// Line dash style for connection lines.
    /// </summary>
    public enum LineDashStyle
    {
        Solid = 0,
        Dashed = 1,
        Dotted = 2
    }
}
