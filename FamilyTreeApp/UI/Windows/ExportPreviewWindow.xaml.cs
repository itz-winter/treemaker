using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using FamilyTreeApp.UI.Controls;
using Microsoft.Win32;

namespace FamilyTreeApp.UI.Windows
{
    /// <summary>
    /// Export preview window that shows the tree before exporting.
    /// </summary>
    public partial class ExportPreviewWindow : Window
    {
        private TreeCanvas _treeCanvas;
        private RenderTargetBitmap? _previewBitmap;

        public ExportPreviewWindow(TreeCanvas treeCanvas)
        {
            InitializeComponent();
            _treeCanvas = treeCanvas;
            GeneratePreview();
        }

        private void GeneratePreview()
        {
            _previewBitmap = _treeCanvas.GenerateExportPreview();
            
            if (_previewBitmap != null)
            {
                PreviewImage.Source = _previewBitmap;
                ImageInfoText.Text = $"Size: {_previewBitmap.PixelWidth} x {_previewBitmap.PixelHeight} pixels";
            }
            else
            {
                ImageInfoText.Text = "Nothing to export";
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var selectedFormat = (FormatComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "png";
            
            var saveDialog = new SaveFileDialog();
            
            if (selectedFormat == "svg")
            {
                saveDialog.Filter = "SVG files (*.svg)|*.svg";
                saveDialog.DefaultExt = ".svg";
            }
            else
            {
                saveDialog.Filter = "PNG files (*.png)|*.png";
                saveDialog.DefaultExt = ".png";
            }
            
            saveDialog.FileName = "FamilyTree";

            if (saveDialog.ShowDialog() == true)
            {
                if (selectedFormat == "svg")
                {
                    _treeCanvas.ExportToSvg(saveDialog.FileName);
                }
                else
                {
                    _treeCanvas.ExportToPng(saveDialog.FileName);
                }
                
                DialogResult = true;
                Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
