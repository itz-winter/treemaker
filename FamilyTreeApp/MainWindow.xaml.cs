using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FamilyTreeApp.Core;
using FamilyTreeApp.UI.Windows;
using Microsoft.Win32;

namespace FamilyTreeApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private FamilyTree _familyTree;
    private string? _currentFilePath;
    private ConnectionStyleSettings _connectionStyles = new();
    private Core.CommandManager _commandManager = new();
    private ToolbarManager _toolbarManager = new();
    
    // Custom routed commands
    public static readonly RoutedCommand NewTreeCommand = new();
    public static readonly RoutedCommand OpenTreeCommand = new();
    public static readonly RoutedCommand SaveTreeCommand = new();
    public static readonly RoutedCommand SaveAsCommand = new();
    public static readonly RoutedCommand UndoCommand = new();
    public static readonly RoutedCommand RedoCommand = new();
    public static readonly RoutedCommand DeleteCommand = new();
    public static readonly RoutedCommand ResetViewCommand = new();
    public static readonly RoutedCommand ToggleGridCommand = new();
    public static readonly RoutedCommand ToggleLayoutCommand = new();
    public static readonly RoutedCommand AddNodeCommand = new();
    public static readonly RoutedCommand ZoomInCommand = new();
    public static readonly RoutedCommand ZoomOutCommand = new();

    public MainWindow()
    {
        InitializeComponent();
        
        // Set up command bindings
        SetupCommandBindings();

        // Initialize a new family tree
        _familyTree = new FamilyTree();
        MainCanvas.SetFamilyTree(_familyTree);
        MainCanvas.SetCommandManager(_commandManager);

        // Initialize toolbar
        InitializeToolbar();

        // Subscribe to dock position changes to adjust tool panel orientation
        ToolsPanel.DockPositionChanged += OnToolsPanelDockPositionChanged;
        ToolsPanel.Docked += OnToolsPanelDocked;

        // Subscribe to canvas events
        MainCanvas.ZoomChanged += OnZoomChanged;
        MainCanvas.NodeSelectionChanged += OnNodeSelectionChanged;

        // Subscribe to collection changes for status bar
        _familyTree.Nodes.CollectionChanged += (s, e) => UpdateStatusBar();
        _familyTree.Connections.CollectionChanged += (s, e) => UpdateStatusBar();

        UpdateStatusBar();
    }
    
    private void SetupCommandBindings()
    {
        // Command bindings
        CommandBindings.Add(new CommandBinding(NewTreeCommand, (s, e) => NewTree_Click(s, e)));
        CommandBindings.Add(new CommandBinding(OpenTreeCommand, (s, e) => OpenTree_Click(s, e)));
        CommandBindings.Add(new CommandBinding(SaveTreeCommand, (s, e) => SaveTree_Click(s, e)));
        CommandBindings.Add(new CommandBinding(SaveAsCommand, (s, e) => SaveTreeAs_Click(s, e)));
        CommandBindings.Add(new CommandBinding(UndoCommand, (s, e) => Undo_Click(s, e)));
        CommandBindings.Add(new CommandBinding(RedoCommand, (s, e) => Redo_Click(s, e)));
        CommandBindings.Add(new CommandBinding(DeleteCommand, (s, e) => DeleteSelected_Click(s, e)));
        CommandBindings.Add(new CommandBinding(ResetViewCommand, (s, e) => ResetView_Click(s, e)));
        CommandBindings.Add(new CommandBinding(ToggleGridCommand, (s, e) => ShowGrid_Click(s, e)));
        CommandBindings.Add(new CommandBinding(ToggleLayoutCommand, (s, e) => {
            if (FreeLayoutMenuItem.IsChecked)
                LayoutModeFixed_Click(s, e);
            else
                LayoutModeFree_Click(s, e);
        }));
        CommandBindings.Add(new CommandBinding(AddNodeCommand, (s, e) => AddNode_Click(s, e)));
        CommandBindings.Add(new CommandBinding(ZoomInCommand, (s, e) => ZoomIn_Click(s, e)));
        CommandBindings.Add(new CommandBinding(ZoomOutCommand, (s, e) => ZoomOut_Click(s, e)));
        
        // Input bindings (keyboard shortcuts)
        InputBindings.Add(new KeyBinding(NewTreeCommand, Key.N, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(OpenTreeCommand, Key.O, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(SaveTreeCommand, Key.S, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(SaveAsCommand, Key.S, ModifierKeys.Control | ModifierKeys.Shift));
        InputBindings.Add(new KeyBinding(UndoCommand, Key.Z, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(RedoCommand, Key.Y, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(DeleteCommand, Key.Delete, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(ResetViewCommand, Key.R, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(ToggleGridCommand, Key.G, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(ToggleLayoutCommand, Key.F, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(AddNodeCommand, Key.Space, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(ZoomInCommand, Key.OemPlus, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(ZoomInCommand, Key.Add, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(ZoomOutCommand, Key.OemMinus, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(ZoomOutCommand, Key.Subtract, ModifierKeys.Control));
    }

    private void InitializeToolbar()
    {
        _toolbarManager.Load();
        // Filter out separators and items without a valid command/name for display
        var visibleTools = _toolbarManager.Tools
            .Where(t => !t.IsSeparator && t.IsVisible && !string.IsNullOrEmpty(t.Name) && t.Id != "royalty")
            .OrderBy(t => t.Order)
            .ToList();
        ToolsListBox.DataContext = visibleTools;
        ToolsListBox.ItemsSource = visibleTools;
    }

    private void RefreshToolbar()
    {
        var visibleTools = _toolbarManager.Tools
            .Where(t => !t.IsSeparator && t.IsVisible && !string.IsNullOrEmpty(t.Name) && t.Id != "royalty")
            .OrderBy(t => t.Order)
            .ToList();
        ToolsListBox.ItemsSource = visibleTools;
    }

    private void UpdateStatusBar()
    {
        NodeCountText.Text = _familyTree.Nodes.Count.ToString();
        ConnectionCountText.Text = _familyTree.Connections.Count.ToString();
        AlignmentText.Text = _familyTree.AlignmentMode == AlignmentMode.TopDown ? "Top-Down" : "Left-Right";
    }

    private void OnZoomChanged(object? sender, double zoom)
    {
        ZoomText.Text = $"{(int)(zoom * 100)}%";
    }

    private void OnNodeSelectionChanged(object? sender, Node node)
    {
        Title = $"Family Tree Builder - {node.Name}";
    }

    #region File Menu

    private void NewTree_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Create a new tree? Unsaved changes will be lost.",
            "New Tree", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _familyTree = new FamilyTree();
            MainCanvas.SetFamilyTree(_familyTree);
            _currentFilePath = null;
            Title = "Family Tree Builder";
            UpdateStatusBar();
        }
    }

    private void OpenTree_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Tree Files (*.tree)|*.tree|All Files (*.*)|*.*",
            Title = "Open Family Tree"
        };

        if (dialog.ShowDialog() == true)
        {
            var loadedTree = FileService.LoadTree(dialog.FileName);
            if (loadedTree != null)
            {
                _familyTree = loadedTree;
                _currentFilePath = dialog.FileName;
                MainCanvas.SetFamilyTree(_familyTree);
                
                // Update menu states
                AllowIncestMenuItem.IsChecked = _familyTree.AllowIncest;
                AllowThreesomeMenuItem.IsChecked = _familyTree.AllowThreesome;
                TopDownMenuItem.IsChecked = _familyTree.AlignmentMode == AlignmentMode.TopDown;
                LeftRightMenuItem.IsChecked = _familyTree.AlignmentMode == AlignmentMode.LeftRight;
                CurvesStyleMenuItem.IsChecked = _familyTree.LineStyle == LineStyle.Curves;
                SquareStyleMenuItem.IsChecked = _familyTree.LineStyle == LineStyle.Square;
                
                // Subscribe to collection changes for status bar
                _familyTree.Nodes.CollectionChanged += (s, ev) => UpdateStatusBar();
                _familyTree.Connections.CollectionChanged += (s, ev) => UpdateStatusBar();
                
                UpdateStatusBar();
                Title = $"Family Tree Builder - {System.IO.Path.GetFileName(dialog.FileName)}";
            }
        }
    }

    private void SaveTree_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            SaveTreeAs_Click(sender, e);
        }
        else
        {
            if (FileService.SaveTree(_familyTree, _currentFilePath))
            {
                Title = $"Family Tree Builder - {System.IO.Path.GetFileName(_currentFilePath)}";
            }
        }
    }

    private void SaveTreeAs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Tree Files (*.tree)|*.tree|All Files (*.*)|*.*",
            Title = "Save Family Tree",
            DefaultExt = ".tree"
        };

        if (dialog.ShowDialog() == true)
        {
            _currentFilePath = dialog.FileName;
            if (FileService.SaveTree(_familyTree, _currentFilePath))
            {
                Title = $"Family Tree Builder - {System.IO.Path.GetFileName(_currentFilePath)}";
            }
        }
    }

    private void ExportPreview_Click(object sender, RoutedEventArgs e)
    {
        var previewWindow = new ExportPreviewWindow(MainCanvas);
        previewWindow.Owner = this;
        previewWindow.ShowDialog();
    }

    private void ExportPng_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "PNG Files (*.png)|*.png",
            Title = "Export as PNG",
            DefaultExt = ".png"
        };

        if (dialog.ShowDialog() == true)
        {
            MainCanvas.ExportToPng(dialog.FileName);
        }
    }

    private void ExportSvg_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "SVG Files (*.svg)|*.svg",
            Title = "Export as SVG",
            DefaultExt = ".svg"
        };

        if (dialog.ShowDialog() == true)
        {
            MainCanvas.ExportToSvg(dialog.FileName);
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region Edit Menu

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_commandManager.CanUndo)
        {
            _commandManager.Undo();
            MainCanvas.RefreshCanvas();
            UpdateStatusBar();
        }
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        if (_commandManager.CanRedo)
        {
            _commandManager.Redo();
            MainCanvas.RefreshCanvas();
            UpdateStatusBar();
        }
    }

    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        MainCanvas.DeleteSelectedNode();
    }

    #endregion

    #region Insert Menu

    private void AddTextBox_Click(object sender, RoutedEventArgs e)
    {
        MainCanvas.AddTextBox();
    }

    #endregion

    #region View Menu

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        MainCanvas.ZoomIn();
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        MainCanvas.ZoomOut();
    }

    private void ResetView_Click(object sender, RoutedEventArgs e)
    {
        MainCanvas.ResetView();
    }

    private void ShowToolsPanel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            if (menuItem.IsChecked)
            {
                ToolsPanel.Visibility = Visibility.Visible;
                if (ToolsPanel.IsFloating)
                {
                    ToolsPanel.Dock();
                }
            }
            else
            {
                ToolsPanel.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void ShowGrid_Click(object sender, RoutedEventArgs e)
    {
        var isChecked = false;

        if (sender is MenuItem menuItem)
        {
            isChecked = menuItem.IsChecked;
            GridCheckBox.IsChecked = isChecked;
        }
        else if (sender is CheckBox checkBox)
        {
            isChecked = checkBox.IsChecked ?? false;
            ShowGridMenuItem.IsChecked = isChecked;
        }

        MainCanvas.ShowGrid = isChecked;
    }

    private void LineStyleCurves_Click(object sender, RoutedEventArgs e)
    {
        _familyTree.LineStyle = LineStyle.Curves;
        CurvesStyleMenuItem.IsChecked = true;
        SquareStyleMenuItem.IsChecked = false;
        MainCanvas.SetLineStyle(LineStyle.Curves);
    }

    private void LineStyleSquare_Click(object sender, RoutedEventArgs e)
    {
        _familyTree.LineStyle = LineStyle.Square;
        CurvesStyleMenuItem.IsChecked = false;
        SquareStyleMenuItem.IsChecked = true;
        MainCanvas.SetLineStyle(LineStyle.Square);
    }

    private void LayoutModeFixed_Click(object sender, RoutedEventArgs e)
    {
        _familyTree.LayoutMode = LayoutMode.Fixed;
        FixedLayoutMenuItem.IsChecked = true;
        FreeLayoutMenuItem.IsChecked = false;
        MainCanvas.SetLayoutMode(LayoutMode.Fixed);
    }

    private void LayoutModeFree_Click(object sender, RoutedEventArgs e)
    {
        _familyTree.LayoutMode = LayoutMode.Free;
        FixedLayoutMenuItem.IsChecked = false;
        FreeLayoutMenuItem.IsChecked = true;
        MainCanvas.SetLayoutMode(LayoutMode.Free);
    }

    private void SnapToGrid_Click(object sender, RoutedEventArgs e)
    {
        SnapToGridMenuItem.IsChecked = !SnapToGridMenuItem.IsChecked;
        MainCanvas.SetSnapToGrid(SnapToGridMenuItem.IsChecked);
    }

    private void SnapToAngle_Click(object sender, RoutedEventArgs e)
    {
        SnapToAngleMenuItem.IsChecked = !SnapToAngleMenuItem.IsChecked;
        MainCanvas.SetSnapToAngle(SnapToAngleMenuItem.IsChecked);
    }

    private void SnapToGeometry_Click(object sender, RoutedEventArgs e)
    {
        SnapToGeometryMenuItem.IsChecked = !SnapToGeometryMenuItem.IsChecked;
        MainCanvas.SetSnapToGeometry(SnapToGeometryMenuItem.IsChecked);
    }

    #endregion

    #region Tree Menu

    private void AddNode_Click(object sender, RoutedEventArgs e)
    {
        MainCanvas.AddNewNodeAtCenter();
    }

    private void AddLink_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement link creation mode
        MessageBox.Show("Select a node and use the + buttons to add connections.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void AutoLayout_Click(object sender, RoutedEventArgs e)
    {
        MainCanvas.ApplyAutoLayout();
        UpdateStatusBar();
    }

    private void AlignmentTopDown_Click(object sender, RoutedEventArgs e)
    {
        _familyTree.AlignmentMode = AlignmentMode.TopDown;
        MainCanvas.SetAlignment(Core.LayoutEngine.AlignmentMode.TopDown);
        TopDownMenuItem.IsChecked = true;
        LeftRightMenuItem.IsChecked = false;
        UpdateStatusBar();
    }

    private void AlignmentLeftRight_Click(object sender, RoutedEventArgs e)
    {
        _familyTree.AlignmentMode = AlignmentMode.LeftRight;
        MainCanvas.SetAlignment(Core.LayoutEngine.AlignmentMode.LeftRight);
        TopDownMenuItem.IsChecked = false;
        LeftRightMenuItem.IsChecked = true;
        UpdateStatusBar();
    }

    private void TreeFontSettings_Click(object sender, RoutedEventArgs e)
    {
        var fontWindow = new UI.Windows.TreeFontWindow(_familyTree);
        fontWindow.Owner = this;
        if (fontWindow.ShowDialog() == true)
        {
            // Refresh all nodes to apply new font
            MainCanvas.RefreshAllNodes();
        }
    }

    #endregion

    #region Groups Menu

    private void ManageGroups_Click(object sender, RoutedEventArgs e)
    {
        var groupsWindow = new UI.Windows.GroupsWindow(_familyTree);
        groupsWindow.Owner = this;
        groupsWindow.Show(); // Non-modal so user can still edit tree
    }

    #endregion

    #region Appearance Menu

    private void Colors_Click(object sender, RoutedEventArgs e)
    {
        var colorsWindow = new ColorsWindow { Owner = this };
        if (colorsWindow.ShowDialog() == true)
        {
            // Colors are saved to settings; could apply live here if needed
            MainCanvas.RefreshCanvas();
        }
    }

    private void ConnectionStyles_Click(object sender, RoutedEventArgs e)
    {
        var window = new ConnectionStylesWindow(_connectionStyles);
        window.Owner = this;
        window.ShowDialog();
        _connectionStyles = window.Settings;
        MainCanvas.SetConnectionStyles(_connectionStyles);
    }

    private void Royalty_Click(object sender, RoutedEventArgs e)
    {
        // Royalty is handled via node context menu
    }

    #endregion

    #region Settings Menu

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new UI.Windows.SettingsWindow { Owner = this };
        if (settingsWindow.ShowDialog() == true)
        {
            ApplySettings();
        }
    }

    private void Keybinds_Click(object sender, RoutedEventArgs e)
    {
        var keybindsWindow = new UI.Windows.KeybindsWindow { Owner = this };
        keybindsWindow.ShowDialog();
    }

    private void CustomizeToolbar_Click(object sender, RoutedEventArgs e)
    {
        var toolbarWindow = new UI.Windows.ToolbarCustomizeWindow(_toolbarManager) { Owner = this };
        if (toolbarWindow.ShowDialog() == true)
        {
            // Refresh toolbar display
            RefreshToolbar();
        }
        else
        {
            // Reload to restore original state
            _toolbarManager.Load();
            RefreshToolbar();
        }
    }

    private void DynamicTool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string toolId)
        {
            var tool = _toolbarManager.Tools.FirstOrDefault(t => t.Id == toolId);
            if (tool != null)
            {
                ExecuteToolCommand(tool.CommandName);
            }
        }
    }

    #region Tool Drag-Drop Reordering

    private Point _toolDragStartPoint;
    private ToolItem? _draggedTool;

    private void ToolsListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _toolDragStartPoint = e.GetPosition(null);
    }

    private void ToolsListBox_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        var currentPos = e.GetPosition(null);
        var diff = _toolDragStartPoint - currentPos;

        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            var listBox = sender as ListBox;
            var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);

            if (listBoxItem != null && listBox != null)
            {
                _draggedTool = listBoxItem.DataContext as ToolItem;
                if (_draggedTool != null)
                {
                    DragDrop.DoDragDrop(listBoxItem, _draggedTool, DragDropEffects.Move);
                }
            }
        }
    }

    private void ToolsListBox_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(ToolItem)))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }
    }

    private void ToolsListBox_Drop(object sender, DragEventArgs e)
    {
        if (_draggedTool == null)
            return;

        var listBox = sender as ListBox;
        if (listBox == null)
            return;

        var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (targetItem == null)
            return;

        var targetTool = targetItem.DataContext as ToolItem;
        if (targetTool == null || targetTool == _draggedTool)
            return;

        // Swap the order values
        int draggedOrder = _draggedTool.Order;
        int targetOrder = targetTool.Order;

        _draggedTool.Order = targetOrder;
        targetTool.Order = draggedOrder;

        // Save and refresh
        _toolbarManager.Save();
        RefreshToolbar();

        _draggedTool = null;
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

    #region Tool Panel Orientation

    private void OnToolsPanelDockPositionChanged(object? sender, UI.Controls.DockPosition newPosition)
    {
        UpdateToolPanelOrientation(newPosition);
    }

    private void OnToolsPanelDocked(object? sender, EventArgs e)
    {
        UpdateToolPanelOrientation(ToolsPanel.DockPosition);
    }

    private void UpdateToolPanelOrientation(UI.Controls.DockPosition position)
    {
        // Find the StackPanel inside the ScrollViewer
        if (ToolsPanel.Content is ScrollViewer scrollViewer &&
            scrollViewer.Content is StackPanel stackPanel)
        {
            bool isHorizontal = position == UI.Controls.DockPosition.Top || 
                               position == UI.Controls.DockPosition.Bottom;

            // Update the main StackPanel orientation
            stackPanel.Orientation = isHorizontal ? Orientation.Horizontal : Orientation.Vertical;
            
            // Update ScrollViewer scroll bars
            scrollViewer.HorizontalScrollBarVisibility = isHorizontal ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
            scrollViewer.VerticalScrollBarVisibility = isHorizontal ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;

            // Update ListBox panel template for horizontal layout
            if (isHorizontal)
            {
                ToolsListBox.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(StackPanel))
                {
                    Name = "HorizontalPanel"
                });
                var factory = new FrameworkElementFactory(typeof(StackPanel));
                factory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
                ToolsListBox.ItemsPanel = new ItemsPanelTemplate(factory);
            }
            else
            {
                var factory = new FrameworkElementFactory(typeof(StackPanel));
                factory.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
                ToolsListBox.ItemsPanel = new ItemsPanelTemplate(factory);
            }
        }
    }

    #endregion

    private void CustomizeTools_Click(object sender, RoutedEventArgs e)
    {
        var toolbarWindow = new UI.Windows.ToolbarCustomizeWindow(_toolbarManager) { Owner = this };
        if (toolbarWindow.ShowDialog() == true)
        {
            RefreshToolbar();
        }
        else
        {
            _toolbarManager.Load();
            RefreshToolbar();
        }
    }

    private void ExecuteToolCommand(string commandName)
    {
        switch (commandName)
        {
            case "AddNode":
                AddNode_Click(this, new RoutedEventArgs());
                break;
            case "AddLink":
                AddLink_Click(this, new RoutedEventArgs());
                break;
            case "Royalty":
                Royalty_Click(this, new RoutedEventArgs());
                break;
            case "Colors":
                Colors_Click(this, new RoutedEventArgs());
                break;
            case "Groups":
                ManageGroups_Click(this, new RoutedEventArgs());
                break;
            case "ZoomIn":
                ZoomIn_Click(this, new RoutedEventArgs());
                break;
            case "ZoomOut":
                ZoomOut_Click(this, new RoutedEventArgs());
                break;
            case "ResetView":
                ResetView_Click(this, new RoutedEventArgs());
                break;
            case "ToggleGrid":
                ShowGrid_Click(this, new RoutedEventArgs());
                break;
            case "Settings":
                Settings_Click(this, new RoutedEventArgs());
                break;
            case "Save":
                SaveTree_Click(this, new RoutedEventArgs());
                break;
            case "Open":
                OpenTree_Click(this, new RoutedEventArgs());
                break;
            case "New":
                NewTree_Click(this, new RoutedEventArgs());
                break;
            case "Undo":
                Undo_Click(this, new RoutedEventArgs());
                break;
            case "Redo":
                Redo_Click(this, new RoutedEventArgs());
                break;
            case "Delete":
                DeleteSelected_Click(this, new RoutedEventArgs());
                break;
            case "AutoLayout":
                AutoLayout_Click(this, new RoutedEventArgs());
                break;
            case "ExportPng":
                ExportPng_Click(this, new RoutedEventArgs());
                break;
            case "ExportSvg":
                ExportSvg_Click(this, new RoutedEventArgs());
                break;
            case "FontSettings":
                TreeFontSettings_Click(this, new RoutedEventArgs());
                break;
            default:
                // Custom tool - could show a message or do nothing
                MessageBox.Show($"Custom tool command: {commandName}", "Tool", MessageBoxButton.OK, MessageBoxImage.Information);
                break;
        }
    }
    
    private void ApplySettings()
    {
        var settings = SettingsManager.Current;
        
        // Apply grid setting
        MainCanvas.ShowGrid = settings.ShowGrid;
        GridCheckBox.IsChecked = settings.ShowGrid;
        ShowGridMenuItem.IsChecked = settings.ShowGrid;
        
        // Apply line style
        if (settings.LineStyle == "Curves")
        {
            MainCanvas.SetLineStyle(LineStyle.Curves);
            CurvesStyleMenuItem.IsChecked = true;
            SquareStyleMenuItem.IsChecked = false;
        }
        else
        {
            MainCanvas.SetLineStyle(LineStyle.Square);
            CurvesStyleMenuItem.IsChecked = false;
            SquareStyleMenuItem.IsChecked = true;
        }
        
        // Apply layout mode
        if (settings.LayoutMode == "Fixed")
        {
            MainCanvas.SetLayoutMode(LayoutMode.Fixed);
            FixedLayoutMenuItem.IsChecked = true;
            FreeLayoutMenuItem.IsChecked = false;
        }
        else
        {
            MainCanvas.SetLayoutMode(LayoutMode.Free);
            FixedLayoutMenuItem.IsChecked = false;
            FreeLayoutMenuItem.IsChecked = true;
        }
        
        // Apply rules
        _familyTree.AllowIncest = settings.AllowIncest;
        _familyTree.AllowThreesome = settings.AllowThreesome;
        AllowIncestMenuItem.IsChecked = settings.AllowIncest;
        AllowThreesomeMenuItem.IsChecked = settings.AllowThreesome;
        
        MainCanvas.RefreshCanvas();
    }

    private void AllowIncest_Click(object sender, RoutedEventArgs e)
    {
        _familyTree.AllowIncest = AllowIncestMenuItem.IsChecked;
    }

    private void AllowThreesome_Click(object sender, RoutedEventArgs e)
    {
        _familyTree.AllowThreesome = AllowThreesomeMenuItem.IsChecked;
    }

    #endregion

    #region Help Menu

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Family Tree Builder\n\nVersion 1.0.0\n\nA customizable family tree application.",
            "About", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    #endregion

    #region Keyboard Shortcuts

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Most keybinds are handled via CommandBindings in SetupCommandBindings()
        // This handler is for Escape key only (clear selection)
        if (e.OriginalSource is TextBox) return;
        
        if (e.Key == Key.Escape)
        {
            MainCanvas.ClearSelection();
            e.Handled = true;
        }
    }

    #endregion
}