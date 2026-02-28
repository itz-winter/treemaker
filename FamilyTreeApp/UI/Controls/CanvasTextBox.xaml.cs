using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FamilyTreeApp.UI.Controls
{
    /// <summary>
    /// A resizable text box that can be placed on the canvas.
    /// Does not snap to grid/angle/geometry.
    /// Single-click to select, double-click to edit (like Google Slides).
    /// </summary>
    public partial class CanvasTextBox : UserControl
    {
        private bool _isDragging = false;
        private bool _isResizing = false;
        private bool _isEditing = false;
        private Point _dragStart;
        private Point _startPosition;
        private Size _startSize;
        private string _resizeCorner = "";

        public event EventHandler? TextBoxSelected;
        public event EventHandler? TextBoxDeleted;

        #region Dependency Properties

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(CanvasTextBox),
                new PropertyMetadata("Text", OnTextChanged));

        public static readonly DependencyProperty TextFontFamilyProperty =
            DependencyProperty.Register("TextFontFamily", typeof(FontFamily), typeof(CanvasTextBox),
                new PropertyMetadata(new FontFamily("Segoe UI"), OnFontChanged));

        public static readonly DependencyProperty TextFontSizeProperty =
            DependencyProperty.Register("TextFontSize", typeof(double), typeof(CanvasTextBox),
                new PropertyMetadata(14.0, OnFontChanged));

        public static readonly DependencyProperty TextColorProperty =
            DependencyProperty.Register("TextColor", typeof(Color), typeof(CanvasTextBox),
                new PropertyMetadata(Colors.White, OnTextColorChanged));

        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register("IsSelected", typeof(bool), typeof(CanvasTextBox),
                new PropertyMetadata(false, OnIsSelectedChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public FontFamily TextFontFamily
        {
            get => (FontFamily)GetValue(TextFontFamilyProperty);
            set => SetValue(TextFontFamilyProperty, value);
        }

        public double TextFontSize
        {
            get => (double)GetValue(TextFontSizeProperty);
            set => SetValue(TextFontSizeProperty, value);
        }

        public Color TextColor
        {
            get => (Color)GetValue(TextColorProperty);
            set => SetValue(TextColorProperty, value);
        }

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CanvasTextBox textBox)
            {
                textBox.TextContent.Text = (string)e.NewValue;
                textBox.TextDisplay.Text = (string)e.NewValue;
            }
        }

        private static void OnFontChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CanvasTextBox textBox)
            {
                textBox.TextContent.FontFamily = textBox.TextFontFamily;
                textBox.TextContent.FontSize = textBox.TextFontSize;
                textBox.TextDisplay.FontFamily = textBox.TextFontFamily;
                textBox.TextDisplay.FontSize = textBox.TextFontSize;
            }
        }

        private static void OnTextColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CanvasTextBox textBox)
            {
                textBox.TextContent.Foreground = new SolidColorBrush(textBox.TextColor);
                textBox.TextDisplay.Foreground = new SolidColorBrush(textBox.TextColor);
            }
        }

        private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CanvasTextBox textBox)
            {
                var isSelected = (bool)e.NewValue;
                textBox.MainBorder.BorderBrush = isSelected 
                    ? new SolidColorBrush(Color.FromRgb(0, 120, 215)) 
                    : (Brush)textBox.FindResource("BorderBrush");
                textBox.MainBorder.BorderThickness = isSelected 
                    ? new Thickness(2) 
                    : new Thickness(1);
                
                // Show/hide resize handles
                var visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;
                textBox.ResizeTopLeft.Visibility = visibility;
                textBox.ResizeTopRight.Visibility = visibility;
                textBox.ResizeBottomLeft.Visibility = visibility;
                textBox.ResizeBottomRight.Visibility = visibility;
                
                // Exit edit mode when deselected
                if (!isSelected)
                {
                    textBox.ExitEditMode();
                }
            }
        }

        #endregion

        public CanvasTextBox()
        {
            InitializeComponent();

            // Hook up events
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseMove += OnMouseMove;
            MouseDoubleClick += OnMouseDoubleClick;
            
            // Exit edit mode when clicking outside TextContent
            TextContent.LostFocus += (s, e) => ExitEditMode();
            TextContent.KeyDown += OnTextContentKeyDown;
            
            // Resize corner handlers
            ResizeTopLeft.MouseLeftButtonDown += (s, e) => StartResize("TopLeft", e);
            ResizeTopRight.MouseLeftButtonDown += (s, e) => StartResize("TopRight", e);
            ResizeBottomLeft.MouseLeftButtonDown += (s, e) => StartResize("BottomLeft", e);
            ResizeBottomRight.MouseLeftButtonDown += (s, e) => StartResize("BottomRight", e);

            // Context menu
            ContextMenu = CreateContextMenu();

            // Update text when editing
            TextContent.TextChanged += (s, e) => 
            {
                Text = TextContent.Text;
                TextDisplay.Text = TextContent.Text;
            };
        }
        
        private void EnterEditMode()
        {
            _isEditing = true;
            TextDisplay.Visibility = Visibility.Collapsed;
            TextContent.Visibility = Visibility.Visible;
            TextContent.Focus();
            TextContent.SelectAll();
        }
        
        private void ExitEditMode()
        {
            if (!_isEditing) return;
            _isEditing = false;
            TextDisplay.Visibility = Visibility.Visible;
            TextContent.Visibility = Visibility.Collapsed;
            Text = TextContent.Text;
        }
        
        private void OnTextContentKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ExitEditMode();
                e.Handled = true;
            }
        }
        
        private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Double-click enters edit mode
            EnterEditMode();
            e.Handled = true;
        }

        private void StartResize(string corner, MouseButtonEventArgs e)
        {
            _isResizing = true;
            _resizeCorner = corner;
            _dragStart = e.GetPosition(Parent as UIElement);
            _startPosition = new Point(Canvas.GetLeft(this), Canvas.GetTop(this));
            _startSize = new Size(ActualWidth, ActualHeight);
            
            if (double.IsNaN(_startPosition.X)) _startPosition.X = 0;
            if (double.IsNaN(_startPosition.Y)) _startPosition.Y = 0;
            
            CaptureMouse();
            e.Handled = true;
        }
        
        private void UserControl_MouseEnter(object sender, MouseEventArgs e)
        {
            UpdateDeleteButtonVisibility();
        }
        
        private void UserControl_MouseLeave(object sender, MouseEventArgs e)
        {
            DeleteButton.Visibility = Visibility.Collapsed;
        }
        
        private void UpdateDeleteButtonVisibility()
        {
            bool shiftHeld = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            DeleteButton.Visibility = shiftHeld ? Visibility.Visible : Visibility.Collapsed;
        }
        
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            TextBoxDeleted?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private ContextMenu CreateContextMenu()
        {
            var menu = new ContextMenu();

            // Font Family submenu
            var fontFamilyMenu = new MenuItem { Header = "Font" };
            var fonts = new[] { "Segoe UI", "Arial", "Times New Roman", "Courier New", "Comic Sans MS", "Georgia", "Verdana" };
            foreach (var font in fonts)
            {
                var item = new MenuItem { Header = font, Tag = font };
                item.Click += (s, e) =>
                {
                    if (s is MenuItem mi && mi.Tag is string fontName)
                    {
                        TextFontFamily = new FontFamily(fontName);
                    }
                };
                fontFamilyMenu.Items.Add(item);
            }
            menu.Items.Add(fontFamilyMenu);

            // Font Size submenu
            var fontSizeMenu = new MenuItem { Header = "Size" };
            var sizes = new[] { 10, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48 };
            foreach (var size in sizes)
            {
                var item = new MenuItem { Header = size.ToString(), Tag = size };
                item.Click += (s, e) =>
                {
                    if (s is MenuItem mi && mi.Tag is int fontSize)
                    {
                        TextFontSize = fontSize;
                    }
                };
                fontSizeMenu.Items.Add(item);
            }
            menu.Items.Add(fontSizeMenu);

            // Color submenu
            var colorMenu = new MenuItem { Header = "Color" };
            var colors = new (string name, Color color)[]
            {
                ("White", Colors.White),
                ("Black", Colors.Black),
                ("Red", Colors.Red),
                ("Green", Colors.LimeGreen),
                ("Blue", Colors.DodgerBlue),
                ("Yellow", Colors.Yellow),
                ("Orange", Colors.Orange),
                ("Purple", Colors.MediumPurple),
                ("Pink", Colors.HotPink),
                ("Gray", Colors.Gray)
            };
            foreach (var (name, color) in colors)
            {
                var item = new MenuItem 
                { 
                    Header = name, 
                    Tag = color,
                    Icon = new System.Windows.Shapes.Rectangle
                    {
                        Width = 14,
                        Height = 14,
                        Fill = new SolidColorBrush(color),
                        Stroke = Brushes.Gray,
                        StrokeThickness = 1
                    }
                };
                item.Click += (s, e) =>
                {
                    if (s is MenuItem mi && mi.Tag is Color c)
                    {
                        TextColor = c;
                    }
                };
                colorMenu.Items.Add(item);
            }
            menu.Items.Add(colorMenu);

            menu.Items.Add(new Separator());

            // Delete
            var deleteItem = new MenuItem { Header = "Delete", Foreground = Brushes.IndianRed };
            deleteItem.Click += (s, e) => TextBoxDeleted?.Invoke(this, EventArgs.Empty);
            menu.Items.Add(deleteItem);

            return menu;
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // If already editing, don't start dragging
            if (_isEditing)
            {
                return;
            }
            
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
            
            // Update delete button visibility on mouse activity
            UpdateDeleteButtonVisibility();

            IsSelected = true;
            TextBoxSelected?.Invoke(this, EventArgs.Empty);

            _isDragging = true;
            _dragStart = e.GetPosition(Parent as UIElement);
            _startPosition = new Point(Canvas.GetLeft(this), Canvas.GetTop(this));
            
            if (double.IsNaN(_startPosition.X)) _startPosition.X = 0;
            if (double.IsNaN(_startPosition.Y)) _startPosition.Y = 0;

            CaptureMouse();
            e.Handled = true;
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
            }
            if (_isResizing)
            {
                _isResizing = false;
                ReleaseMouseCapture();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            // Update delete button visibility based on Shift key
            UpdateDeleteButtonVisibility();
            
            if (_isDragging)
            {
                var currentPos = e.GetPosition(Parent as UIElement);
                var delta = new Point(currentPos.X - _dragStart.X, currentPos.Y - _dragStart.Y);

                // No snapping for text boxes
                Canvas.SetLeft(this, _startPosition.X + delta.X);
                Canvas.SetTop(this, _startPosition.Y + delta.Y);
            }
            else if (_isResizing)
            {
                var currentPos = e.GetPosition(Parent as UIElement);
                var deltaX = currentPos.X - _dragStart.X;
                var deltaY = currentPos.Y - _dragStart.Y;

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
                if (newWidth >= MinWidth)
                {
                    Width = newWidth;
                    if (_resizeCorner == "TopLeft" || _resizeCorner == "BottomLeft")
                        Canvas.SetLeft(this, newLeft);
                }
                if (newHeight >= MinHeight)
                {
                    Height = newHeight;
                    if (_resizeCorner == "TopLeft" || _resizeCorner == "TopRight")
                        Canvas.SetTop(this, newTop);
                }
            }
        }

        public void Deselect()
        {
            IsSelected = false;
        }
    }
}
