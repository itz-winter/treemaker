using System.Windows;

namespace FamilyTreeApp.UI.Controls
{
    /// <summary>
    /// A transparent overlay window that shows where a panel will dock.
    /// </summary>
    public partial class DockPreviewWindow : Window
    {
        public DockPreviewWindow()
        {
            InitializeComponent();
        }

        public void ShowPreview(Rect bounds)
        {
            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;
            Show();
        }

        public void HidePreview()
        {
            Hide();
        }
    }
}
