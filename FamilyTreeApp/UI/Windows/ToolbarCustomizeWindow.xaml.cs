using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FamilyTreeApp.Core;

namespace FamilyTreeApp.UI.Windows
{
    /// <summary>
    /// Window for customizing the toolbar - reorder, show/hide, add/delete custom tools.
    /// </summary>
    public partial class ToolbarCustomizeWindow : Window
    {
        private readonly ToolbarManager _toolbarManager;
        private Point _dragStartPoint;
        private ToolItem? _draggedItem;

        public ToolbarCustomizeWindow(ToolbarManager toolbarManager)
        {
            InitializeComponent();
            _toolbarManager = toolbarManager;
            DataContext = _toolbarManager;
            RefreshLists();
        }

        private void RefreshLists()
        {
            ToolsListBox.ItemsSource = null;
            ToolsListBox.ItemsSource = _toolbarManager.Tools;
            HiddenToolsListBox.ItemsSource = null;
            HiddenToolsListBox.ItemsSource = _toolbarManager.GetHiddenTools();
        }

        #region Drag and Drop

        private void ToolsListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void ToolsListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            Point position = e.GetPosition(null);
            Vector diff = _dragStartPoint - position;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                ListBox listBox = (ListBox)sender;
                var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);

                if (listBoxItem != null)
                {
                    _draggedItem = (ToolItem)listBox.ItemContainerGenerator.ItemFromContainer(listBoxItem);
                    if (_draggedItem != null)
                    {
                        DragDrop.DoDragDrop(listBoxItem, _draggedItem, DragDropEffects.Move);
                    }
                }
            }
        }

        private void ToolsListBox_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void ToolsListBox_Drop(object sender, DragEventArgs e)
        {
            if (_draggedItem == null)
                return;

            var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (listBoxItem != null)
            {
                var targetItem = (ToolItem)ToolsListBox.ItemContainerGenerator.ItemFromContainer(listBoxItem);
                if (targetItem != null && targetItem != _draggedItem)
                {
                    int targetIndex = _toolbarManager.Tools.IndexOf(targetItem);
                    _toolbarManager.MoveTool(_draggedItem, targetIndex);
                    RefreshLists();
                }
            }

            _draggedItem = null;
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T ancestor)
                    return ancestor;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        #endregion

        #region Context Menu Actions

        private void Hide_Click(object sender, RoutedEventArgs e)
        {
            if (ToolsListBox.SelectedItem is ToolItem selectedTool)
            {
                _toolbarManager.HideTool(selectedTool);
                RefreshLists();
            }
        }

        private void Show_Click(object sender, RoutedEventArgs e)
        {
            if (ToolsListBox.SelectedItem is ToolItem selectedTool)
            {
                _toolbarManager.ShowTool(selectedTool);
                RefreshLists();
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (ToolsListBox.SelectedItem is ToolItem selectedTool)
            {
                if (selectedTool.IsBuiltIn)
                {
                    MessageBox.Show("Built-in tools cannot be deleted. You can hide them instead.", 
                        "Cannot Delete", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show($"Are you sure you want to delete '{selectedTool.Name}'?",
                    "Delete Tool", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _toolbarManager.DeleteCustomTool(selectedTool);
                    RefreshLists();
                }
            }
        }

        private void ShowHidden_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ToolItem tool)
            {
                _toolbarManager.ShowTool(tool);
                RefreshLists();
            }
        }

        #endregion

        #region Button Actions

        private void AddTool_Click(object sender, RoutedEventArgs e)
        {
            var customToolWindow = new CustomToolWindow();
            customToolWindow.Owner = this;
            
            if (customToolWindow.ShowDialog() == true && customToolWindow.CreatedTool != null)
            {
                _toolbarManager.AddCustomTool(customToolWindow.CreatedTool);
                RefreshLists();
            }
        }

        private void AddSeparator_Click(object sender, RoutedEventArgs e)
        {
            var separator = new ToolItem
            {
                Name = "Separator",
                IsSeparator = true,
                IsBuiltIn = false,
                IsVisible = true,
                Order = _toolbarManager.Tools.Count
            };
            _toolbarManager.AddCustomTool(separator);
            RefreshLists();
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will reset the toolbar to default settings and remove all custom tools. Continue?",
                "Reset Toolbar", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _toolbarManager.ResetToDefaults();
                RefreshLists();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _toolbarManager.Save();
            Close();
        }

        #endregion
    }
}
