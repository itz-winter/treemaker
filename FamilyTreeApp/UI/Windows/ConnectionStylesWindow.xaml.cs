using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FamilyTreeApp.Core;
using Xceed.Wpf.Toolkit;

namespace FamilyTreeApp.UI.Windows
{
    public partial class ConnectionStylesWindow : Window
    {
        public ConnectionStyleSettings Settings { get; private set; }

        public ConnectionStylesWindow(ConnectionStyleSettings? settings = null)
        {
            InitializeComponent();
            Settings = settings ?? new ConnectionStyleSettings();
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Load Biological settings
            BiologicalColor.SelectedColor = Settings.BiologicalColor;
            BiologicalWidth.Value = Settings.BiologicalWidth;
            BiologicalStyle.SelectedIndex = (int)Settings.BiologicalDashStyle;

            // Load Adopted settings
            AdoptedColor.SelectedColor = Settings.AdoptedColor;
            AdoptedWidth.Value = Settings.AdoptedWidth;
            AdoptedStyle.SelectedIndex = (int)Settings.AdoptedDashStyle;

            // Load Step settings
            StepColor.SelectedColor = Settings.StepColor;
            StepWidth.Value = Settings.StepWidth;
            StepStyle.SelectedIndex = (int)Settings.StepDashStyle;

            // Load Partner settings
            PartnerColor.SelectedColor = Settings.PartnerColor;
            PartnerWidth.Value = Settings.PartnerWidth;
            PartnerStyle.SelectedIndex = (int)Settings.PartnerDashStyle;
        }

        private void SaveSettings()
        {
            // Save Biological settings
            Settings.BiologicalColor = BiologicalColor.SelectedColor ?? Colors.Teal;
            Settings.BiologicalWidth = BiologicalWidth.Value;
            Settings.BiologicalDashStyle = (LineDashStyle)BiologicalStyle.SelectedIndex;

            // Save Adopted settings
            Settings.AdoptedColor = AdoptedColor.SelectedColor ?? Colors.Orange;
            Settings.AdoptedWidth = AdoptedWidth.Value;
            Settings.AdoptedDashStyle = (LineDashStyle)AdoptedStyle.SelectedIndex;

            // Save Step settings
            Settings.StepColor = StepColor.SelectedColor ?? Colors.MediumPurple;
            Settings.StepWidth = StepWidth.Value;
            Settings.StepDashStyle = (LineDashStyle)StepStyle.SelectedIndex;

            // Save Partner settings
            Settings.PartnerColor = PartnerColor.SelectedColor ?? Colors.HotPink;
            Settings.PartnerWidth = PartnerWidth.Value;
            Settings.PartnerDashStyle = (LineDashStyle)PartnerStyle.SelectedIndex;
        }

        private void ConnectionColor_Changed(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (IsLoaded)
                SaveSettings();
        }

        private void ConnectionWidth_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoaded)
                SaveSettings();
        }

        private void ConnectionStyle_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
                SaveSettings();
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            Settings = new ConnectionStyleSettings();
            LoadSettings();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            Close();
        }
    }
}
