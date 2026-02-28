using System;
using System.Windows;
using System.Windows.Media;
using FamilyTreeApp.Core;

namespace FamilyTreeApp.UI.Windows
{
    /// <summary>
    /// Window for customizing colors throughout the application.
    /// </summary>
    public partial class ColorsWindow : Window
    {
        private AppSettings _settings;
        
        public ColorsWindow()
        {
            InitializeComponent();
            _settings = SettingsManager.Current;
            LoadColors();
        }
        
        private void LoadColors()
        {
            NodeFillPicker.SelectedColor = ParseColor(_settings.NodeFillColor);
            NodeBorderPicker.SelectedColor = ParseColor(_settings.NodeBorderColor);
            NodeTextPicker.SelectedColor = ParseColor(_settings.NodeTextColor);
            CanvasBackgroundPicker.SelectedColor = ParseColor(_settings.CanvasBackgroundColor);
            GridColorPicker.SelectedColor = ParseColor(_settings.GridColor);
        }
        
        private Color ParseColor(string hex)
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(hex);
            }
            catch
            {
                return Colors.White;
            }
        }
        
        private string ColorToHex(Color? color)
        {
            if (color == null)
            {
                return "#FFFFFF";
            }
            return $"#{color.Value.R:X2}{color.Value.G:X2}{color.Value.B:X2}";
        }
        
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _settings.NodeFillColor = ColorToHex(NodeFillPicker.SelectedColor);
            _settings.NodeBorderColor = ColorToHex(NodeBorderPicker.SelectedColor);
            _settings.NodeTextColor = ColorToHex(NodeTextPicker.SelectedColor);
            _settings.CanvasBackgroundColor = ColorToHex(CanvasBackgroundPicker.SelectedColor);
            _settings.GridColor = ColorToHex(GridColorPicker.SelectedColor);
            
            SettingsManager.Save(_settings);
            DialogResult = true;
            Close();
        }
        
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to restore all colors to their defaults?",
                "Restore Defaults",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );
            
            if (result == MessageBoxResult.Yes)
            {
                var defaults = AppSettings.GetDefaults();
                _settings.NodeFillColor = defaults.NodeFillColor;
                _settings.NodeBorderColor = defaults.NodeBorderColor;
                _settings.NodeTextColor = defaults.NodeTextColor;
                _settings.CanvasBackgroundColor = defaults.CanvasBackgroundColor;
                _settings.GridColor = defaults.GridColor;
                LoadColors();
            }
        }
        
        private void ConnectionStylesLink_Click(object sender, RoutedEventArgs e)
        {
            // Close this window and let the user know to access via menu
            MessageBox.Show(
                "Please use the View → Connection Styles menu to customize connection line colors.",
                "Connection Styles",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
    }
}
