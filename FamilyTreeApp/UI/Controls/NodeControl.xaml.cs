using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FamilyTreeApp.Core;

namespace FamilyTreeApp.UI.Controls
{
    /// <summary>
    /// Interaction logic for NodeControl.xaml
    /// </summary>
    public partial class NodeControl : UserControl
    {
        private bool _isDraggingNode = false;
        private Point _nodeDragStartPoint;
        private Point _originalPosition;
        
        // For resizing
        private bool _isResizing = false;
        private string _resizeCorner = "";
        private Point _resizeStartPoint;
        private Size _startSize;
        private Point _startPosition;
        
        // For tracking which circle is hovered
        private string _hoveredCircle = "";
        
        // Static state shared across all nodes for connection mode
        public static bool IsInConnectionMode { get; set; } = false;
        public static string? ConnectionSourceNodeId { get; set; } = null;
        public static Func<string, bool>? IsConnectedToSource { get; set; } = null;
        
        // Static reference to the family tree for groups access
        public static FamilyTree? CurrentFamilyTree { get; set; } = null;

        public static readonly DependencyProperty NodeProperty =
            DependencyProperty.Register("Node", typeof(Node), typeof(NodeControl),
                new PropertyMetadata(null, OnNodeChanged));

        public Node Node
        {
            get => (Node)GetValue(NodeProperty);
            set => SetValue(NodeProperty, value);
        }

        // Events for node actions
        public event EventHandler<NodeActionEventArgs>? AddParentRequested;
        public event EventHandler<NodeActionEventArgs>? AddChildRequested;
        public event EventHandler<NodeActionEventArgs>? AddPartnerRequested;
        public event EventHandler<NodeDragEventArgs>? NodeDragged;
        public event EventHandler<NodeActionEventArgs>? NodeSelected;
        public event EventHandler<NodeActionEventArgs>? DeleteRequested;
        public event EventHandler<NodeActionEventArgs>? DuplicateRequested;
        public event EventHandler<NodeActionEventArgs>? DisconnectAllRequested;
        public event EventHandler<ConnectionDragEventArgs>? ConnectionDragStarted;
        public event EventHandler<ConnectionDragEventArgs>? ConnectionDragEnded;
        public event EventHandler? IndicatorLinesChanged;
        public event EventHandler? NodeResized;

        public NodeControl()
        {
            InitializeComponent();
            
            // Resize corner handlers
            ResizeTopLeft.MouseLeftButtonDown += (s, e) => StartResize("TopLeft", e);
            ResizeTopRight.MouseLeftButtonDown += (s, e) => StartResize("TopRight", e);
            ResizeBottomLeft.MouseLeftButtonDown += (s, e) => StartResize("BottomLeft", e);
            ResizeBottomRight.MouseLeftButtonDown += (s, e) => StartResize("BottomRight", e);
        }
        
        private void StartResize(string corner, MouseButtonEventArgs e)
        {
            _isResizing = true;
            _resizeCorner = corner;
            _resizeStartPoint = e.GetPosition(Parent as UIElement);
            _startPosition = new Point(Canvas.GetLeft(this), Canvas.GetTop(this));
            _startSize = new Size(ActualWidth, ActualHeight);
            
            if (double.IsNaN(_startPosition.X)) _startPosition.X = 0;
            if (double.IsNaN(_startPosition.Y)) _startPosition.Y = 0;
            
            CaptureMouse();
            e.Handled = true;
        }

