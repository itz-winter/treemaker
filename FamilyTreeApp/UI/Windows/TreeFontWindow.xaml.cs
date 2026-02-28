using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FamilyTreeApp.Core;

namespace FamilyTreeApp.UI.Windows
{
    /// <summary>
    /// Tree-specific font settings window
    /// </summary>
    public partial class TreeFontWindow : Window
    {
        private readonly FamilyTree _tree;
        private bool _isLoading = true;

        public TreeFontWindow(FamilyTree tree)
        {
            InitializeComponent();
            _tree = tree;
            
            PopulateFontFamilies();
            LoadSettings();
            
            _isLoading = false;
            UpdateFontPreview();
        }

        private void PopulateFontFamilies()
        {
            foreach (var fontFamily in Fonts.SystemFontFamilies.OrderBy(f => f.Source))
            {
                FontFamilyComboBox.Items.Add(fontFamily);
            }
        }

        private void LoadSettings()
        {
            // Check if tree is using defaults (empty or null font settings)
            bool useDefaults = string.IsNullOrEmpty(_tree.FontFamily) && _tree.FontSize <= 0;
            UseDefaultCheckBox.IsChecked = useDefaults;
            
            var appSettings = SettingsManager.Load();
            
            // Get the font to display
            string fontFamily = useDefaults ? appSettings.FontFamily : _tree.FontFamily;
            double fontSize = useDefaults ? appSettings.FontSize : _tree.FontSize;
            bool fontBold = useDefaults ? appSettings.FontBold : _tree.FontBold;
            bool fontItalic = useDefaults ? appSettings.FontItalic : _tree.FontItalic;
            
            if (string.IsNullOrEmpty(fontFamily))
            {
                fontFamily = "Segoe UI";
            }
            if (fontSize <= 0)
            {
                fontSize = 14;
            }
            
            // Select font family
            foreach (FontFamily font in FontFamilyComboBox.Items)
            {
                if (font.Source == fontFamily)
                {
                    FontFamilyComboBox.SelectedItem = font;
                    break;
                }
            }
            
            // If not found, select first item
            if (FontFamilyComboBox.SelectedItem == null && FontFamilyComboBox.Items.Count > 0)
            {
                // Try to find Segoe UI
                foreach (FontFamily font in FontFamilyComboBox.Items)
                {
                    if (font.Source == "Segoe UI")
                    {
                        FontFamilyComboBox.SelectedItem = font;
                        break;
                    }
                }
                if (FontFamilyComboBox.SelectedItem == null)
                {
                    FontFamilyComboBox.SelectedIndex = 0;
                }
            }
            
            // Select font size
            foreach (ComboBoxItem item in FontSizeComboBox.Items)
            {
                if (item.Tag != null && double.TryParse(item.Tag.ToString(), out double size) && size == fontSize)
                {
                    FontSizeComboBox.SelectedItem = item;
                    break;
                }
            }
            
            // Apply style options
            FontBoldCheckBox.IsChecked = fontBold;
            FontItalicCheckBox.IsChecked = fontItalic;
            
            // Update controls enabled state
            UpdateControlsEnabledState();
        }

        private void UseDefaultCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            
            UpdateControlsEnabledState();
            
            // If switching to defaults, load the default settings preview
            if (UseDefaultCheckBox.IsChecked == true)
            {
                var appSettings = SettingsManager.Load();
                
                // Select font family
                foreach (FontFamily font in FontFamilyComboBox.Items)
                {
                    if (font.Source == appSettings.FontFamily)
                    {
                        FontFamilyComboBox.SelectedItem = font;
                        break;
                    }
                }
                
                // Select font size
                foreach (ComboBoxItem item in FontSizeComboBox.Items)
                {
                    if (item.Tag != null && double.TryParse(item.Tag.ToString(), out double size) && size == appSettings.FontSize)
                    {
                        FontSizeComboBox.SelectedItem = item;
                        break;
                    }
                }
                
                FontBoldCheckBox.IsChecked = appSettings.FontBold;
                FontItalicCheckBox.IsChecked = appSettings.FontItalic;
            }
            
            UpdateFontPreview();
        }

        private void UpdateControlsEnabledState()
        {
            bool enabled = UseDefaultCheckBox.IsChecked != true;
            FontFamilyComboBox.IsEnabled = enabled;
            FontSizeComboBox.IsEnabled = enabled;
            FontBoldCheckBox.IsEnabled = enabled;
            FontItalicCheckBox.IsEnabled = enabled;
        }

        private void FontOption_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || _isLoading) return;
            UpdateFontPreview();
        }

        private void FontOption_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || _isLoading) return;
            UpdateFontPreview();
        }

        private void UpdateFontPreview()
        {
            if (FontPreviewText == null) return;

            try
            {
                // Get selected font
                if (FontFamilyComboBox.SelectedItem is FontFamily selectedFont)
                {
                    FontPreviewText.FontFamily = selectedFont;
                }
                
                // Get selected size
                if (FontSizeComboBox.SelectedItem is ComboBoxItem sizeItem && sizeItem.Tag != null)
                {
                    if (double.TryParse(sizeItem.Tag.ToString(), out double size))
                    {
                        FontPreviewText.FontSize = size;
                    }
                }
                
                // Apply styles
                FontPreviewText.FontWeight = FontBoldCheckBox.IsChecked == true ? FontWeights.Bold : FontWeights.Normal;
                FontPreviewText.FontStyle = FontItalicCheckBox.IsChecked == true ? FontStyles.Italic : FontStyles.Normal;
            }
            catch
            {
                // Ignore errors during preview update
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (UseDefaultCheckBox.IsChecked == true)
            {
                // Clear tree-specific settings (use defaults)
                _tree.FontFamily = "";
                _tree.FontSize = 0;
                _tree.FontBold = false;
                _tree.FontItalic = false;
            }
            else
            {
                // Save tree-specific settings
                if (FontFamilyComboBox.SelectedItem is FontFamily selectedFont)
                {
                    _tree.FontFamily = selectedFont.Source;
                }
                
                if (FontSizeComboBox.SelectedItem is ComboBoxItem sizeItem && sizeItem.Tag != null)
                {
                    if (double.TryParse(sizeItem.Tag.ToString(), out double size))
                    {
                        _tree.FontSize = size;
                    }
                }
                
                _tree.FontBold = FontBoldCheckBox.IsChecked ?? false;
                _tree.FontItalic = FontItalicCheckBox.IsChecked ?? false;
            }
            
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
