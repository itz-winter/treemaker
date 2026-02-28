using System.Windows;
using System.Windows.Controls;
using FamilyTreeApp.Core;

namespace FamilyTreeApp.UI.Windows
{
    /// <summary>
    /// First run configuration window shown on initial launch.
    /// </summary>
    public partial class FirstRunWindow : Window
    {
        public FirstRunWindow()
        {
            InitializeComponent();
        }

        private void UseDefaults_Click(object sender, RoutedEventArgs e)
        {
            // Just mark as complete with defaults
            var settings = SettingsManager.Current;
            settings.FirstRunComplete = true;
            SettingsManager.Save(settings);
            
            DialogResult = true;
            Close();
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            // Save the configured settings
            var settings = SettingsManager.Current;
            
            // Appearance
            settings.DarkMode = DarkModeCheckBox.IsChecked ?? true;
            settings.ShowGenderIcons = ShowGenderCheckBox.IsChecked ?? true;
            settings.ShowGrid = ShowGridCheckBox.IsChecked ?? false;
            
            // Layout
            if (AlignmentComboBox.SelectedItem is ComboBoxItem alignItem)
                settings.Alignment = alignItem.Tag?.ToString() ?? "TopDown";
            
            if (LineStyleComboBox.SelectedItem is ComboBoxItem lineItem)
                settings.LineStyle = lineItem.Tag?.ToString() ?? "Curves";
            
            if (LayoutModeComboBox.SelectedItem is ComboBoxItem layoutItem)
                settings.LayoutMode = layoutItem.Tag?.ToString() ?? "Fixed";
            
            // Rules
            settings.AllowIncest = AllowIncestCheckBox.IsChecked ?? false;
            settings.AllowThreesome = AllowThreesomeCheckBox.IsChecked ?? false;
            
            // Royalty
            if (CrownDisplayComboBox.SelectedItem is ComboBoxItem crownItem)
                settings.CrownDisplay = crownItem.Tag?.ToString() ?? "QueenOnly";
            
            // Mark first run as complete
            settings.FirstRunComplete = true;
            
            // Save settings
            SettingsManager.Save(settings);
            
            DialogResult = true;
            Close();
        }
    }
}