        private static void OnNodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NodeControl control && e.NewValue is Node node)
            {
                control.UpdateDisplay();
                node.PropertyChanged += (s, args) => control.UpdateDisplay();
            }
        }

        private void UpdateDisplay()
        {
            if (Node == null) return;

            NameText.Text = Node.Name;
            
            // Apply font settings from tree or app settings
            ApplyFontSettings();
            
            // Update gender, crown, deceased markers
            UpdateGenderIcon();
            UpdateCrownIcon();
            UpdateDeceasedMarker();
            
            // Apply custom size if set
            if (!double.IsNaN(Node.Width) && Node.Width > 0)
                Width = Node.Width;
            if (!double.IsNaN(Node.Height) && Node.Height > 0)
                Height = Node.Height;

            // Update selection visual
            if (Node.IsSelected)
            {
                NodeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204)); // Accent color
                NodeBorder.BorderThickness = new Thickness(3);
                // Show resize handles when selected
                ResizeTopLeft.Visibility = Visibility.Visible;
                ResizeTopRight.Visibility = Visibility.Visible;
                ResizeBottomLeft.Visibility = Visibility.Visible;
                ResizeBottomRight.Visibility = Visibility.Visible;
            }
            else
            {
                // Apply group color if set, otherwise default
                UpdateNodeAppearanceFromGroup();
                NodeBorder.BorderThickness = new Thickness(2);
                // Hide resize handles when not selected
                ResizeTopLeft.Visibility = Visibility.Collapsed;
                ResizeTopRight.Visibility = Visibility.Collapsed;
                ResizeBottomLeft.Visibility = Visibility.Collapsed;
                ResizeBottomRight.Visibility = Visibility.Collapsed;
            }
        }
        
        /// <summary>
        /// Applies font settings from tree or app settings
        /// </summary>
        private void ApplyFontSettings()
        {
            var appSettings = SettingsManager.Load();
            string fontFamily = appSettings.FontFamily ?? "Segoe UI";
            double fontSize = appSettings.FontSize;
            bool fontBold = appSettings.FontBold;
            bool fontItalic = appSettings.FontItalic;
            
            // If tree has specific font settings, use them (non-empty font means tree override)
            if (CurrentFamilyTree != null && !string.IsNullOrEmpty(CurrentFamilyTree.FontFamily))
            {
                fontFamily = CurrentFamilyTree.FontFamily;
                if (CurrentFamilyTree.FontSize > 0)
                {
                    fontSize = CurrentFamilyTree.FontSize;
                }
                fontBold = CurrentFamilyTree.FontBold;
                fontItalic = CurrentFamilyTree.FontItalic;
            }
            
            // Apply to NameText
            try
            {
                NameText.FontFamily = new FontFamily(fontFamily);
                NameText.FontSize = fontSize;
                NameText.FontWeight = fontBold ? FontWeights.Bold : FontWeights.SemiBold;
                NameText.FontStyle = fontItalic ? FontStyles.Italic : FontStyles.Normal;
                
                // Also apply to edit box for consistency
                NameEditBox.FontFamily = new FontFamily(fontFamily);
                NameEditBox.FontSize = fontSize;
                NameEditBox.FontWeight = fontBold ? FontWeights.Bold : FontWeights.SemiBold;
                NameEditBox.FontStyle = fontItalic ? FontStyles.Italic : FontStyles.Normal;
            }
            catch
            {
                // Fallback to default if font not found
                NameText.FontFamily = new FontFamily("Segoe UI");
                NameText.FontSize = 14;
            }
        }

        /// <summary>
        /// Updates circle visibility based on connection mode and whether this is the source node
        /// </summary>
        public void UpdateConnectionModeVisuals()
        {
            if (Node == null) return;

            if (IsInConnectionMode)
            {
                // In connection mode
                if (Node.Id == ConnectionSourceNodeId)
                {
                    // This is the source node - hide all circles
                    HideAllCircles();
                }
                else
                {
                    // This is a potential target - show all circles
                    bool isConnected = IsConnectedToSource?.Invoke(Node.Id) ?? false;
                    ShowAllCircles(isConnected);
                }
            }
            else
            {
                // Not in connection mode - circles shown based on mouse proximity
                HideAllCircles();
            }
        }

        private void ShowAllCircles(bool isAlreadyConnected)
        {
            var color = isAlreadyConnected 
                ? new SolidColorBrush(Color.FromRgb(200, 80, 80))  // Red for already connected
                : new SolidColorBrush(Color.FromRgb(0, 122, 204)); // Blue for available
            
            TopConnectionPoint.Fill = color;
            BottomConnectionPoint.Fill = color;
            LeftConnectionPoint.Fill = color;
            RightConnectionPoint.Fill = color;
            
            TopConnectionPoint.Visibility = Visibility.Visible;
            BottomConnectionPoint.Visibility = Visibility.Visible;
            LeftConnectionPoint.Visibility = Visibility.Visible;
            RightConnectionPoint.Visibility = Visibility.Visible;
        }

        private void HideAllCircles()
        {
            TopConnectionPoint.Visibility = Visibility.Collapsed;
            BottomConnectionPoint.Visibility = Visibility.Collapsed;
            LeftConnectionPoint.Visibility = Visibility.Collapsed;
            RightConnectionPoint.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Shows a single connection circle at the specified direction
        /// </summary>
        public void ShowSingleCircle(string direction, bool isAlreadyConnected = false)
        {
            HideAllCircles();
            var circle = GetCircleForDirection(direction);
            if (circle != null)
            {
                circle.Fill = isAlreadyConnected 
                    ? new SolidColorBrush(Color.FromRgb(200, 80, 80))  // Red
                    : new SolidColorBrush(Color.FromRgb(0, 122, 204)); // Blue
                circle.Visibility = Visibility.Visible;
            }
        }

        private System.Windows.Shapes.Ellipse? GetCircleForDirection(string direction)
        {
            return direction switch
            {
                "top" => TopConnectionPoint,
                "bottom" => BottomConnectionPoint,
                "left" => LeftConnectionPoint,
                "right" => RightConnectionPoint,
                _ => null
            };
        }

        private void ShowAddButtons()
        {
            if (Node?.IsLocked == true) return;

            TopAddButton.Visibility = Visibility.Visible;
            BottomAddButton.Visibility = Visibility.Visible;
            LeftAddButton.Visibility = Visibility.Visible;
            RightAddButton.Visibility = Visibility.Visible;
        }

        private void HideAddButtons()
        {
            TopAddButton.Visibility = Visibility.Collapsed;
            BottomAddButton.Visibility = Visibility.Collapsed;
            LeftAddButton.Visibility = Visibility.Collapsed;
            RightAddButton.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Gets the canvas position of a specific connection circle
        /// </summary>
        public Point GetCirclePosition(string direction)
        {
            if (Node == null) return new Point(0, 0);
            
            const double nodeWidth = 120;
            const double nodeHeight = 60;
            double x = Node.Position.X;
            double y = Node.Position.Y;

            return direction switch
            {
                "top" => new Point(x + nodeWidth / 2, y),
                "bottom" => new Point(x + nodeWidth / 2, y + nodeHeight),
                "left" => new Point(x, y + nodeHeight / 2),
                "right" => new Point(x + nodeWidth, y + nodeHeight / 2),
                _ => new Point(x + nodeWidth / 2, y + nodeHeight / 2)
            };
        }

        private bool _isMouseOver = false;

        private void UserControl_MouseEnter(object sender, MouseEventArgs e)
        {
            _isMouseOver = true;
            
            // Show add buttons only if SHIFT is held
            UpdateAddButtonsVisibility();
            
            NodeBorder.Background = new SolidColorBrush(Color.FromRgb(70, 70, 70)); // Slightly lighter on hover
        }

        private void UserControl_MouseLeave(object sender, MouseEventArgs e)
        {
            // Check if mouse is still within the expanded bounds (including buttons)
            var pos = e.GetPosition(this);
            var bounds = new Rect(-30, -30, ActualWidth + 60, ActualHeight + 60);
            
            if (!bounds.Contains(pos))
            {
                _isMouseOver = false;
                HideAddButtons();
                if (!IsInConnectionMode)
                {
                    HideAllCircles();
                }
                NodeBorder.Background = (SolidColorBrush)FindResource("NodeBackgroundBrush");
            }
        }

        private void UpdateAddButtonsVisibility()
        {
            if (_isMouseOver && (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
            {
                ShowAddButtons();
            }
            else
            {
                HideAddButtons();
            }
        }

        private void UserControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Node == null) return;
            
            // Check if clicking on a resize handle
            if (e.OriginalSource is System.Windows.Shapes.Rectangle rect)
            {
                var name = rect.Name;
                if (name == "ResizeTopLeft" || name == "ResizeTopRight" || 
                    name == "ResizeBottomLeft" || name == "ResizeBottomRight")
                {
                    return; // Let resize handle its own event
                }
            }

            _isDraggingNode = true;
            _nodeDragStartPoint = e.GetPosition(Parent as UIElement);
            _originalPosition = Node.Position;
            CaptureMouse();

            bool isShiftHeld = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            NodeSelected?.Invoke(this, new NodeActionEventArgs(Node, isShiftHeld));
            e.Handled = true;
        }

        private void UserControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isResizing)
            {
                _isResizing = false;
                ReleaseMouseCapture();
                // Store the new size in the Node model
                if (Node != null)
                {
                    Node.Width = Width;
                    Node.Height = Height;
                }
                NodeResized?.Invoke(this, EventArgs.Empty);
            }
            _isDraggingNode = false;
            ReleaseMouseCapture();
        }

        private void UserControl_MouseMove(object sender, MouseEventArgs e)
        {
            // Update add buttons visibility based on SHIFT key state
            UpdateAddButtonsVisibility();
            
            // Handle resizing
            if (_isResizing)
            {
                var currentPos = e.GetPosition(Parent as UIElement);
                var deltaX = currentPos.X - _resizeStartPoint.X;
                var deltaY = currentPos.Y - _resizeStartPoint.Y;

                double newLeft = _startPosition.X;
                double newTop = _startPosition.Y;
                double newWidth = _startSize.Width;
                double newHeight = _startSize.Height;

                switch (_resizeCorner)
                {
                    case "TopLeft":
                        newLeft = _startPosition.X + deltaX;
                        newTop = _startPosition.Y + deltaY;
                        newWidth = _startSize.Width - deltaX;
                        newHeight = _startSize.Height - deltaY;
                        break;
                    case "TopRight":
                        newTop = _startPosition.Y + deltaY;
                        newWidth = _startSize.Width + deltaX;
                        newHeight = _startSize.Height - deltaY;
                        break;
                    case "BottomLeft":
                        newLeft = _startPosition.X + deltaX;
                        newWidth = _startSize.Width - deltaX;
                        newHeight = _startSize.Height + deltaY;
                        break;
                    case "BottomRight":
                        newWidth = _startSize.Width + deltaX;
                        newHeight = _startSize.Height + deltaY;
                        break;
                }

                // Apply minimum size constraints
                double minW = NodeBorder.MinWidth > 0 ? NodeBorder.MinWidth : 100;
                double minH = NodeBorder.MinHeight > 0 ? NodeBorder.MinHeight : 40;
                
                if (newWidth >= minW)
                {
                    Width = newWidth;
                    if (_resizeCorner == "TopLeft" || _resizeCorner == "BottomLeft")
                    {
                        Canvas.SetLeft(this, newLeft);
                        if (Node != null) Node.Position = new Point(newLeft, Node.Position.Y);
                    }
                }
                if (newHeight >= minH)
                {
                    Height = newHeight;
                    if (_resizeCorner == "TopLeft" || _resizeCorner == "TopRight")
                    {
                        Canvas.SetTop(this, newTop);
                        if (Node != null) Node.Position = new Point(Node.Position.X, newTop);
                    }
                }
                return;
            }
            
            if (!_isDraggingNode || Node == null) return;

            var currentPosition = e.GetPosition(Parent as UIElement);
            var delta = currentPosition - _nodeDragStartPoint;

            var newPosition = new Point(
                _originalPosition.X + delta.X,
                _originalPosition.Y + delta.Y);

            NodeDragged?.Invoke(this, new NodeDragEventArgs(Node, newPosition));
        }

        private void AddParentButton_Click(object sender, RoutedEventArgs e)
        {
            if (Node != null && !_isDraggingConnection)
                AddParentRequested?.Invoke(this, new NodeActionEventArgs(Node));
            e.Handled = true;
        }

        private void AddChildButton_Click(object sender, RoutedEventArgs e)
        {
            if (Node != null && !_isDraggingConnection)
                AddChildRequested?.Invoke(this, new NodeActionEventArgs(Node));
            e.Handled = true;
        }

        private void AddPartnerLeftButton_Click(object sender, RoutedEventArgs e)
        {
            if (Node != null && !_isDraggingConnection)
                AddPartnerRequested?.Invoke(this, new NodeActionEventArgs(Node));
            e.Handled = true;
        }

        private void AddPartnerRightButton_Click(object sender, RoutedEventArgs e)
        {
            if (Node != null && !_isDraggingConnection)
                AddPartnerRequested?.Invoke(this, new NodeActionEventArgs(Node));
            e.Handled = true;
        }

        #region Drag-to-Connect

        private bool _isDraggingConnection = false;
        private Point _dragStartPoint;
        private string _dragDirection = "";

        private void TopAddButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            StartConnectionDrag("parent", e);
        }

        private void BottomAddButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            StartConnectionDrag("child", e);
        }

        private void LeftAddButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            StartConnectionDrag("partner", e);
        }

        private void RightAddButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            StartConnectionDrag("partner", e);
        }

        private void StartConnectionDrag(string direction, MouseButtonEventArgs e)
        {
            if (Node == null) return;
            
            _dragStartPoint = e.GetPosition(this);
            _dragDirection = direction;
            // Don't start drag immediately - wait for mouse move
        }

        private void AddButton_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && Node != null && !_isDraggingConnection)
            {
                var currentPos = e.GetPosition(this);
                var diff = currentPos - _dragStartPoint;

                // Start drag only if moved more than threshold
                if (Math.Abs(diff.X) > 5 || Math.Abs(diff.Y) > 5)
                {
                    _isDraggingConnection = true;
                    var startPos = GetConnectionStartPosition();
                    ConnectionDragStarted?.Invoke(this, new ConnectionDragEventArgs(Node, startPos, _dragDirection));
                    (sender as Button)?.CaptureMouse();
                }
            }
        }

        private void AddButton_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingConnection && Node != null)
            {
                var endPos = e.GetPosition(Parent as UIElement);
                ConnectionDragEnded?.Invoke(this, new ConnectionDragEventArgs(Node, endPos, _dragDirection));
                (sender as Button)?.ReleaseMouseCapture();
            }
            _isDraggingConnection = false;
        }

        private Point GetConnectionStartPosition()
        {
            // Return the center of this node control in parent coordinates
            if (Parent is UIElement parent)
            {
                return TranslatePoint(new Point(ActualWidth / 2, ActualHeight / 2), parent);
            }
            return new Point(0, 0);
        }

        #endregion

        #region Context Menu Handlers

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            // Update the deceased menu item text based on current state
            if (Node != null)
            {
                DeceasedMenuItem.Header = Node.IsAlive ? "Mark Deceased" : "Mark Alive";
                
                // Update checkable menu items for line indicators
                ContinuationUpMenuItem.IsChecked = Node.ShowContinuationUp;
                ContinuationDownMenuItem.IsChecked = Node.ShowContinuationDown;
                NoDescendantsMenuItem.IsChecked = Node.ShowNoDescendants;
                AdoptedMenuItem.IsChecked = Node.IsAdopted;
            }

            // Populate groups submenu
            PopulateGroupsMenu();
        }

        private void PopulateGroupsMenu()
        {
            AssignGroupMenuItem.Items.Clear();

            // Add "None" option
            var noneItem = new MenuItem { Header = "(None)" };
            noneItem.Click += (s, e) => { if (Node != null) Node.GroupId = null; };
            if (Node?.GroupId == null)
                noneItem.FontWeight = FontWeights.Bold;
            AssignGroupMenuItem.Items.Add(noneItem);

            // Add separator if there are groups
            if (CurrentFamilyTree?.Groups.Count > 0)
            {
                AssignGroupMenuItem.Items.Add(new Separator());

                // Add each group
                foreach (var group in CurrentFamilyTree.Groups)
                {
                    var groupItem = new MenuItem { Header = group.Name, Tag = group.Id };
                    
                    // Mark current group
                    if (Node?.GroupId == group.Id)
                        groupItem.FontWeight = FontWeights.Bold;

                    // Add color indicator
                    var colorRect = new System.Windows.Shapes.Rectangle
                    {
                        Width = 12,
                        Height = 12,
                        Fill = new SolidColorBrush(group.Color),
                        Margin = new Thickness(0, 0, 5, 0)
                    };
                    groupItem.Icon = colorRect;

                    groupItem.Click += (s, e) =>
                    {
                        if (Node != null && s is MenuItem mi && mi.Tag is string groupId)
                        {
                            Node.GroupId = groupId;
                            UpdateNodeAppearanceFromGroup();
                        }
                    };
                    
                    AssignGroupMenuItem.Items.Add(groupItem);
                }
            }
        }

        private void UpdateNodeAppearanceFromGroup()
        {
            if (Node == null || CurrentFamilyTree == null) return;

            if (!string.IsNullOrEmpty(Node.GroupId))
            {
                var group = CurrentFamilyTree.Groups.FirstOrDefault(g => g.Id == Node.GroupId);
                if (group != null)
                {
                    // Apply group color to node border
                    NodeBorder.BorderBrush = new SolidColorBrush(group.Color);
                }
            }
            else
            {
                // Reset to default
                NodeBorder.BorderBrush = (SolidColorBrush)FindResource("NodeBorderBrush");
            }
        }

        private void UpdateGenderIcon()
        {
            if (Node == null) return;
            
            GenderIconCanvas.Children.Clear();
            
            var settings = SettingsManager.Current;
            var style = settings.GenderIconStyle;
            
            if (Node.Gender == Gender.Unspecified)
            {
                GenderIconCanvas.Visibility = Visibility.Collapsed;
                return;
            }
            
            GenderIconCanvas.Visibility = Visibility.Visible;
            
            // Draw gender icon based on style
            switch (style)
            {
                case "Dots":
                    DrawGenderDotIcon();
                    break;
                case "ColoredCircles":
                    DrawGenderColoredCircle();
                    break;
                case "Symbols":
                default:
                    DrawGenderSymbolIcon();
                    break;
            }
        }
        
        private void DrawGenderDotIcon()
        {
            // Dots style: filled=female, empty=male, shaded=other
            var ellipse = new Ellipse
            {
                Width = 10,
                Height = 10,
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 1.5
            };
            
            switch (Node.Gender)
            {
                case Gender.Female:
                    ellipse.Fill = new SolidColorBrush(Colors.White); // Filled
                    break;
                case Gender.Male:
                    ellipse.Fill = Brushes.Transparent; // Empty
                    break;
                case Gender.Other:
                    // Shaded (half-filled pattern)
                    var brush = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(1, 1)
                    };
                    brush.GradientStops.Add(new GradientStop(Colors.White, 0));
                    brush.GradientStops.Add(new GradientStop(Colors.White, 0.5));
                    brush.GradientStops.Add(new GradientStop(Colors.Transparent, 0.5));
                    brush.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
                    ellipse.Fill = brush;
                    break;
            }
            
            Canvas.SetLeft(ellipse, 2);
            Canvas.SetTop(ellipse, 2);
            GenderIconCanvas.Children.Add(ellipse);
        }
        
        private void DrawGenderColoredCircle()
        {
            // Colored circles: pink=female, light blue=male, yellow=other
            var ellipse = new Ellipse
            {
                Width = 10,
                Height = 10,
                StrokeThickness = 0
            };
            
            switch (Node.Gender)
            {
                case Gender.Female:
                    ellipse.Fill = new SolidColorBrush(Color.FromRgb(255, 182, 193)); // Pink
                    break;
                case Gender.Male:
                    ellipse.Fill = new SolidColorBrush(Color.FromRgb(173, 216, 230)); // Light blue
                    break;
                case Gender.Other:
                    ellipse.Fill = new SolidColorBrush(Color.FromRgb(255, 255, 150)); // Yellow
                    break;
            }
            
            Canvas.SetLeft(ellipse, 2);
            Canvas.SetTop(ellipse, 2);
            GenderIconCanvas.Children.Add(ellipse);
        }
        
        private void DrawGenderSymbolIcon()
        {
            // Traditional symbols drawn as paths
            var path = new Path
            {
                Stroke = Node.Gender switch
                {
                    Gender.Female => new SolidColorBrush(Colors.Pink),
                    Gender.Male => new SolidColorBrush(Colors.LightBlue),
                    Gender.Other => new SolidColorBrush(Colors.Plum),
                    _ => new SolidColorBrush(Colors.Gray)
                },
                StrokeThickness = 1.5,
                Fill = Brushes.Transparent
            };
            
            var geometry = new PathGeometry();
            
            switch (Node.Gender)
            {
                case Gender.Female:
                    // Venus symbol: circle with cross below
                    geometry.AddGeometry(new EllipseGeometry(new Point(7, 5), 4, 4));
                    geometry.AddGeometry(new LineGeometry(new Point(7, 9), new Point(7, 14)));
                    geometry.AddGeometry(new LineGeometry(new Point(5, 12), new Point(9, 12)));
                    break;
                case Gender.Male:
                    // Mars symbol: circle with arrow
                    geometry.AddGeometry(new EllipseGeometry(new Point(6, 8), 4, 4));
                    geometry.AddGeometry(new LineGeometry(new Point(9, 5), new Point(13, 1)));
                    geometry.AddGeometry(new LineGeometry(new Point(10, 1), new Point(13, 1)));
                    geometry.AddGeometry(new LineGeometry(new Point(13, 1), new Point(13, 4)));
                    break;
                case Gender.Other:
                    // Combined/other symbol: circle with cross and arrow
                    geometry.AddGeometry(new EllipseGeometry(new Point(7, 7), 4, 4));
                    geometry.AddGeometry(new LineGeometry(new Point(7, 11), new Point(7, 14)));
                    geometry.AddGeometry(new LineGeometry(new Point(5, 13), new Point(9, 13)));
                    geometry.AddGeometry(new LineGeometry(new Point(10, 4), new Point(13, 1)));
                    break;
            }
            
            path.Data = geometry;
            GenderIconCanvas.Children.Add(path);
        }

        private void UpdateCrownIcon()
        {
            if (Node == null) return;
            
            CrownIconCanvas.Children.Clear();
            
            if (Node.RoyalTitle == RoyalTitle.None)
            {
                CrownIconCanvas.Visibility = Visibility.Collapsed;
                return;
            }
            
            CrownIconCanvas.Visibility = Visibility.Visible;
            
            // Draw crown
            DrawCrownIcon(Node.RoyalTitle);
        }
        
        private void DrawCrownIcon(RoyalTitle title)
        {
            var crownColor = title switch
            {
                RoyalTitle.King or RoyalTitle.Queen => Colors.Gold,
                RoyalTitle.FormerKing or RoyalTitle.FormerQueen => Colors.DarkGoldenrod,
                RoyalTitle.Prince or RoyalTitle.Princess => Colors.Silver,
                RoyalTitle.Heir => Colors.LightGoldenrodYellow,
                _ => Colors.Gold
            };
            
            // Draw crown shape
            var crownPath = new Path
            {
                Stroke = new SolidColorBrush(crownColor),
                StrokeThickness = 1.5,
                Fill = new SolidColorBrush(crownColor)
            };
            
            // Crown points: base with 3 peaks
            var crownGeometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = new Point(2, 14), IsClosed = true };
            figure.Segments.Add(new LineSegment(new Point(2, 8), true));
            figure.Segments.Add(new LineSegment(new Point(5, 10), true));
            figure.Segments.Add(new LineSegment(new Point(10, 4), true));  // Center peak
            figure.Segments.Add(new LineSegment(new Point(15, 10), true));
            figure.Segments.Add(new LineSegment(new Point(18, 8), true));
            figure.Segments.Add(new LineSegment(new Point(18, 14), true));
            crownGeometry.Figures.Add(figure);
            crownPath.Data = crownGeometry;
            CrownIconCanvas.Children.Add(crownPath);
            
            // For former king/queen, add diagonal slash
            if (title == RoyalTitle.FormerKing || title == RoyalTitle.FormerQueen)
            {
                var slashLine = new Line
                {
                    X1 = 16, Y1 = 2,
                    X2 = 4, Y2 = 14,
                    Stroke = new SolidColorBrush(Colors.Red),
                    StrokeThickness = 2
                };
                CrownIconCanvas.Children.Add(slashLine);
            }
            
            // Add small decorations for different titles
            if (title == RoyalTitle.Heir)
            {
                // Add star on top
                var star = new Polygon
                {
                    Points = new PointCollection(new[] {
                        new Point(10, 0), new Point(11, 3), new Point(14, 3),
                        new Point(12, 5), new Point(13, 8), new Point(10, 6),
                        new Point(7, 8), new Point(8, 5), new Point(6, 3), new Point(9, 3)
                    }),
                    Fill = new SolidColorBrush(Colors.Yellow),
                    Stroke = new SolidColorBrush(Colors.Gold),
                    StrokeThickness = 0.5
                };
                CrownIconCanvas.Children.Add(star);
            }
        }

        private void UpdateDeceasedMarker()
        {
            if (Node == null) return;

            if (!Node.IsAlive)
            {
                DeceasedMarker.Visibility = Visibility.Visible;
                if (Node.DeathDate.HasValue)
                {
                    DeathDateText.Text = Node.DeathDate.Value.ToString("yyyy");
                }
                else
                {
                    DeathDateText.Text = "";
                }
                // Apply strikethrough effect to name
                NameText.TextDecorations = TextDecorations.Strikethrough;
                NameText.Opacity = 0.7;
            }
            else
            {
                DeceasedMarker.Visibility = Visibility.Collapsed;
                NameText.TextDecorations = null;
                NameText.Opacity = 1.0;
            }
        }

        /// <summary>
        /// Updates all visual appearance based on node state (called when node is set)
        /// </summary>
        public void RefreshAppearance()
        {
            UpdateGenderIcon();
            UpdateCrownIcon();
            UpdateDeceasedMarker();
            UpdateNodeAppearanceFromGroup();
        }

        private void EditName_Click(object sender, RoutedEventArgs e)
        {
            StartNameEdit();
        }

        private void MarkDeceased_Click(object sender, RoutedEventArgs e)
        {
            if (Node != null)
            {
                Node.IsAlive = !Node.IsAlive;
                UpdateDeceasedMarker();
            }
        }

        private void Duplicate_Click(object sender, RoutedEventArgs e)
        {
            if (Node != null)
                DuplicateRequested?.Invoke(this, new NodeActionEventArgs(Node));
        }

        private void DisconnectAll_Click(object sender, RoutedEventArgs e)
        {
            if (Node != null)
                DisconnectAllRequested?.Invoke(this, new NodeActionEventArgs(Node));
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (Node != null)
                DeleteRequested?.Invoke(this, new NodeActionEventArgs(Node));
        }

        private void SetGender_Click(object sender, RoutedEventArgs e)
        {
            if (Node != null && sender is MenuItem mi && mi.Tag is string genderStr)
            {
                if (Enum.TryParse<Gender>(genderStr, out var gender))
                {
                    Node.Gender = gender;
                    UpdateGenderIcon();
                }
            }
        }

        private void SetRoyalty_Click(object sender, RoutedEventArgs e)
        {
            if (Node != null && sender is MenuItem mi && mi.Tag is string titleStr)
            {
                if (Enum.TryParse<RoyalTitle>(titleStr, out var title))
                {
                    Node.RoyalTitle = title;
                    Node.IsRoyal = title != RoyalTitle.None;
                    UpdateCrownIcon();
                }
            }
        }

        private void ToggleContinuationUp_Click(object sender, RoutedEventArgs e)
        {
            if (Node != null)
            {
                Node.ShowContinuationUp = !Node.ShowContinuationUp;
                if (sender is MenuItem mi)
                    mi.IsChecked = Node.ShowContinuationUp;
                IndicatorLinesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ToggleContinuationDown_Click(object sender, RoutedEventArgs e)
        {
            if (Node != null)
            {
                Node.ShowContinuationDown = !Node.ShowContinuationDown;
                if (sender is MenuItem mi)
                    mi.IsChecked = Node.ShowContinuationDown;
                IndicatorLinesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ToggleNoDescendants_Click(object sender, RoutedEventArgs e)
        {
            if (Node != null)
            {
                Node.ShowNoDescendants = !Node.ShowNoDescendants;
                if (sender is MenuItem mi)
                    mi.IsChecked = Node.ShowNoDescendants;
                IndicatorLinesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ToggleAdopted_Click(object sender, RoutedEventArgs e)
        {
            if (Node != null)
            {
                Node.IsAdopted = !Node.IsAdopted;
                if (sender is MenuItem mi)
                    mi.IsChecked = Node.IsAdopted;
                IndicatorLinesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        #endregion

        #region Name Editing

        private void NameText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                StartNameEdit();
                e.Handled = true;
            }
        }

        private void StartNameEdit()
        {
            if (Node == null) return;

            NameText.Visibility = Visibility.Collapsed;
            NameEditBox.Text = Node.Name;
            NameEditBox.Visibility = Visibility.Visible;
            NameEditBox.Focus();
            NameEditBox.SelectAll();
        }

        private void EndNameEdit()
        {
            if (Node != null && !string.IsNullOrWhiteSpace(NameEditBox.Text))
            {
                Node.Name = NameEditBox.Text.Trim();
            }

            NameEditBox.Visibility = Visibility.Collapsed;
            NameText.Visibility = Visibility.Visible;
        }

        private void NameEditBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                EndNameEdit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                NameEditBox.Visibility = Visibility.Collapsed;
                NameText.Visibility = Visibility.Visible;
                e.Handled = true;
            }
        }

        private void NameEditBox_LostFocus(object sender, RoutedEventArgs e)
        {
            EndNameEdit();
        }

        #endregion

        #region Connection Points (Blue Circles)

        private void TopConnectionPoint_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Node == null) return;
            System.Diagnostics.Debug.WriteLine($"TopConnectionPoint_MouseDown: Node={Node.Name}");
            _isDraggingConnection = true;
            _dragDirection = "parent";
            ConnectionDragStarted?.Invoke(this, new ConnectionDragEventArgs(Node, new Point(0, 0), "parent"));
            e.Handled = true; // Prevent bubbling to UserControl
        }

        private void BottomConnectionPoint_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Node == null) return;
            System.Diagnostics.Debug.WriteLine($"BottomConnectionPoint_MouseDown: Node={Node.Name}");
            _isDraggingConnection = true;
            _dragDirection = "child";
            ConnectionDragStarted?.Invoke(this, new ConnectionDragEventArgs(Node, new Point(0, 0), "child"));
            e.Handled = true; // Prevent bubbling to UserControl
        }

        private void LeftConnectionPoint_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Node == null) return;
            System.Diagnostics.Debug.WriteLine($"LeftConnectionPoint_MouseDown: Node={Node.Name}");
            _isDraggingConnection = true;
            _dragDirection = "partner-left";
            ConnectionDragStarted?.Invoke(this, new ConnectionDragEventArgs(Node, new Point(0, 0), "partner-left"));
            e.Handled = true; // Prevent bubbling to UserControl
        }

        private void RightConnectionPoint_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Node == null) return;
            System.Diagnostics.Debug.WriteLine($"RightConnectionPoint_MouseDown: Node={Node.Name}");
            _isDraggingConnection = true;
            _dragDirection = "partner-right";
            ConnectionDragStarted?.Invoke(this, new ConnectionDragEventArgs(Node, new Point(0, 0), "partner-right"));
            e.Handled = true; // Prevent bubbling to UserControl
        }

        #endregion
    }

    public class NodeActionEventArgs : EventArgs
    {
        public Node Node { get; }
        public bool IsShiftHeld { get; }

        public NodeActionEventArgs(Node node, bool isShiftHeld = false)
        {
            Node = node;
            IsShiftHeld = isShiftHeld;
        }
    }

    public class NodeDragEventArgs : EventArgs
    {
        public Node Node { get; }
        public Point NewPosition { get; }

        public NodeDragEventArgs(Node node, Point newPosition)
        {
            Node = node;
            NewPosition = newPosition;
        }
    }

    public class ConnectionDragEventArgs : EventArgs
    {
        public Node SourceNode { get; }
        public Point StartPosition { get; }
        public string ConnectionDirection { get; } // "parent", "child", "partner"

        public ConnectionDragEventArgs(Node sourceNode, Point startPosition, string direction)
        {
            SourceNode = sourceNode;
            StartPosition = startPosition;
            ConnectionDirection = direction;
        }
    }
}
