using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace FamilyTreeApp.UI.Controls
{
    /// <summary>
    /// A dockable panel that can be docked, floating, or hidden.
    /// </summary>
    public class DockablePanel : ContentControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(DockablePanel), new PropertyMetadata("Panel"));

        public static readonly DependencyProperty DockPositionProperty =
            DependencyProperty.Register(nameof(DockPosition), typeof(DockPosition), typeof(DockablePanel), new PropertyMetadata(DockPosition.Left, OnDockPositionChanged));

        public static readonly DependencyProperty IsFloatingProperty =
            DependencyProperty.Register(nameof(IsFloating), typeof(bool), typeof(DockablePanel), new PropertyMetadata(false, OnIsFloatingChanged));

        public static readonly DependencyProperty IsPinnedProperty =
            DependencyProperty.Register(nameof(IsPinned), typeof(bool), typeof(DockablePanel), new PropertyMetadata(true));

        public static readonly DependencyProperty CanFloatProperty =
            DependencyProperty.Register(nameof(CanFloat), typeof(bool), typeof(DockablePanel), new PropertyMetadata(true));

        public static readonly DependencyProperty CanCloseProperty =
            DependencyProperty.Register(nameof(CanClose), typeof(bool), typeof(DockablePanel), new PropertyMetadata(true));

        private Window? _floatingWindow;
        private DockPreviewWindow? _previewWindow;
        private DispatcherTimer? _locationStableTimer;
        private Point _lastWindowPosition;
        private Point _originalPosition;
        private DockManager? _dockManager;
        private Button? _pinButton;
        private Button? _floatButton;
        private Button? _closeButton;
        private Border? _headerBorder;
        private FrameworkElement? _originalParent;
        private int _originalIndex;
        private int _originalGridColumn;
        private int _originalGridRow;
        private Point _dragStartPoint;
        private bool _isDragging;
        private DockPosition? _pendingDockPosition;
        private const double DragThreshold = 10;
        private const double DockZoneSize = 80;

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public DockPosition DockPosition
        {
            get => (DockPosition)GetValue(DockPositionProperty);
            set => SetValue(DockPositionProperty, value);
        }

        public bool IsFloating
        {
            get => (bool)GetValue(IsFloatingProperty);
            set => SetValue(IsFloatingProperty, value);
        }

        public bool IsPinned
        {
            get => (bool)GetValue(IsPinnedProperty);
            set => SetValue(IsPinnedProperty, value);
        }

        public bool CanFloat
        {
            get => (bool)GetValue(CanFloatProperty);
            set => SetValue(CanFloatProperty, value);
        }

        public bool CanClose
        {
            get => (bool)GetValue(CanCloseProperty);
            set => SetValue(CanCloseProperty, value);
        }

        public event EventHandler? Closed;
        public event EventHandler? Docked;
        public event EventHandler? Undocked;
        public event EventHandler<DockPosition>? DockPositionChanged;

        private static void OnDockPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DockablePanel panel && e.NewValue is DockPosition newPos)
            {
                panel.DockPositionChanged?.Invoke(panel, newPos);
            }
        }

        static DockablePanel()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DockablePanel), 
                new FrameworkPropertyMetadata(typeof(DockablePanel)));
        }

        public DockablePanel()
        {
            Loaded += DockablePanel_Loaded;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Get template parts
            _pinButton = GetTemplateChild("PART_PinButton") as Button;
            _floatButton = GetTemplateChild("PART_FloatButton") as Button;
            _closeButton = GetTemplateChild("PART_CloseButton") as Button;
            _headerBorder = GetTemplateChild("PART_Header") as Border;

            // Wire up header drag events for docked state
            if (_headerBorder != null)
            {
                _headerBorder.MouseLeftButtonDown += Header_MouseLeftButtonDown;
                _headerBorder.MouseMove += Header_MouseMove;
                _headerBorder.MouseLeftButtonUp += Header_MouseLeftButtonUp;
                _headerBorder.Cursor = Cursors.SizeAll;
            }

            // Wire up button events
            if (_pinButton != null)
            {
                _pinButton.Click += (s, e) =>
                {
                    IsPinned = !IsPinned;
                    _pinButton.Content = IsPinned ? "📌" : "📍";
                };
            }

            if (_floatButton != null)
            {
                _floatButton.Click += (s, e) =>
                {
                    if (IsFloating)
                        Dock();
                    else
                        Float();
                };
            }

            if (_closeButton != null)
            {
                _closeButton.Click += (s, e) => Close();
                _closeButton.Visibility = CanClose ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        #region Header Drag Events

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!CanFloat)
                return;

            _dragStartPoint = e.GetPosition(Application.Current.MainWindow);
            _isDragging = false;
            
            if (IsFloating && _floatingWindow != null)
            {
                // Start dragging the floating window
                SetupFloatingWindowDockDetection();
                _floatingWindow.DragMove();
                CheckDockAfterDrag();
                e.Handled = true;
            }
            else
            {
                _headerBorder?.CaptureMouse();
            }
        }

        private void Header_MouseMove(object sender, MouseEventArgs e)
        {
            if (!CanFloat || IsFloating || e.LeftButton != MouseButtonState.Pressed || _headerBorder == null)
                return;

            Point currentPos = e.GetPosition(Application.Current.MainWindow);
            Vector diff = currentPos - _dragStartPoint;

            // Check if we've dragged beyond threshold to start floating
            if (!_isDragging && (Math.Abs(diff.X) > DragThreshold || Math.Abs(diff.Y) > DragThreshold))
            {
                _isDragging = true;
                _headerBorder.ReleaseMouseCapture();
                
                // Float the panel and position it at mouse location
                FloatAtPosition(currentPos);
            }
        }

        private void Header_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            _headerBorder?.ReleaseMouseCapture();
        }

        private void FloatAtPosition(Point screenPos)
        {
            if (_floatingWindow != null || !CanFloat)
                return;

            // Store original parent and position info
            _originalParent = Parent as FrameworkElement;
            _originalGridColumn = Grid.GetColumn(this);
            _originalGridRow = Grid.GetRow(this);
            
            if (_originalParent is Panel parentPanel)
            {
                _originalIndex = parentPanel.Children.IndexOf(this);
                parentPanel.Children.Remove(this);
            }

            // Create floating window
            _floatingWindow = new Window
            {
                Title = Title,
                Width = ActualWidth > 0 ? ActualWidth + 20 : 270,
                Height = ActualHeight > 0 ? ActualHeight + 20 : 320,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                ResizeMode = ResizeMode.CanResizeWithGrip,
                ShowInTaskbar = false,
                Owner = Application.Current.MainWindow,
                Content = this,
                Background = (Brush)Application.Current.Resources["PrimaryBackgroundBrush"]
            };

            // Position at drag location
            if (Application.Current.MainWindow != null)
            {
                var mainWindow = Application.Current.MainWindow;
                _floatingWindow.Left = mainWindow.Left + screenPos.X - 50;
                _floatingWindow.Top = mainWindow.Top + screenPos.Y - 10;
            }

            _floatingWindow.Closed += FloatingWindow_Closed;
            _floatingWindow.Show();
            
            // Mark as floating before drag
            IsFloating = true;
            if (_floatButton != null)
                _floatButton.Content = "🗖";
            
            // Start dragging the window with dock detection
            SetupFloatingWindowDockDetection();
            _floatingWindow.DragMove();
            
            // After DragMove ends, check if we should dock
            CheckDockAfterDrag();

            Undocked?.Invoke(this, EventArgs.Empty);
        }

        private void CheckDockAfterDrag()
        {
            System.Diagnostics.Debug.WriteLine("CheckDockAfterDrag: Starting");
            if (_floatingWindow == null)
            {
                System.Diagnostics.Debug.WriteLine("CheckDockAfterDrag: _floatingWindow is null");
                return;
            }
            
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null)
            {
                System.Diagnostics.Debug.WriteLine("CheckDockAfterDrag: mainWindow is null");
                return;
            }
            
            var floatingPos = new Point(_floatingWindow.Left, _floatingWindow.Top);
            System.Diagnostics.Debug.WriteLine($"CheckDockAfterDrag: floatingPos=({floatingPos.X}, {floatingPos.Y})");
            System.Diagnostics.Debug.WriteLine($"CheckDockAfterDrag: mainWindow bounds=({mainWindow.Left}, {mainWindow.Top}, {mainWindow.ActualWidth}, {mainWindow.ActualHeight})");
            
            var dockZone = GetDockZone(floatingPos, mainWindow);
            System.Diagnostics.Debug.WriteLine($"CheckDockAfterDrag: dockZone={dockZone}");
            
            _previewWindow?.HidePreview();
            
            if (dockZone.HasValue)
            {
                System.Diagnostics.Debug.WriteLine($"CheckDockAfterDrag: Docking to {dockZone.Value}");
                DockPosition = dockZone.Value;
                Dock();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("CheckDockAfterDrag: No dock zone detected");
            }
        }

        #endregion

        #region Floating Window Dock Detection

        private void SetupFloatingWindowDockDetection()
        {
            if (_floatingWindow == null) return;
            _floatingWindow.LocationChanged += FloatingWindow_LocationChanged;
        }

        private void FloatingWindow_LocationChanged(object? sender, EventArgs e)
        {
            if (_floatingWindow == null) return;

            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null) return;

            var floatingPos = new Point(_floatingWindow.Left, _floatingWindow.Top);
            var dockZone = GetDockZone(floatingPos, mainWindow);

            if (dockZone.HasValue)
            {
                _pendingDockPosition = dockZone.Value;
                ShowDockPreview(dockZone.Value, mainWindow);
            }
            else
            {
                _pendingDockPosition = null;
                _previewWindow?.HidePreview();
            }
        }

        private DockPosition? GetDockZone(Point floatingWindowPos, Window mainWindow)
        {
            var mainRect = new Rect(
                mainWindow.Left,
                mainWindow.Top,
                mainWindow.ActualWidth,
                mainWindow.ActualHeight);

            double distLeft = floatingWindowPos.X - mainRect.Left;
            double distRight = mainRect.Right - floatingWindowPos.X;
            double distTop = floatingWindowPos.Y - mainRect.Top;
            double distBottom = mainRect.Bottom - floatingWindowPos.Y;

            bool inVerticalRange = floatingWindowPos.Y >= mainRect.Top - 50 &&
                                   floatingWindowPos.Y <= mainRect.Bottom + 50;
            bool inHorizontalRange = floatingWindowPos.X >= mainRect.Left - 50 &&
                                     floatingWindowPos.X <= mainRect.Right + 50;

            if (inVerticalRange && distLeft >= -20 && distLeft < DockZoneSize)
                return DockPosition.Left;

            if (inVerticalRange && distRight >= -20 && distRight < DockZoneSize)
                return DockPosition.Right;

            if (inHorizontalRange && distTop >= -20 && distTop < DockZoneSize + 30)
                return DockPosition.Top;

            if (inHorizontalRange && distBottom >= -20 && distBottom < DockZoneSize)
                return DockPosition.Bottom;

            return null;
        }

        private void ShowDockPreview(DockPosition position, Window mainWindow)
        {
            if (_previewWindow == null)
            {
                _previewWindow = new DockPreviewWindow();
            }

            double previewWidth = 200;
            double previewHeight = 150;

            Rect previewBounds;

            switch (position)
            {
                case DockPosition.Left:
                    previewBounds = new Rect(
                        mainWindow.Left + 5,
                        mainWindow.Top + 55,
                        previewWidth,
                        mainWindow.ActualHeight - 85);
                    break;

                case DockPosition.Right:
                    previewBounds = new Rect(
                        mainWindow.Left + mainWindow.ActualWidth - previewWidth - 5,
                        mainWindow.Top + 55,
                        previewWidth,
                        mainWindow.ActualHeight - 85);
                    break;

                case DockPosition.Top:
                    previewBounds = new Rect(
                        mainWindow.Left + 5,
                        mainWindow.Top + 55,
                        mainWindow.ActualWidth - 10,
                        previewHeight);
                    break;

                case DockPosition.Bottom:
                    previewBounds = new Rect(
                        mainWindow.Left + 5,
                        mainWindow.Top + mainWindow.ActualHeight - previewHeight - 30,
                        mainWindow.ActualWidth - 10,
                        previewHeight);
                    break;

                default:
                    return;
            }

            _previewWindow.ShowPreview(previewBounds);
        }

        #endregion

        private void DockablePanel_Loaded(object sender, RoutedEventArgs e)
        {
            // Find the dock manager in the visual tree
            _dockManager = FindAncestor<DockManager>(this);
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T ancestor)
                    return ancestor;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private static void OnIsFloatingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DockablePanel panel)
            {
                if ((bool)e.NewValue)
                {
                    panel.CreateFloatingWindow();
                }
                else
                {
                    panel.CloseFloatingWindow();
                }
            }
        }

        private void CreateFloatingWindow()
        {
            if (_floatingWindow != null || !CanFloat)
                return;

            // Store original position
            _originalPosition = TranslatePoint(new Point(0, 0), Application.Current.MainWindow);

            // Store original parent and position info
            _originalParent = Parent as FrameworkElement;
            _originalGridColumn = Grid.GetColumn(this);
            _originalGridRow = Grid.GetRow(this);
            
            if (_originalParent is Panel parentPanel)
            {
                _originalIndex = parentPanel.Children.IndexOf(this);
                parentPanel.Children.Remove(this);
            }

            // Create floating window
            _floatingWindow = new Window
            {
                Title = Title,
                Width = ActualWidth > 0 ? ActualWidth + 20 : 270,
                Height = ActualHeight > 0 ? ActualHeight + 20 : 320,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                ResizeMode = ResizeMode.CanResizeWithGrip,
                ShowInTaskbar = false,
                Owner = Application.Current.MainWindow,
                Content = this,
                Background = (Brush)Application.Current.Resources["PrimaryBackgroundBrush"]
            };

            // Position near original position
            if (Application.Current.MainWindow != null)
            {
                var mainWindow = Application.Current.MainWindow;
                _floatingWindow.Left = mainWindow.Left + _originalPosition.X;
                _floatingWindow.Top = mainWindow.Top + _originalPosition.Y + 30; // Offset for title bar
            }

            _floatingWindow.Closed += FloatingWindow_Closed;
            _floatingWindow.LocationChanged += FloatingWindow_LocationChanged;
            _floatingWindow.Show();

            // Use a deferred check for dock snapping after location stops changing
            StartLocationStableCheck();

            if (_floatButton != null)
                _floatButton.Content = "🗖"; // Change icon to dock

            Undocked?.Invoke(this, EventArgs.Empty);
        }

        private void StartLocationStableCheck()
        {
            if (_locationStableTimer != null)
            {
                _locationStableTimer.Stop();
            }

            _locationStableTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _locationStableTimer.Tick += LocationStableTimer_Tick;
            _locationStableTimer.Start();

            if (_floatingWindow != null)
            {
                _lastWindowPosition = new Point(_floatingWindow.Left, _floatingWindow.Top);
            }
        }

        private void LocationStableTimer_Tick(object? sender, EventArgs e)
        {
            if (_floatingWindow == null)
            {
                StopLocationStableCheck();
                return;
            }

            var currentPos = new Point(_floatingWindow.Left, _floatingWindow.Top);

            // Check if the window has stopped moving
            if (Math.Abs(currentPos.X - _lastWindowPosition.X) < 2 &&
                Math.Abs(currentPos.Y - _lastWindowPosition.Y) < 2)
            {
                // Window has stopped - check if we should dock
                if (_pendingDockPosition.HasValue)
                {
                    var dockPos = _pendingDockPosition.Value;
                    _pendingDockPosition = null;
                    StopLocationStableCheck();
                    DockPosition = dockPos;
                    Dock();
                    return;
                }
            }

            _lastWindowPosition = currentPos;
        }

        private void StopLocationStableCheck()
        {
            if (_locationStableTimer != null)
            {
                _locationStableTimer.Stop();
                _locationStableTimer.Tick -= LocationStableTimer_Tick;
                _locationStableTimer = null;
            }
            _previewWindow?.HidePreview();
        }

        private void CloseFloatingWindow()
        {
            if (_floatingWindow == null)
                return;

            System.Diagnostics.Debug.WriteLine($"CloseFloatingWindow: DockPosition={DockPosition}");
            System.Diagnostics.Debug.WriteLine($"CloseFloatingWindow: _originalParent type={_originalParent?.GetType().Name}");

            StopLocationStableCheck();
            _previewWindow?.HidePreview();
            _floatingWindow.Closed -= FloatingWindow_Closed;
            _floatingWindow.LocationChanged -= FloatingWindow_LocationChanged;
            _floatingWindow.Content = null;
            _floatingWindow.Close();
            _floatingWindow = null;

            // Reset size constraints for docked mode
            ResetSizeForDocking();

            // Return to original parent with proper dock position
            if (_originalParent is Grid parentGrid)
            {
                System.Diagnostics.Debug.WriteLine($"CloseFloatingWindow: Parent is Grid, DockPosition={DockPosition}");
                
                // Set grid position based on dock position
                // MainWindow Grid layout:
                // Rows: 0=Top dock, 1=Main content, 2=Bottom dock
                // Cols: 0=Left dock, 1=Splitter, 2=Canvas, 3=Splitter, 4=Right dock
                switch (DockPosition)
                {
                    case DockPosition.Left:
                        Grid.SetRow(this, 1);
                        Grid.SetColumn(this, 0);
                        Grid.SetRowSpan(this, 1);
                        Grid.SetColumnSpan(this, 1);
                        break;
                    case DockPosition.Right:
                        Grid.SetRow(this, 1);
                        Grid.SetColumn(this, 4);
                        Grid.SetRowSpan(this, 1);
                        Grid.SetColumnSpan(this, 1);
                        break;
                    case DockPosition.Top:
                        Grid.SetRow(this, 0);
                        Grid.SetColumn(this, 0);
                        Grid.SetRowSpan(this, 1);
                        Grid.SetColumnSpan(this, 5); // Span all columns
                        break;
                    case DockPosition.Bottom:
                        Grid.SetRow(this, 2);
                        Grid.SetColumn(this, 0);
                        Grid.SetRowSpan(this, 1);
                        Grid.SetColumnSpan(this, 5); // Span all columns
                        break;
                }
                
                if (!parentGrid.Children.Contains(this))
                {
                    System.Diagnostics.Debug.WriteLine("CloseFloatingWindow: Adding to parentGrid");
                    parentGrid.Children.Add(this);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("CloseFloatingWindow: Already in parentGrid");
                }
            }
            else if (_originalParent is Panel parentPanel)
            {
                System.Diagnostics.Debug.WriteLine($"CloseFloatingWindow: Parent is Panel");
                if (!parentPanel.Children.Contains(this))
                {
                    if (_originalIndex >= 0 && _originalIndex <= parentPanel.Children.Count)
                        parentPanel.Children.Insert(_originalIndex, this);
                    else
                        parentPanel.Children.Add(this);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"CloseFloatingWindow: _originalParent is null or unknown type");
            }

            // Ensure visibility
            this.Visibility = Visibility.Visible;

            if (_floatButton != null)
                _floatButton.Content = "🗗"; // Change icon to float

            // Activate main window to prevent it from minimizing
            Application.Current.MainWindow?.Activate();

            Docked?.Invoke(this, EventArgs.Empty);
            System.Diagnostics.Debug.WriteLine("CloseFloatingWindow: Done");
        }

        private void ResetSizeForDocking()
        {
            // Set size based on dock position
            switch (DockPosition)
            {
                case DockPosition.Left:
                case DockPosition.Right:
                    // Vertical docking - fixed width, stretch height
                    this.Width = 170;
                    this.Height = double.NaN;
                    this.HorizontalAlignment = HorizontalAlignment.Stretch;
                    this.VerticalAlignment = VerticalAlignment.Stretch;
                    break;
                    
                case DockPosition.Top:
                case DockPosition.Bottom:
                    // Horizontal docking - stretch width, fixed height
                    this.Width = double.NaN;
                    this.Height = 150;
                    this.HorizontalAlignment = HorizontalAlignment.Stretch;
                    this.VerticalAlignment = VerticalAlignment.Stretch;
                    break;
            }
        }

        private void FloatingWindow_Closed(object? sender, EventArgs e)
        {
            _floatingWindow = null;
            IsFloating = false;
            Closed?.Invoke(this, EventArgs.Empty);
        }

        public void Float()
        {
            if (CanFloat && !IsFloating)
            {
                IsFloating = true;
            }
        }

        public void Dock()
        {
            if (IsFloating)
            {
                IsFloating = false;
            }
        }

        public void Close()
        {
            if (CanClose)
            {
                if (IsFloating)
                {
                    _floatingWindow?.Close();
                }
                else
                {
                    if (Parent is Panel parentPanel)
                    {
                        parentPanel.Children.Remove(this);
                    }
                }
                Closed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public enum DockPosition
    {
        Left,
        Right,
        Top,
        Bottom,
        Center
    }
}
