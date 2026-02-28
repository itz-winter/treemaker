using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FamilyTreeApp.UI.Controls
{
    /// <summary>
    /// Manages dockable panels within a layout.
    /// </summary>
    public class DockManager : Grid
    {
        private readonly Dictionary<DockPosition, Grid> _dockAreas = new();
        private readonly Dictionary<DockPosition, GridSplitter> _splitters = new();
        private readonly List<DockablePanel> _panels = new();
        
        private DockablePanel? _draggedPanel;
        private Point _dragStartPoint;
        private DockPreviewAdorner? _previewAdorner;

        public static readonly DependencyProperty CenterContentProperty =
            DependencyProperty.Register(nameof(CenterContent), typeof(UIElement), typeof(DockManager),
                new PropertyMetadata(null, OnCenterContentChanged));

        public UIElement? CenterContent
        {
            get => (UIElement?)GetValue(CenterContentProperty);
            set => SetValue(CenterContentProperty, value);
        }

        public DockManager()
        {
            InitializeLayout();
        }

        private void InitializeLayout()
        {
            // Create main grid structure
            // Column 0: Left dock area + splitter
            // Column 1: Center area
            // Column 2: Right dock area + splitter
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });

            // Row 0: Top dock area
            // Row 1: Main content row
            // Row 2: Bottom dock area
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(0, GridUnitType.Auto) });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(0, GridUnitType.Auto) });

            // Create dock areas
            CreateDockArea(DockPosition.Left, 0, 1);
            CreateDockArea(DockPosition.Right, 2, 1);
            CreateDockArea(DockPosition.Top, 1, 0);
            CreateDockArea(DockPosition.Bottom, 1, 2);

            // Center content area
            var centerGrid = new Grid { Name = "CenterArea" };
            Grid.SetColumn(centerGrid, 1);
            Grid.SetRow(centerGrid, 1);
            _dockAreas[DockPosition.Center] = centerGrid;
            Children.Add(centerGrid);
        }

        private void CreateDockArea(DockPosition position, int column, int row)
        {
            var area = new Grid
            {
                Background = (Brush)Application.Current.Resources["SecondaryBackgroundBrush"],
                Visibility = Visibility.Collapsed
            };

            Grid.SetColumn(area, column);
            Grid.SetRow(area, row);

            // Create splitter
            GridSplitter splitter;
            if (position == DockPosition.Left || position == DockPosition.Right)
            {
                splitter = new GridSplitter
                {
                    Width = 3,
                    HorizontalAlignment = position == DockPosition.Left ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Background = (Brush)Application.Current.Resources["BorderBrush"],
                    Visibility = Visibility.Collapsed
                };
                Grid.SetColumn(splitter, column);
                Grid.SetRow(splitter, row);
            }
            else
            {
                splitter = new GridSplitter
                {
                    Height = 3,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = position == DockPosition.Top ? VerticalAlignment.Bottom : VerticalAlignment.Top,
                    Background = (Brush)Application.Current.Resources["BorderBrush"],
                    Visibility = Visibility.Collapsed
                };
                Grid.SetColumn(splitter, column);
                Grid.SetRow(splitter, row);
            }

            _dockAreas[position] = area;
            _splitters[position] = splitter;

            Children.Add(area);
            Children.Add(splitter);
        }

        private static void OnCenterContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DockManager manager)
            {
                var centerGrid = manager._dockAreas[DockPosition.Center];
                centerGrid.Children.Clear();
                if (e.NewValue is UIElement element)
                {
                    centerGrid.Children.Add(element);
                }
            }
        }

        /// <summary>
        /// Registers a panel with the dock manager.
        /// </summary>
        public void RegisterPanel(DockablePanel panel)
        {
            if (_panels.Contains(panel))
                return;

            _panels.Add(panel);
            panel.Closed += Panel_Closed;
            panel.Docked += Panel_Docked;

            // Add to appropriate dock area
            DockPanel(panel, panel.DockPosition);
        }

        /// <summary>
        /// Docks a panel to a specific position.
        /// </summary>
        public void DockPanel(DockablePanel panel, DockPosition position)
        {
            // Remove from current position
            foreach (var area in _dockAreas.Values)
            {
                if (area.Children.Contains(panel))
                {
                    area.Children.Remove(panel);
                    break;
                }
            }

            panel.DockPosition = position;

            if (position == DockPosition.Center)
            {
                // Don't add dockable panels to center
                return;
            }

            var targetArea = _dockAreas[position];
            
            // Update column/row definition for size
            UpdateDockAreaSize(position);
            
            targetArea.Children.Add(panel);
            targetArea.Visibility = Visibility.Visible;
            _splitters[position].Visibility = Visibility.Visible;
        }

        private void UpdateDockAreaSize(DockPosition position)
        {
            switch (position)
            {
                case DockPosition.Left:
                    ColumnDefinitions[0].Width = new GridLength(200);
                    break;
                case DockPosition.Right:
                    ColumnDefinitions[2].Width = new GridLength(200);
                    break;
                case DockPosition.Top:
                    RowDefinitions[0].Height = new GridLength(150);
                    break;
                case DockPosition.Bottom:
                    RowDefinitions[2].Height = new GridLength(150);
                    break;
            }
        }

        /// <summary>
        /// Removes a panel from the dock manager.
        /// </summary>
        public void RemovePanel(DockablePanel panel)
        {
            foreach (var area in _dockAreas.Values)
            {
                if (area.Children.Contains(panel))
                {
                    area.Children.Remove(panel);
                    CheckDockAreaVisibility(area);
                    break;
                }
            }

            _panels.Remove(panel);
            panel.Closed -= Panel_Closed;
            panel.Docked -= Panel_Docked;
        }

        private void CheckDockAreaVisibility(Grid area)
        {
            if (area.Children.Count == 0)
            {
                area.Visibility = Visibility.Collapsed;
                var position = _dockAreas.FirstOrDefault(x => x.Value == area).Key;
                if (_splitters.ContainsKey(position))
                {
                    _splitters[position].Visibility = Visibility.Collapsed;
                }

                // Reset size
                switch (position)
                {
                    case DockPosition.Left:
                        ColumnDefinitions[0].Width = new GridLength(0, GridUnitType.Auto);
                        break;
                    case DockPosition.Right:
                        ColumnDefinitions[2].Width = new GridLength(0, GridUnitType.Auto);
                        break;
                    case DockPosition.Top:
                        RowDefinitions[0].Height = new GridLength(0, GridUnitType.Auto);
                        break;
                    case DockPosition.Bottom:
                        RowDefinitions[2].Height = new GridLength(0, GridUnitType.Auto);
                        break;
                }
            }
        }

        private void Panel_Closed(object? sender, EventArgs e)
        {
            if (sender is DockablePanel panel)
            {
                RemovePanel(panel);
            }
        }

        private void Panel_Docked(object? sender, EventArgs e)
        {
            if (sender is DockablePanel panel)
            {
                DockPanel(panel, panel.DockPosition);
            }
        }

        /// <summary>
        /// Gets all registered panels.
        /// </summary>
        public IReadOnlyList<DockablePanel> Panels => _panels.AsReadOnly();
    }

    /// <summary>
    /// Adorner for showing dock preview during drag.
    /// </summary>
    internal class DockPreviewAdorner : System.Windows.Documents.Adorner
    {
        private DockPosition _previewPosition;
        private Brush _previewBrush;

        public DockPreviewAdorner(UIElement adornedElement) : base(adornedElement)
        {
            _previewBrush = new SolidColorBrush(Color.FromArgb(100, 0, 122, 204));
            IsHitTestVisible = false;
        }

        public void ShowPreview(DockPosition position)
        {
            _previewPosition = position;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var adornedRect = new Rect(AdornedElement.RenderSize);
            Rect previewRect;

            switch (_previewPosition)
            {
                case DockPosition.Left:
                    previewRect = new Rect(0, 0, adornedRect.Width / 4, adornedRect.Height);
                    break;
                case DockPosition.Right:
                    previewRect = new Rect(adornedRect.Width * 3 / 4, 0, adornedRect.Width / 4, adornedRect.Height);
                    break;
                case DockPosition.Top:
                    previewRect = new Rect(0, 0, adornedRect.Width, adornedRect.Height / 4);
                    break;
                case DockPosition.Bottom:
                    previewRect = new Rect(0, adornedRect.Height * 3 / 4, adornedRect.Width, adornedRect.Height / 4);
                    break;
                default:
                    previewRect = adornedRect;
                    break;
            }

            drawingContext.DrawRectangle(_previewBrush, null, previewRect);
        }
    }
}
