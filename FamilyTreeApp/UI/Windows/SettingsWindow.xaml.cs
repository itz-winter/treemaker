using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FamilyTreeApp.Core;

namespace FamilyTreeApp.UI.Windows
{
    /// <summary>
    /// Settings window for configuring application options.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private AppSettings _settings;
        
        public SettingsWindow()
        {
            InitializeComponent();
            _settings = SettingsManager.Current;
            PopulateFontFamilies();
            LoadSettings();
            
            // Subscribe to font changes for live preview
            FontFamilyComboBox.SelectionChanged += (s, e) => UpdateFontPreview();
            FontSizeComboBox.SelectionChanged += (s, e) => UpdateFontPreview();
            FontBoldCheckBox.Checked += (s, e) => UpdateFontPreview();
            FontBoldCheckBox.Unchecked += (s, e) => UpdateFontPreview();
            FontItalicCheckBox.Checked += (s, e) => UpdateFontPreview();
            FontItalicCheckBox.Unchecked += (s, e) => UpdateFontPreview();
        }
        
        private void PopulateFontFamilies()
        {
            var fonts = Fonts.SystemFontFamilies.OrderBy(f => f.Source).ToList();
            foreach (var font in fonts)
            {
                FontFamilyComboBox.Items.Add(new ComboBoxItem 
                { 
                    Content = font.Source, 
                    Tag = font.Source,
                    FontFamily = font
                });
            }
        }
        
        private void LoadSettings()
        {
            // General
            DarkModeCheckBox.IsChecked = _settings.DarkMode;
            ShowGenderCheckBox.IsChecked = _settings.ShowGenderIcons;
            ShowGridCheckBox.IsChecked = _settings.ShowGrid;
            
            // Rules
            AllowIncestCheckBox.IsChecked = _settings.AllowIncest;
            AllowThreesomeCheckBox.IsChecked = _settings.AllowThreesome;
            
            // Layout
            SelectComboBoxItem(AlignmentComboBox, _settings.Alignment);
            SelectComboBoxItem(LineStyleComboBox, _settings.LineStyle);
            SelectComboBoxItem(LayoutModeComboBox, _settings.LayoutMode);
            
            // Royalty
            SelectComboBoxItem(CrownDisplayComboBox, _settings.CrownDisplay);
            
            // Font
            SelectComboBoxItem(FontFamilyComboBox, _settings.FontFamily);
            SelectComboBoxItem(FontSizeComboBox, _settings.FontSize.ToString());
            FontBoldCheckBox.IsChecked = _settings.FontBold;
            FontItalicCheckBox.IsChecked = _settings.FontItalic;
            
            // Confirmations
            ConfirmUnsavedChangesCheckBox.IsChecked = _settings.ConfirmUnsavedChanges;
            
            UpdateFontPreview();
        }
        
        private void UpdateFontPreview()
        {
            if (FontPreviewText == null) return;
            
            var fontFamily = GetSelectedTag(FontFamilyComboBox);
            var fontSizeStr = GetSelectedTag(FontSizeComboBox);
            
            if (!string.IsNullOrEmpty(fontFamily))
            {
                FontPreviewText.FontFamily = new FontFamily(fontFamily);
            }
            
            if (double.TryParse(fontSizeStr, out double fontSize))
            {
                FontPreviewText.FontSize = fontSize;
            }
            
            FontPreviewText.FontWeight = (FontBoldCheckBox.IsChecked ?? false) 
                ? FontWeights.Bold : FontWeights.Normal;
            FontPreviewText.FontStyle = (FontItalicCheckBox.IsChecked ?? false) 
                ? FontStyles.Italic : FontStyles.Normal;
        }
        
        private void SelectComboBoxItem(ComboBox comboBox, string tagValue)
        {
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Tag?.ToString() == tagValue)
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
            // Default to first item if not found
            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }
        
        private string GetSelectedTag(ComboBox comboBox)
        {
            if (comboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                return item.Tag.ToString() ?? "";
            }
            return "";
        }
        
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // General
            _settings.DarkMode = DarkModeCheckBox.IsChecked ?? true;
            _settings.ShowGenderIcons = ShowGenderCheckBox.IsChecked ?? true;
            _settings.ShowGrid = ShowGridCheckBox.IsChecked ?? false;
            
            // Rules
            _settings.AllowIncest = AllowIncestCheckBox.IsChecked ?? false;
            _settings.AllowThreesome = AllowThreesomeCheckBox.IsChecked ?? false;
            
            // Layout
            _settings.Alignment = GetSelectedTag(AlignmentComboBox);
            _settings.LineStyle = GetSelectedTag(LineStyleComboBox);
            _settings.LayoutMode = GetSelectedTag(LayoutModeComboBox);
            
            // Royalty
            _settings.CrownDisplay = GetSelectedTag(CrownDisplayComboBox);
            
            // Font
            if (FontFamilyComboBox.SelectedItem is System.Windows.Media.FontFamily selectedFont)
            {
                _settings.FontFamily = selectedFont.Source;
            }
            if (FontSizeComboBox.SelectedItem is ComboBoxItem sizeItem && sizeItem.Tag != null)
            {
                if (double.TryParse(sizeItem.Tag.ToString(), out double size))
                {
                    _settings.FontSize = size;
                }
            }
            _settings.FontBold = FontBoldCheckBox.IsChecked ?? false;
            _settings.FontItalic = FontItalicCheckBox.IsChecked ?? false;
            
            // Confirmations
            _settings.ConfirmUnsavedChanges = ConfirmUnsavedChangesCheckBox.IsChecked ?? true;
            
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
                "Are you sure you want to restore all settings to their defaults?",
                "Restore Defaults",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );
            
            if (result == MessageBoxResult.Yes)
            {
                _settings.ResetToDefaults();
                LoadSettings();
            }
        }
    }
}
