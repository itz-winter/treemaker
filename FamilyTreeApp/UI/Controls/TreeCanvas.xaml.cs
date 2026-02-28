using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using FamilyTreeApp.Core;
using FamilyTreeApp.UI.Windows;

namespace FamilyTreeApp.UI.Controls
{
    /// <summary>
    /// Interaction logic for TreeCanvas.xaml - A zoomable and pannable canvas for the family tree.
    /// </summary>
    public partial class TreeCanvas : UserControl
    {
        private bool _isPanning = false;
        private Point _panStartPoint;
        private Point _panStartOffset;
        private double _currentZoom = 1.0;
        private const double MinZoom = 0.1;
        private const double MaxZoom = 5.0;
        private const double ZoomStep = 0.1;

        private FamilyTree? _familyTree;
        private Dictionary<string, NodeControl> _nodeControls = new();
        private Dictionary<string, Path> _connectionPaths = new();
        private Dictionary<string, Button> _connectionDeleteButtons = new();
        private Dictionary<string, CanvasTextBox> _textBoxControls = new();
        private Node? _selectedNode;
        private ConnectionStyleSettings _connectionStyles = new();
        private List<Node> _selectedNodes = new(); // For multi-selection with Shift+click
        private bool _showGrid = false;
        private Core.CommandManager? _commandManager;

        // Layout engine
        private LayoutEngine _layoutEngine = new();

        // Display settings
        private LineStyle _lineStyle = LineStyle.Curves;
        private LayoutMode _layoutMode = LayoutMode.Fixed;

        // Drag-to-connect state
        private bool _isDraggingConnection = false;
        private Node? _connectionStartNode;
        private Path? _dragPreviewPath;
        private Point _dragStartPoint;
        private Node? _snappedTargetNode;
        private string _snappedCircleDirection = "";
        private ConnectionType _pendingConnectionType = ConnectionType.Biological;

        public event EventHandler<double>? ZoomChanged;
        public event EventHandler<Node>? NodeSelectionChanged;
        
        /// <summary>
        /// Sets the command manager for undo/redo operations.
        /// </summary>
        public void SetCommandManager(Core.CommandManager manager)
        {
            _commandManager = manager;
        }

        public double CurrentZoom => _currentZoom;
        public bool ShowGrid
        {
            get => _showGrid;
            set
            {
                _showGrid = value;
                GridBackground.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public TreeCanvas()
        {
            InitializeComponent();
        }

        public void SetFamilyTree(FamilyTree tree)
        {
            _familyTree = tree;
            NodeControl.CurrentFamilyTree = tree; // Set for group assignment menus
            // Sync the line style from the loaded tree
            _lineStyle = tree.LineStyle;
            // Don't auto-refresh on collection change - we handle it manually
            // _familyTree.Nodes.CollectionChanged += (s, e) => RefreshCanvas();
            // _familyTree.Connections.CollectionChanged += (s, e) => RefreshConnections();
            RefreshCanvas();
        }

        public void SetConnectionStyles(ConnectionStyleSettings styles)
        {
            _connectionStyles = styles;
            RefreshConnections();
        }

        public void RefreshCanvas()
        {
            ClearCanvas();
            if (_familyTree == null) return;

            foreach (var node in _familyTree.Nodes)
            {
                AddNodeControl(node);
            }

            // Add text boxes
            foreach (var textBox in _familyTree.TextBoxes)
            {
                AddTextBoxControl(textBox);
            }

            RefreshConnections();
        }

        /// <summary>
        /// Refreshes all node displays without recreating them (useful for font changes, etc.)
        /// </summary>
        public void RefreshAllNodes()
        {
            foreach (var nodeControl in _nodeControls.Values)
            {
                // Trigger property change to force visual update
                nodeControl.Node.NotifyDisplayRefresh();
            }
        }

        private void ClearCanvas()
        {
            NodesCanvas.Children.Clear();
            ConnectionsCanvas.Children.Clear();
            _nodeControls.Clear();
            _connectionPaths.Clear();
            _connectionDeleteButtons.Clear();
            _textBoxControls.Clear();
        }

        private void AddNodeControl(Node node)
        {
            var nodeControl = new NodeControl { Node = node };

            // Set position
            Canvas.SetLeft(nodeControl, node.Position.X);
            Canvas.SetTop(nodeControl, node.Position.Y);

            // Subscribe to events
            nodeControl.NodeSelected += OnNodeSelected;
            nodeControl.NodeDragged += OnNodeDragged;
            nodeControl.AddParentRequested += OnAddParentRequested;
            nodeControl.AddChildRequested += OnAddChildRequested;
            nodeControl.AddPartnerRequested += OnAddPartnerRequested;
            nodeControl.DeleteRequested += OnDeleteRequested;
            nodeControl.DuplicateRequested += OnDuplicateRequested;
            nodeControl.DisconnectAllRequested += OnDisconnectAllRequested;
            nodeControl.ConnectionDragStarted += OnConnectionDragStarted;
            nodeControl.ConnectionDragEnded += OnConnectionDragEnded;
            nodeControl.IndicatorLinesChanged += OnIndicatorLinesChanged;

            NodesCanvas.Children.Add(nodeControl);
            _nodeControls[node.Id] = nodeControl;
        }
        
        private void OnIndicatorLinesChanged(object? sender, EventArgs e)
        {
            RefreshConnections();
        }
        
        private void RemoveNodeControl(Node node)
        {
            if (_nodeControls.TryGetValue(node.Id, out var control))
            {
                // Unsubscribe from events
                control.NodeSelected -= OnNodeSelected;
                control.NodeDragged -= OnNodeDragged;
                control.AddParentRequested -= OnAddParentRequested;
                control.AddChildRequested -= OnAddChildRequested;
                control.AddPartnerRequested -= OnAddPartnerRequested;
                control.DeleteRequested -= OnDeleteRequested;
                control.DuplicateRequested -= OnDuplicateRequested;
                control.DisconnectAllRequested -= OnDisconnectAllRequested;
                control.ConnectionDragStarted -= OnConnectionDragStarted;
                control.ConnectionDragEnded -= OnConnectionDragEnded;
                control.IndicatorLinesChanged -= OnIndicatorLinesChanged;

                NodesCanvas.Children.Remove(control);
                _nodeControls.Remove(node.Id);
            }
        }

        #region TextBox Support

        private void AddTextBoxControl(CanvasText textData)
        {
            var textBox = new CanvasTextBox
            {
                Text = textData.Text,
                Width = textData.Width,
                Height = textData.Height,
                TextFontFamily = new System.Windows.Media.FontFamily(textData.FontFamily),
                TextFontSize = textData.FontSize,
                TextColor = (Color)ColorConverter.ConvertFromString(textData.TextColor)
            };

            Canvas.SetLeft(textBox, textData.Position.X);
            Canvas.SetTop(textBox, textData.Position.Y);

            // Subscribe to events
            textBox.TextBoxDeleted += (s, e) => DeleteTextBox(textData);
            textBox.MouseLeftButtonUp += (s, e) => SyncTextBoxPosition(textBox, textData);
            textBox.SizeChanged += (s, e) => SyncTextBoxSize(textBox, textData);

            NodesCanvas.Children.Add(textBox);
            _textBoxControls[textData.Id] = textBox;
        }

        private void SyncTextBoxPosition(CanvasTextBox control, CanvasText data)
        {
            data.Position = new Point(Canvas.GetLeft(control), Canvas.GetTop(control));
        }

        private void SyncTextBoxSize(CanvasTextBox control, CanvasText data)
        {
            if (!double.IsNaN(control.Width) && control.Width > 0)
                data.Width = control.Width;
            if (!double.IsNaN(control.Height) && control.Height > 0)
                data.Height = control.Height;
        }

        public void AddTextBox()
        {
            if (_familyTree == null) return;

            var textData = new CanvasText
            {
                Text = "New Text",
                Position = new Point(100 + _familyTree.TextBoxes.Count * 20, 100 + _familyTree.TextBoxes.Count * 20)
            };

            _familyTree.TextBoxes.Add(textData);
            AddTextBoxControl(textData);
        }

        private void DeleteTextBox(CanvasText textData)
        {
            if (_familyTree == null) return;

            if (_textBoxControls.TryGetValue(textData.Id, out var control))
            {
                NodesCanvas.Children.Remove(control);
                _textBoxControls.Remove(textData.Id);
            }

            _familyTree.TextBoxes.Remove(textData);
        }

        #endregion

        public void RefreshConnections()
        {
            ConnectionsCanvas.Children.Clear();
            _connectionPaths.Clear();
            _connectionDeleteButtons.Clear();

            if (_familyTree == null) return;

            // Group connections by type for proper rendering
            var partnerConnections = _familyTree.Connections
                .Where(c => c.ConnectionType == ConnectionType.Partner || c.ConnectionType == ConnectionType.FormerPartner)
                .ToList();
            
            var parentChildConnections = _familyTree.Connections
                .Where(c => c.ConnectionType != ConnectionType.Partner && c.ConnectionType != ConnectionType.FormerPartner)
                .ToList();

            // First, draw partner connections (horizontal lines between partners)
            foreach (var connection in partnerConnections)
            {
                DrawPartnerConnection(connection);
            }

            // Then, draw parent-child connections with combined lines for siblings
            DrawParentChildConnections(parentChildConnections);
            
            // Draw continuation and termination indicator lines
            DrawIndicatorLines();
        }

        /// <summary>
        /// Draws continuation lines (fade-out) and termination lines (X marker) for nodes that have them enabled.
        /// </summary>
        private void DrawIndicatorLines()
        {
            if (_familyTree == null) return;

            foreach (var node in _familyTree.Nodes)
            {
                if (!_nodeControls.TryGetValue(node.Id, out var nodeControl))
                    continue;

                bool isTopDown = _layoutEngine.Alignment == LayoutEngine.AlignmentMode.TopDown;

                // Draw continuation up (ancestors)
                if (node.ShowContinuationUp)
                {
                    DrawContinuationLine(nodeControl, isTopDown ? "top" : "left", isTopDown);
                }

                // Draw continuation down (descendants)
                if (node.ShowContinuationDown)
                {
                    DrawContinuationLine(nodeControl, isTopDown ? "bottom" : "right", isTopDown);
                }

                // Draw no descendants (X marker)
                if (node.ShowNoDescendants)
                {
                    DrawTerminationMarker(nodeControl, isTopDown ? "bottom" : "right", isTopDown);
                }
            }
        }

        /// <summary>
        /// Draws a fade-out line indicating continuation beyond the visible tree.
        /// </summary>
        private void DrawContinuationLine(NodeControl nodeControl, string direction, bool isTopDown)
        {
            var startPoint = nodeControl.GetCirclePosition(direction);
            
            // Calculate end point (fade out over 40 pixels)
            double fadeLength = 40;
            Point endPoint;
            
            switch (direction)
            {
                case "top":
                    endPoint = new Point(startPoint.X, startPoint.Y - fadeLength);
                    break;
                case "bottom":
                    endPoint = new Point(startPoint.X, startPoint.Y + fadeLength);
                    break;
                case "left":
                    endPoint = new Point(startPoint.X - fadeLength, startPoint.Y);
                    break;
                case "right":
                    endPoint = new Point(startPoint.X + fadeLength, startPoint.Y);
                    break;
                default:
                    return;
            }

            // Create a gradient brush for the fade effect
            var gradientBrush = new LinearGradientBrush();
            gradientBrush.StartPoint = new Point(0, 0);
            gradientBrush.EndPoint = direction == "top" || direction == "bottom" 
                ? new Point(0, 1) 
                : new Point(1, 0);
            
            var fadeColor = (Color)ColorConverter.ConvertFromString("#888888");
            if (direction == "top" || direction == "left")
            {
                gradientBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
                gradientBrush.GradientStops.Add(new GradientStop(fadeColor, 1));
            }
            else
            {
                gradientBrush.GradientStops.Add(new GradientStop(fadeColor, 0));
                gradientBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            }

            var line = new Line
            {
                X1 = startPoint.X,
                Y1 = startPoint.Y,
                X2 = endPoint.X,
                Y2 = endPoint.Y,
                Stroke = gradientBrush,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 }
            };

            ConnectionsCanvas.Children.Add(line);
        }

        /// <summary>
        /// Draws a termination marker (small X) indicating no descendants.
        /// </summary>
        private void DrawTerminationMarker(NodeControl nodeControl, string direction, bool isTopDown)
        {
            var startPoint = nodeControl.GetCirclePosition(direction);
            
            // Draw a short line, then an X at the end
            double lineLength = 20;
            double xSize = 6;
            Point lineEnd;
            
            switch (direction)
            {
                case "bottom":
                    lineEnd = new Point(startPoint.X, startPoint.Y + lineLength);
                    break;
                case "right":
                    lineEnd = new Point(startPoint.X + lineLength, startPoint.Y);
                    break;
                default:
                    return;
            }

            // Draw the short line
            var line = new Line
            {
                X1 = startPoint.X,
                Y1 = startPoint.Y,
                X2 = lineEnd.X,
                Y2 = lineEnd.Y,
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888")),
                StrokeThickness = 2
            };
            ConnectionsCanvas.Children.Add(line);

            // Draw X marker
            var xLine1 = new Line
            {
                X1 = lineEnd.X - xSize,
                Y1 = lineEnd.Y - xSize,
                X2 = lineEnd.X + xSize,
                Y2 = lineEnd.Y + xSize,
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CC4444")),
                StrokeThickness = 2
            };
            var xLine2 = new Line
            {
                X1 = lineEnd.X - xSize,
                Y1 = lineEnd.Y + xSize,
                X2 = lineEnd.X + xSize,
                Y2 = lineEnd.Y - xSize,
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CC4444")),
                StrokeThickness = 2
            };
            
            ConnectionsCanvas.Children.Add(xLine1);
            ConnectionsCanvas.Children.Add(xLine2);
        }

        /// <summary>
        /// Draws a partner connection between two nodes (horizontal for TopDown, vertical for LeftRight)
        /// </summary>
        private void DrawPartnerConnection(Connection connection)
        {
            if (!_nodeControls.TryGetValue(connection.FromNodeId, out var fromControl) ||
                !_nodeControls.TryGetValue(connection.ToNodeId, out var toControl))
                return;

            var fromNode = fromControl.Node;
            var toNode = toControl.Node;
            if (fromNode == null || toNode == null) return;

            Point fromPoint, toPoint;
            
            // Check alignment mode from the layout engine
            bool isTopDown = _layoutEngine.Alignment == LayoutEngine.AlignmentMode.TopDown;
            
            if (isTopDown)
            {
                // TopDown: Partner connections are horizontal - use left/right circles
                if (fromNode.Position.X < toNode.Position.X)
                {
                    fromPoint = fromControl.GetCirclePosition("right");
                    toPoint = toControl.GetCirclePosition("left");
                }
                else
                {
                    fromPoint = fromControl.GetCirclePosition("left");
                    toPoint = toControl.GetCirclePosition("right");
                }
            }
            else
            {
                // LeftRight: Partner connections are vertical - use top/bottom circles
                if (fromNode.Position.Y < toNode.Position.Y)
                {
                    fromPoint = fromControl.GetCirclePosition("bottom");
                    toPoint = toControl.GetCirclePosition("top");
                }
                else
                {
                    fromPoint = fromControl.GetCirclePosition("top");
                    toPoint = toControl.GetCirclePosition("bottom");
                }
            }

            // Draw a simple line for partners
            var path = new Path
            {
                Stroke = GetConnectionBrush(connection.ConnectionType),
                StrokeThickness = 2,
                StrokeDashArray = GetConnectionDashArray(connection.ConnectionType)
            };

            var geometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = fromPoint };
            figure.Segments.Add(new LineSegment(toPoint, true));
            geometry.Figures.Add(figure);
            path.Data = geometry;

            // Add context menu
            AddConnectionContextMenu(path, connection);

            ConnectionsCanvas.Children.Add(path);
            _connectionPaths[connection.Id] = path;
        }

        /// <summary>
        /// Draws parent-child connections with combined lines for siblings
        /// </summary>
        private void DrawParentChildConnections(List<Connection> connections)
        {
            // Group children by their parent pair
            // First, find all partner pairs
            var partnerPairs = new Dictionary<string, (Node parent1, Node parent2)>();
            
            if (_familyTree == null) return;

            foreach (var conn in _familyTree.Connections.Where(c => 
                c.ConnectionType == ConnectionType.Partner || c.ConnectionType == ConnectionType.FormerPartner))
            {
                var key = GetPartnerPairKey(conn.FromNodeId, conn.ToNodeId);
                if (!partnerPairs.ContainsKey(key))
                {
                    var p1 = _familyTree.Nodes.FirstOrDefault(n => n.Id == conn.FromNodeId);
                    var p2 = _familyTree.Nodes.FirstOrDefault(n => n.Id == conn.ToNodeId);
                    if (p1 != null && p2 != null)
                        partnerPairs[key] = (p1, p2);
                }
            }

            // Group connections by parent
            var childrenByParent = connections
                .GroupBy(c => c.FromNodeId)
                .ToDictionary(g => g.Key, g => g.Select(c => c.ToNodeId).ToList());

            // For each parent, check if they have a partner and draw combined lines
            var processedChildren = new HashSet<string>();

            foreach (var parentEntry in childrenByParent)
            {
                var parentId = parentEntry.Key;
                var childIds = parentEntry.Value;

                // Find partner for this parent
                string? partnerId = null;
                foreach (var pair in partnerPairs)
                {
                    if (pair.Value.parent1.Id == parentId)
                    {
                        partnerId = pair.Value.parent2.Id;
                        break;
                    }
                    if (pair.Value.parent2.Id == parentId)
                    {
                        partnerId = pair.Value.parent1.Id;
                        break;
                    }
                }

                // Get children that haven't been processed yet
                var unprocessedChildren = childIds.Where(id => !processedChildren.Contains(id)).ToList();
                if (unprocessedChildren.Count == 0) continue;

                // Mark these children as processed
                foreach (var childId in unprocessedChildren)
                    processedChildren.Add(childId);

                // Draw the family connection
                DrawFamilyConnection(parentId, partnerId, unprocessedChildren, connections);
            }
        }

        private string GetPartnerPairKey(string id1, string id2)
        {
            return string.Compare(id1, id2) < 0 ? $"{id1}_{id2}" : $"{id2}_{id1}";
        }

        /// <summary>
        /// Draws connections from parent(s) to children with combined sibling lines
        /// </summary>
        private void DrawFamilyConnection(string parentId, string? partnerId, List<string> childIds, List<Connection> connections)
        {
            if (_familyTree == null) return;
            if (!_nodeControls.TryGetValue(parentId, out var parentControl)) return;
            var parentNode = parentControl.Node;
            if (parentNode == null) return;

            bool isLeftRight = _layoutEngine.Alignment == LayoutEngine.AlignmentMode.LeftRight;

            // Get all child nodes and their controls
            // Sort by position - X for TopDown, Y for LeftRight
            var children = childIds
                .Select(id => (_familyTree.Nodes.FirstOrDefault(n => n.Id == id), _nodeControls.GetValueOrDefault(id)))
                .Where(x => x.Item1 != null && x.Item2 != null)
                .Select(x => (node: x.Item1!, control: x.Item2!))
                .OrderBy(x => isLeftRight ? x.node.Position.Y : x.node.Position.X)
                .ToList();

            if (children.Count == 0) return;

            // Calculate the drop point (where the line from parents extends to children)
            Point dropPoint;
            if (partnerId != null && _nodeControls.TryGetValue(partnerId, out var partnerControl) && partnerControl.Node != null)
            {
                if (isLeftRight)
                {
                    // LeftRight: partner line is vertical (connecting bottom of top parent to top of bottom parent)
                    // We need to find the midpoint of that partner line and extend from there
                    Point p1, p2;
                    if (parentNode.Position.Y < partnerControl.Node.Position.Y)
                    {
                        p1 = parentControl.GetCirclePosition("bottom");
                        p2 = partnerControl.GetCirclePosition("top");
                    }
                    else
                    {
                        p1 = parentControl.GetCirclePosition("top");
                        p2 = partnerControl.GetCirclePosition("bottom");
                    }
                    // Drop point is the midpoint of the vertical partner line
                    dropPoint = new Point((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
                }
                else
                {
                    // TopDown: partner line is horizontal, drop down from center
                    Point p1Right, p2Left;
                    if (parentNode.Position.X < partnerControl.Node.Position.X)
                    {
                        p1Right = parentControl.GetCirclePosition("right");
                        p2Left = partnerControl.GetCirclePosition("left");
                    }
                    else
                    {
                        p1Right = parentControl.GetCirclePosition("left");
                        p2Left = partnerControl.GetCirclePosition("right");
                    }
                    // The drop point is the center of the horizontal partner line
                    dropPoint = new Point((p1Right.X + p2Left.X) / 2, (p1Right.Y + p2Left.Y) / 2);
                }
            }
            else
            {
                // Single parent - drop from right (LeftRight) or bottom (TopDown)
                dropPoint = parentControl.GetCirclePosition(isLeftRight ? "right" : "bottom");
            }

            // Get the connection info for styling
            var firstConnection = connections.FirstOrDefault(c => c.FromNodeId == parentId && childIds.Contains(c.ToNodeId));
            var brush = firstConnection != null ? GetConnectionBrush(firstConnection.ConnectionType) : Brushes.Gray;
            var dashArray = firstConnection != null ? GetConnectionDashArray(firstConnection.ConnectionType) : null;

            if (isLeftRight)
            {
                // LEFT-RIGHT MODE
                // Draw horizontal line from drop point, then vertical sibling bar, then horizontal to children
                // Place sibling bar just 40px to the right of drop point (close to parents)
                double siblingBarX = dropPoint.X + 40;

                // Horizontal line from drop point to sibling bar
                var dropLine = CreateLine(dropPoint, new Point(siblingBarX, dropPoint.Y), brush, dashArray);
                ConnectionsCanvas.Children.Add(dropLine);

                if (children.Count == 1)
                {
                    var child = children[0];
                    var childLeft = child.control.GetCirclePosition("left");
                    
                    Path childLine;
                    if (_lineStyle == LineStyle.Curves)
                    {
                        childLine = CreateCurvedLine(new Point(siblingBarX, dropPoint.Y), childLeft, brush, dashArray, isVertical: false);
                    }
                    else
                    {
                        if (Math.Abs(dropPoint.Y - childLeft.Y) > 5)
                        {
                            var vertLine = CreateLine(new Point(siblingBarX, dropPoint.Y), new Point(siblingBarX, childLeft.Y), brush, dashArray);
                            ConnectionsCanvas.Children.Add(vertLine);
                        }
                        childLine = CreateLine(new Point(siblingBarX, childLeft.Y), childLeft, brush, dashArray);
                    }
                    ConnectionsCanvas.Children.Add(childLine);

                    if (firstConnection != null)
                    {
                        AddConnectionContextMenu(childLine, firstConnection);
                        _connectionPaths[firstConnection.Id] = childLine;
                    }
                }
                else
                {
                    // Multiple children - vertical sibling bar
                    double topY = children.First().control.GetCirclePosition("left").Y;
                    double bottomY = children.Last().control.GetCirclePosition("left").Y;

                    var siblingBar = CreateLine(new Point(siblingBarX, topY), new Point(siblingBarX, bottomY), brush, dashArray);
                    ConnectionsCanvas.Children.Add(siblingBar);

                    // Connect drop point to sibling bar
                    if (dropPoint.Y < topY)
                    {
                        var connectLine = CreateLine(new Point(siblingBarX, dropPoint.Y), new Point(siblingBarX, topY), brush, dashArray);
                        ConnectionsCanvas.Children.Add(connectLine);
                    }
                    else if (dropPoint.Y > bottomY)
                    {
                        var connectLine = CreateLine(new Point(siblingBarX, bottomY), new Point(siblingBarX, dropPoint.Y), brush, dashArray);
                        ConnectionsCanvas.Children.Add(connectLine);
                    }

                    foreach (var child in children)
                    {
                        var childLeft = child.control.GetCirclePosition("left");
                        
                        Path childLine;
                        if (_lineStyle == LineStyle.Curves)
                        {
                            childLine = CreateCurvedLine(new Point(siblingBarX, childLeft.Y), childLeft, brush, dashArray, isVertical: false);
                        }
                        else
                        {
                            childLine = CreateLine(new Point(siblingBarX, childLeft.Y), childLeft, brush, dashArray);
                        }
                        ConnectionsCanvas.Children.Add(childLine);

                        // Find the connection for this child to add context menu
                        var childConnection = connections.FirstOrDefault(c => 
                            c.FromNodeId == parentId && c.ToNodeId == child.node.Id);
                        if (childConnection != null)
                        {
                            AddConnectionContextMenu(childLine, childConnection);
                            _connectionPaths[childConnection.Id] = childLine;
                        }
                    }
                }
            }
            else
            {
                // TOP-DOWN MODE
                // Calculate the Y level for the horizontal sibling bar
                double minChildY = children.Min(c => c.node.Position.Y);
                double siblingBarY = dropPoint.Y + (minChildY - dropPoint.Y) * 0.6;

                // Draw the vertical line from drop point to sibling bar level
                var dropLine = CreateLine(dropPoint, new Point(dropPoint.X, siblingBarY), brush, dashArray);
                ConnectionsCanvas.Children.Add(dropLine);

                if (children.Count == 1)
                {
                    // Single child - draw line from sibling bar level to child
                    var child = children[0];
                    var childTop = child.control.GetCirclePosition("top");
                    
                    Path childLine;
                    if (_lineStyle == LineStyle.Curves)
                    {
                        // Curved line from drop point down to child
                        childLine = CreateCurvedLine(new Point(dropPoint.X, siblingBarY), childTop, brush, dashArray, isVertical: true);
                    }
                    else
                    {
                        // Square: horizontal then vertical
                        if (Math.Abs(dropPoint.X - childTop.X) > 5)
                        {
                            var horizontalLine = CreateLine(new Point(dropPoint.X, siblingBarY), new Point(childTop.X, siblingBarY), brush, dashArray);
                            ConnectionsCanvas.Children.Add(horizontalLine);
                        }
                        childLine = CreateLine(new Point(childTop.X, siblingBarY), childTop, brush, dashArray);
                    }
                    ConnectionsCanvas.Children.Add(childLine);

                    if (firstConnection != null)
                    {
                        AddConnectionContextMenu(childLine, firstConnection);
                        _connectionPaths[firstConnection.Id] = childLine;
                    }
                }
                else
                {
                    // Multiple children - draw horizontal sibling bar
                    double leftX = children.First().control.GetCirclePosition("top").X;
                    double rightX = children.Last().control.GetCirclePosition("top").X;

                    // Horizontal sibling bar
                    var siblingBar = CreateLine(new Point(leftX, siblingBarY), new Point(rightX, siblingBarY), brush, dashArray);
                    ConnectionsCanvas.Children.Add(siblingBar);

                    // Connect the drop point to the sibling bar if needed
                    if (dropPoint.X < leftX)
                    {
                        var connectLine = CreateLine(new Point(dropPoint.X, siblingBarY), new Point(leftX, siblingBarY), brush, dashArray);
                        ConnectionsCanvas.Children.Add(connectLine);
                    }
                    else if (dropPoint.X > rightX)
                    {
                        var connectLine = CreateLine(new Point(rightX, siblingBarY), new Point(dropPoint.X, siblingBarY), brush, dashArray);
                        ConnectionsCanvas.Children.Add(connectLine);
                    }

                    // Vertical lines from sibling bar to each child
                    foreach (var child in children)
                    {
                        var childTop = child.control.GetCirclePosition("top");
                        
                        Path childLine;
                        if (_lineStyle == LineStyle.Curves)
                        {
                            // Curved line from sibling bar to child
                            childLine = CreateCurvedLine(new Point(childTop.X, siblingBarY), childTop, brush, dashArray, isVertical: true);
                        }
                        else
                        {
                            // Straight vertical line
                            childLine = CreateLine(new Point(childTop.X, siblingBarY), childTop, brush, dashArray);
                        }
                        ConnectionsCanvas.Children.Add(childLine);

                        // Find the connection for this child to add context menu
                        var childConnection = connections.FirstOrDefault(c => 
                            c.FromNodeId == parentId && c.ToNodeId == child.node.Id);
                        if (childConnection != null)
                        {
                            AddConnectionContextMenu(childLine, childConnection);
                            _connectionPaths[childConnection.Id] = childLine;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates a straight line segment (for square style)
        /// </summary>
        private Path CreateLine(Point from, Point to, Brush stroke, DoubleCollection? dashArray)
        {
            var path = new Path
            {
                Stroke = stroke,
                StrokeThickness = 2,
                StrokeDashArray = dashArray
            };

            var geometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = from };
            figure.Segments.Add(new LineSegment(to, true));
            geometry.Figures.Add(figure);
            path.Data = geometry;

            return path;
        }

        /// <summary>
        /// Creates a curved bezier line (for curves style)
        /// </summary>
        private Path CreateCurvedLine(Point from, Point to, Brush stroke, DoubleCollection? dashArray, bool isVertical = true)
        {
            var path = new Path
            {
                Stroke = stroke,
                StrokeThickness = 2,
                StrokeDashArray = dashArray
            };

            var geometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = from };

            // Create control points for a smooth bezier curve
            Point cp1, cp2;
            if (isVertical)
            {
                // Vertical curve - control points extend vertically
                double midY = (from.Y + to.Y) / 2;
                cp1 = new Point(from.X, midY);
                cp2 = new Point(to.X, midY);
            }
            else
            {
                // Horizontal curve - control points extend horizontally
                double midX = (from.X + to.X) / 2;
                cp1 = new Point(midX, from.Y);
                cp2 = new Point(midX, to.Y);
            }

            figure.Segments.Add(new BezierSegment(cp1, cp2, to, true));
            geometry.Figures.Add(figure);
            path.Data = geometry;

            return path;
        }

        private void AddConnectionContextMenu(Path path, Connection connection)
        {
            // Create context menu for connection type
            var contextMenu = new ContextMenu();
            
            foreach (ConnectionType connType in Enum.GetValues(typeof(ConnectionType)))
            {
                var menuItem = new MenuItem
                {
                    Header = GetConnectionTypeName(connType),
                    IsChecked = connection.ConnectionType == connType,
                    Tag = connType
                };
                
                menuItem.Click += (s, e) =>
                {
                    if (s is MenuItem mi && mi.Tag is ConnectionType newType)
                    {
                        connection.ConnectionType = newType;
                        RefreshConnections();
                    }
                };
                
                contextMenu.Items.Add(menuItem);
            }
            
            contextMenu.Items.Add(new Separator());
            
            var deleteMenuItem = new MenuItem { Header = "Delete Connection", Foreground = Brushes.IndianRed };
            deleteMenuItem.Click += (s, e) =>
            {
                if (_familyTree != null)
                {
                    _familyTree.RemoveConnection(connection);
                    RefreshConnections();
                }
            };
            contextMenu.Items.Add(deleteMenuItem);

            path.ContextMenu = contextMenu;
            path.Cursor = Cursors.Hand;
            path.ToolTip = $"{GetConnectionTypeName(connection.ConnectionType)}\n(Right-click to change type)";

            // Hover effect
            path.MouseEnter += (s, e) => path.StrokeThickness = 3;
            path.MouseLeave += (s, e) => path.StrokeThickness = 2;
        }

        private void DrawConnection(Connection connection)
        {
            if (!_nodeControls.TryGetValue(connection.FromNodeId, out var fromControl) ||
                !_nodeControls.TryGetValue(connection.ToNodeId, out var toControl))
                return;

            var fromNode = fromControl.Node;
            var toNode = toControl.Node;
            if (fromNode == null || toNode == null) return;

            // Determine best connection points based on relative positions
            var (fromDir, toDir) = GetBestConnectionDirections(fromNode, toNode, connection.ConnectionType);
            
            // Get actual circle positions
            var fromPoint = fromControl.GetCirclePosition(fromDir);
            var toPoint = toControl.GetCirclePosition(toDir);

            // Calculate midpoint for the delete button
            var midPoint = new Point(
                (fromPoint.X + toPoint.X) / 2,
                (fromPoint.Y + toPoint.Y) / 2);

            // Create Bezier curve path
            var path = new Path
            {
                Stroke = GetConnectionBrush(connection.ConnectionType),
                StrokeThickness = 2,
                StrokeDashArray = GetConnectionDashArray(connection.ConnectionType)
            };

            var geometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = fromPoint };

            // Calculate control points for smooth curve based on direction
            var (cp1, cp2) = CalculateControlPoints(fromPoint, toPoint, fromDir, toDir);

            var bezierSegment = new BezierSegment(cp1, cp2, toPoint, true);
            figure.Segments.Add(bezierSegment);

            geometry.Figures.Add(figure);
            path.Data = geometry;

            // Create delete button for connection
            var deleteButton = new Button
            {
                Content = "−",
                Width = 20,
                Height = 20,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromRgb(200, 80, 80)),
                Foreground = Brushes.White,
                BorderBrush = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed,
                ToolTip = "Remove connection"
            };

            // Style the button
            deleteButton.Style = null; // Remove default style
            
            Canvas.SetLeft(deleteButton, midPoint.X - 10);
            Canvas.SetTop(deleteButton, midPoint.Y - 10);

            // Delete button click handler
            deleteButton.Click += (s, e) =>
            {
                if (_familyTree != null)
                {
                    _familyTree.RemoveConnection(connection);
                    RefreshConnections();
                }
                e.Handled = true;
            };

            // Show/hide delete button on path hover
            path.MouseEnter += (s, e) =>
            {
                deleteButton.Visibility = Visibility.Visible;
                path.StrokeThickness = 3;
            };

            path.MouseLeave += (s, e) =>
            {
                // Delay hiding to allow clicking the button
                if (!deleteButton.IsMouseOver)
                {
                    deleteButton.Visibility = Visibility.Collapsed;
                    path.StrokeThickness = connection.IsSelected ? 4 : 2;
                }
            };

            deleteButton.MouseLeave += (s, e) =>
            {
                if (!path.IsMouseOver)
                {
                    deleteButton.Visibility = Visibility.Collapsed;
                    path.StrokeThickness = connection.IsSelected ? 4 : 2;
                }
            };

            // Make connection clickable for selection
            path.MouseLeftButtonDown += (s, e) =>
            {
                connection.IsSelected = !connection.IsSelected;
                path.StrokeThickness = connection.IsSelected ? 4 : 2;
                e.Handled = true;
            };

            // Create context menu for connection type
            var contextMenu = new ContextMenu();
            
            foreach (ConnectionType connType in Enum.GetValues(typeof(ConnectionType)))
            {
                var menuItem = new MenuItem
                {
                    Header = GetConnectionTypeName(connType),
                    IsChecked = connection.ConnectionType == connType,
                    Tag = connType
                };
                
                menuItem.Click += (s, e) =>
                {
                    if (s is MenuItem mi && mi.Tag is ConnectionType newType)
                    {
                        connection.ConnectionType = newType;
                        RefreshConnections();
                    }
                };
                
                contextMenu.Items.Add(menuItem);
            }
            
            contextMenu.Items.Add(new Separator());
            
            var deleteMenuItem = new MenuItem { Header = "Delete Connection", Foreground = Brushes.IndianRed };
            deleteMenuItem.Click += (s, e) =>
            {
                if (_familyTree != null)
                {
                    _familyTree.RemoveConnection(connection);
                    RefreshConnections();
                }
            };
            contextMenu.Items.Add(deleteMenuItem);

            path.ContextMenu = contextMenu;
            path.Cursor = Cursors.Hand;
            path.ToolTip = $"{GetConnectionTypeName(connection.ConnectionType)}\n(Right-click to change type)";

            ConnectionsCanvas.Children.Add(path);
            ConnectionsCanvas.Children.Add(deleteButton);
            _connectionPaths[connection.Id] = path;
            _connectionDeleteButtons[connection.Id] = deleteButton;
        }

        /// <summary>
        /// Determines the best connection circle directions based on relative node positions
        /// </summary>
        private (string fromDir, string toDir) GetBestConnectionDirections(Node fromNode, Node toNode, ConnectionType connectionType)
        {
            // Partner connections should use left/right
            if (connectionType == ConnectionType.Partner || connectionType == ConnectionType.FormerPartner)
            {
                // Determine which node is to the left
                if (fromNode.Position.X < toNode.Position.X)
                    return ("right", "left");
                else
                    return ("left", "right");
            }

            // For parent/child relationships, use top/bottom
            double dx = toNode.Position.X - fromNode.Position.X;
            double dy = toNode.Position.Y - fromNode.Position.Y;

            // If target is mostly below source
            if (dy > Math.Abs(dx))
                return ("bottom", "top");
            
            // If target is mostly above source
            if (dy < -Math.Abs(dx))
                return ("top", "bottom");
            
            // If target is mostly to the right
            if (dx > 0)
                return ("right", "left");
            
            // Target is mostly to the left
            return ("left", "right");
        }

        /// <summary>
        /// Calculates bezier control points based on connection directions
        /// </summary>
        private (Point cp1, Point cp2) CalculateControlPoints(Point from, Point to, string fromDir, string toDir)
        {
            double curveStrength = 50; // How much the curve bends
            
            Point cp1, cp2;
            
            // Calculate offset based on direction
            cp1 = fromDir switch
            {
                "top" => new Point(from.X, from.Y - curveStrength),
                "bottom" => new Point(from.X, from.Y + curveStrength),
                "left" => new Point(from.X - curveStrength, from.Y),
                "right" => new Point(from.X + curveStrength, from.Y),
                _ => from
            };
            
            cp2 = toDir switch
            {
                "top" => new Point(to.X, to.Y - curveStrength),
                "bottom" => new Point(to.X, to.Y + curveStrength),
                "left" => new Point(to.X - curveStrength, to.Y),
                "right" => new Point(to.X + curveStrength, to.Y),
                _ => to
            };
            
            return (cp1, cp2);
        }

        private SolidColorBrush GetConnectionBrush(ConnectionType type)
        {
            var (color, _, _) = _connectionStyles.GetStyleForType(type);
            return new SolidColorBrush(color);
        }

        private double GetConnectionWidth(ConnectionType type)
        {
            var (_, width, _) = _connectionStyles.GetStyleForType(type);
            return width;
        }

        private DoubleCollection? GetConnectionDashArray(ConnectionType type)
        {
            var (_, _, dashStyle) = _connectionStyles.GetStyleForType(type);
            return dashStyle switch
            {
                LineDashStyle.Solid => null,
                LineDashStyle.Dashed => new DoubleCollection { 5, 3 },
                LineDashStyle.Dotted => new DoubleCollection { 2, 2 },
                _ => null
            };
        }

        private string GetConnectionTypeName(ConnectionType type)
        {
            return type switch
            {
                ConnectionType.Biological => "Biological",
                ConnectionType.Adopted => "Adopted",
                ConnectionType.Step => "Step",
                ConnectionType.Partner => "Partner",
                ConnectionType.FormerPartner => "Former Partner",
                ConnectionType.Hidden => "Hidden",
                _ => "Connection"
            };
        }

        private void OnNodeSelected(object? sender, NodeActionEventArgs e)
        {
            if (e.IsShiftHeld)
            {
                // Multi-select mode: toggle this node's selection
                if (_selectedNodes.Contains(e.Node))
                {
                    // Deselect
                    e.Node.IsSelected = false;
                    _selectedNodes.Remove(e.Node);
                    if (_selectedNode == e.Node)
                        _selectedNode = _selectedNodes.FirstOrDefault();
                }
                else
                {
                    // Add to selection
                    e.Node.IsSelected = true;
                    _selectedNodes.Add(e.Node);
                    _selectedNode = e.Node;
                }
            }
            else
            {
                // Single-select mode: clear all previous selections
                foreach (var node in _selectedNodes)
                {
                    node.IsSelected = false;
                }
                _selectedNodes.Clear();

                // Select new node
                _selectedNode = e.Node;
                _selectedNode.IsSelected = true;
                _selectedNodes.Add(e.Node);
            }

            NodeSelectionChanged?.Invoke(this, _selectedNode!);
        }

        /// <summary>
        /// Gets all currently selected nodes
        /// </summary>
        public IReadOnlyList<Node> SelectedNodes => _selectedNodes.AsReadOnly();

        /// <summary>
        /// Clears all node selections
        /// </summary>
        public void ClearSelection()
        {
            foreach (var node in _selectedNodes)
            {
                node.IsSelected = false;
            }
            _selectedNodes.Clear();
            _selectedNode = null;
        }

        private const double NodeWidth = 120;
        private const double NodeHeight = 60;
        private const double MinNodeSpacing = 20;

        private void OnNodeDragged(object? sender, NodeDragEventArgs e)
        {
            // In Fixed mode, don't allow manual dragging
            if (_layoutMode == LayoutMode.Fixed)
            {
                return;
            }

            // Apply snapping to the position
            var snappedPosition = ApplySnapping(e.NewPosition, e.Node);
            
            // Update position with snapping applied
            e.Node.Position = snappedPosition;

            if (_nodeControls.TryGetValue(e.Node.Id, out var control))
            {
                Canvas.SetLeft(control, snappedPosition.X);
                Canvas.SetTop(control, snappedPosition.Y);
            }

            // Refresh connections to update lines
            RefreshConnections();
        }

        /// <summary>
        /// Finds a non-overlapping position for a new node, starting from desired position.
        /// </summary>
        private Point FindNonOverlappingPosition(Node newNode, Point desiredPosition)
        {
            if (_familyTree == null) return desiredPosition;

            var result = desiredPosition;
            int maxIterations = 20;
            int iteration = 0;

            while (iteration < maxIterations)
            {
                bool hasOverlap = false;
                var testRect = new Rect(result.X, result.Y, NodeWidth, NodeHeight);

                foreach (var otherNode in _familyTree.Nodes)
                {
                    if (otherNode.Id == newNode.Id) continue;

                    var otherRect = new Rect(
                        otherNode.Position.X - MinNodeSpacing,
                        otherNode.Position.Y - MinNodeSpacing,
                        NodeWidth + MinNodeSpacing * 2,
                        NodeHeight + MinNodeSpacing * 2);

                    if (testRect.IntersectsWith(otherRect))
                    {
                        hasOverlap = true;
                        // Try shifting right
                        result.X = otherNode.Position.X + NodeWidth + MinNodeSpacing + 20;
                        break;
                    }
                }

                if (!hasOverlap) break;
                iteration++;
            }

            return result;
        }

        private void OnAddParentRequested(object? sender, NodeActionEventArgs e)
        {
            if (_familyTree == null) return;

            var newNode = _familyTree.AddNode("New Parent");
            var desiredPos = new Point(e.Node.Position.X, e.Node.Position.Y - NodeHeight - 60);
            newNode.Position = FindNonOverlappingPosition(newNode, desiredPos);

            var connection = _familyTree.AddConnection(newNode.Id, e.Node.Id, ConnectionType.Biological);

            AddNodeControl(newNode);
            RefreshConnections();
            ApplyAutoLayoutIfFixed();
            
            // Register undo command
            var capturedNode = newNode;
            var capturedConnection = connection;
            _commandManager?.Execute(new Core.ActionCommand(
                () => { }, // Already executed above
                () => { // Undo: remove the node and connection
                    if (capturedConnection != null) _familyTree.Connections.Remove(capturedConnection);
                    _familyTree.Nodes.Remove(capturedNode);
                    RemoveNodeControl(capturedNode);
                    RefreshConnections();
                    ApplyAutoLayoutIfFixed();
                }
            ));
        }

        private void OnAddChildRequested(object? sender, NodeActionEventArgs e)
        {
            if (_familyTree == null) return;

            var newNode = _familyTree.AddNode("New Child");
            var desiredPos = new Point(e.Node.Position.X, e.Node.Position.Y + NodeHeight + 60);
            newNode.Position = FindNonOverlappingPosition(newNode, desiredPos);

            var connection = _familyTree.AddConnection(e.Node.Id, newNode.Id, ConnectionType.Biological);

            AddNodeControl(newNode);
            RefreshConnections();
            ApplyAutoLayoutIfFixed();
            
            // Register undo command
            var capturedNode = newNode;
            var capturedConnection = connection;
            _commandManager?.Execute(new Core.ActionCommand(
                () => { }, // Already executed
                () => {
                    if (capturedConnection != null) _familyTree.Connections.Remove(capturedConnection);
                    _familyTree.Nodes.Remove(capturedNode);
                    RemoveNodeControl(capturedNode);
                    RefreshConnections();
                    ApplyAutoLayoutIfFixed();
                },
                "Add Child"
            ));
        }

        private void OnAddPartnerRequested(object? sender, NodeActionEventArgs e)
        {
            if (_familyTree == null) return;

            var newNode = _familyTree.AddNode("New Partner");
            var desiredPos = new Point(e.Node.Position.X + NodeWidth + 40, e.Node.Position.Y);
            newNode.Position = FindNonOverlappingPosition(newNode, desiredPos);

            var connection = _familyTree.AddConnection(e.Node.Id, newNode.Id, ConnectionType.Partner);

            AddNodeControl(newNode);
            RefreshConnections();
            ApplyAutoLayoutIfFixed();
            
            // Register undo command
            var capturedNode = newNode;
            var capturedConnection = connection;
            _commandManager?.Execute(new Core.ActionCommand(
                () => { }, // Already executed
                () => {
                    if (capturedConnection != null) _familyTree.Connections.Remove(capturedConnection);
                    _familyTree.Nodes.Remove(capturedNode);
                    RemoveNodeControl(capturedNode);
                    RefreshConnections();
                    ApplyAutoLayoutIfFixed();
                },
                "Add Partner"
            ));
        }

        /// <summary>
        /// Applies auto-layout if in Fixed mode
        /// </summary>
        private void ApplyAutoLayoutIfFixed()
        {
            if (_layoutMode == LayoutMode.Fixed)
            {
                ApplyAutoLayout();
            }
        }

        private void OnDeleteRequested(object? sender, NodeActionEventArgs e)
        {
            if (_familyTree == null) return;
            
            // Store data for undo
            var nodeToDelete = e.Node;
            var connectionsToDelete = _familyTree.Connections
                .Where(c => c.FromNodeId == nodeToDelete.Id || c.ToNodeId == nodeToDelete.Id)
                .ToList();

            _familyTree.RemoveNode(e.Node);

            if (_nodeControls.TryGetValue(e.Node.Id, out var control))
            {
                NodesCanvas.Children.Remove(control);
                _nodeControls.Remove(e.Node.Id);
            }

            if (_selectedNode?.Id == e.Node.Id)
                _selectedNode = null;

            RefreshConnections();
            
            // Register undo command
            _commandManager?.Execute(new Core.ActionCommand(
                () => { }, // Already executed
                () => {
                    // Restore node
                    _familyTree.Nodes.Add(nodeToDelete);
                    AddNodeControl(nodeToDelete);
                    // Restore connections
                    foreach (var conn in connectionsToDelete)
                    {
                        _familyTree.Connections.Add(conn);
                    }
                    RefreshConnections();
                    ApplyAutoLayoutIfFixed();
                },
                $"Delete {nodeToDelete.Name}"
            ));
        }

        private void OnDuplicateRequested(object? sender, NodeActionEventArgs e)
        {
            if (_familyTree == null) return;

            var newNode = _familyTree.AddNode(e.Node.Name + " (Copy)");
            newNode.Position = new Point(e.Node.Position.X + 30, e.Node.Position.Y + 30);
            newNode.Gender = e.Node.Gender;
            newNode.IsAlive = e.Node.IsAlive;
            newNode.IsRoyal = e.Node.IsRoyal;
            newNode.RoyalTitle = e.Node.RoyalTitle;
            newNode.GroupId = e.Node.GroupId;

            AddNodeControl(newNode);
            
            // Register undo command
            var capturedNode = newNode;
            _commandManager?.Execute(new Core.ActionCommand(
                () => { },
                () => {
                    _familyTree.Nodes.Remove(capturedNode);
                    RemoveNodeControl(capturedNode);
                },
                $"Duplicate {e.Node.Name}"
            ));
        }

        private void OnDisconnectAllRequested(object? sender, NodeActionEventArgs e)
        {
            if (_familyTree == null) return;

            var connectionsToRemove = _familyTree.GetConnectionsForNode(e.Node.Id).ToList();
            foreach (var connection in connectionsToRemove)
            {
                _familyTree.RemoveConnection(connection);
            }

            RefreshConnections();
        }

        #region Drag-to-Connect

        private string _dragDirection = "";

        private void OnConnectionDragStarted(object? sender, ConnectionDragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[TreeCanvas] OnConnectionDragStarted: node={e.SourceNode.Name}, dir={e.ConnectionDirection}");
            
            _isDraggingConnection = true;
            _connectionStartNode = e.SourceNode;
            _dragDirection = e.ConnectionDirection;
            _snappedTargetNode = null;
            _snappedCircleDirection = "";
            
            // Set connection mode on all nodes
            NodeControl.IsInConnectionMode = true;
            NodeControl.ConnectionSourceNodeId = e.SourceNode.Id;
            NodeControl.IsConnectedToSource = (nodeId) => IsNodeConnectedTo(e.SourceNode.Id, nodeId);
            UpdateAllNodesConnectionMode();

            // Calculate start position based on drag direction
            var startPos = GetConnectionPointForDirection(e.SourceNode, _dragDirection);
            _dragStartPoint = startPos;

            // Create a Path for curved preview line instead of Line
            _dragPreviewPath = new Path
            {
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A90D9")),
                StrokeThickness = 3,
                StrokeDashArray = new DoubleCollection { 6, 3 },
                IsHitTestVisible = false,
                Opacity = 0.8
            };

            NodesCanvas.Children.Add(_dragPreviewPath);

            // Hook up mouse events on the main TreeCanvasElement
            TreeCanvasElement.CaptureMouse();
            TreeCanvasElement.MouseMove += OnConnectionDrag;
            TreeCanvasElement.MouseLeftButtonUp += OnConnectionDragMouseUp;
            TreeCanvasElement.MouseRightButtonDown += OnConnectionDragRightClick;
            
            System.Diagnostics.Debug.WriteLine($"[TreeCanvas] Drag started, mouse captured");
        }

        private bool IsNodeConnectedTo(string nodeId1, string nodeId2)
        {
            if (_familyTree == null) return false;
            return _familyTree.Connections.Any(c =>
                (c.FromNodeId == nodeId1 && c.ToNodeId == nodeId2) ||
                (c.FromNodeId == nodeId2 && c.ToNodeId == nodeId1));
        }

        private void UpdateAllNodesConnectionMode()
        {
            foreach (var nodeControl in _nodeControls.Values)
            {
                nodeControl.UpdateConnectionModeVisuals();
            }
        }

        private Point GetConnectionPointForDirection(Node node, string direction)
        {
            double x = node.Position.X;
            double y = node.Position.Y;
            const double nodeWidth = 120;
            const double nodeHeight = 60;

            return direction switch
            {
                "parent" => new Point(x + nodeWidth / 2, y),               // Top center
                "child" => new Point(x + nodeWidth / 2, y + nodeHeight),   // Bottom center
                "top" => new Point(x + nodeWidth / 2, y),                  // Top center
                "bottom" => new Point(x + nodeWidth / 2, y + nodeHeight),  // Bottom center
                "partner" => new Point(x + nodeWidth, y + nodeHeight / 2), // Right center (default partner)
                "partner-left" => new Point(x, y + nodeHeight / 2),        // Left center
                "partner-right" => new Point(x + nodeWidth, y + nodeHeight / 2), // Right center
                "left" => new Point(x, y + nodeHeight / 2),                // Left center
                "right" => new Point(x + nodeWidth, y + nodeHeight / 2),   // Right center
                _ => new Point(x + nodeWidth / 2, y + nodeHeight / 2)      // Center
            };
        }

        private void OnConnectionDrag(object sender, MouseEventArgs e)
        {
            if (!_isDraggingConnection || _dragPreviewPath == null) return;

            var mousePos = e.GetPosition(NodesCanvas);
            
            // Find nearest circle to snap to (exclude source node)
            var (nearestNode, nearestDir, nearestPos, distance) = FindNearestCircle(mousePos, _connectionStartNode?.Id);
            
            Point endPoint;
            string endDir = "";
            
            if (nearestNode != null && distance < 30)
            {
                // Snap to circle
                endPoint = nearestPos;
                endDir = nearestDir;
                _snappedTargetNode = nearestNode;
                _snappedCircleDirection = nearestDir;
            }
            else
            {
                // Free position
                endPoint = mousePos;
                _snappedTargetNode = null;
                _snappedCircleDirection = "";
            }

            // Update the bezier curve preview
            UpdateDragPreviewCurve(_dragStartPoint, endPoint, _dragDirection, endDir);

            // Highlight node under cursor
            var nodeUnderCursor = FindNodeAtPosition(mousePos);
            HighlightDropTarget(nodeUnderCursor);
        }

        private void UpdateDragPreviewCurve(Point start, Point end, string startDir, string endDir)
        {
            if (_dragPreviewPath == null) return;

            var geometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = start };

            // Calculate control points
            var (cp1, cp2) = CalculateControlPoints(start, end, startDir, endDir.Length > 0 ? endDir : GetOppositeDirection(startDir));

            var bezierSegment = new BezierSegment(cp1, cp2, end, true);
            figure.Segments.Add(bezierSegment);

            geometry.Figures.Add(figure);
            _dragPreviewPath.Data = geometry;
        }

        private string GetOppositeDirection(string dir)
        {
            return dir switch
            {
                "top" or "parent" => "bottom",
                "bottom" or "child" => "top",
                "left" or "partner-left" => "right",
                "right" or "partner-right" or "partner" => "left",
                _ => "top"
            };
        }

        private (Node? node, string direction, Point position, double distance) FindNearestCircle(Point mousePos, string? excludeNodeId = null)
        {
            Node? nearestNode = null;
            string nearestDir = "";
            Point nearestPos = new Point();
            double minDistance = double.MaxValue;

            if (_familyTree == null) 
                return (null, "", new Point(), double.MaxValue);

            foreach (var node in _familyTree.Nodes)
            {
                // Skip excluded node (usually the source node when dragging)
                if (excludeNodeId != null && node.Id == excludeNodeId) continue;

                // Check each circle direction
                foreach (var dir in new[] { "top", "bottom", "left", "right" })
                {
                    var circlePos = GetConnectionPointForDirection(node, dir);
                    var dist = Math.Sqrt(Math.Pow(circlePos.X - mousePos.X, 2) + Math.Pow(circlePos.Y - mousePos.Y, 2));
                    
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        nearestNode = node;
                        nearestDir = dir;
                        nearestPos = circlePos;
                    }
                }
            }

            return (nearestNode, nearestDir, nearestPos, minDistance);
        }

        private void OnConnectionDragMouseUp(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[TreeCanvas] OnConnectionDragMouseUp");
            
            if (!_isDraggingConnection) return;

            // Use snapped target if available
            var targetNode = _snappedTargetNode ?? FindNodeAtPosition(e.GetPosition(NodesCanvas));

            EndConnectionDrag(targetNode, createConnection: true);
            e.Handled = true;
        }

        private void OnConnectionDragRightClick(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[TreeCanvas] OnConnectionDragRightClick - CANCEL");
            
            // Right-click cancels the drag operation
            if (_isDraggingConnection)
            {
                EndConnectionDrag(null, createConnection: false);
                e.Handled = true;
            }
        }

        private void OnConnectionDragEnded(object? sender, ConnectionDragEventArgs e)
        {
            // This is called from NodeControl - we handle via OnConnectionDragMouseUp instead
        }

        private void EndConnectionDrag(Node? targetNode, bool createConnection = true)
        {
            System.Diagnostics.Debug.WriteLine($"[TreeCanvas] EndConnectionDrag: target={targetNode?.Name ?? "null"}, create={createConnection}");
            
            // Clear connection mode on all nodes
            NodeControl.IsInConnectionMode = false;
            NodeControl.ConnectionSourceNodeId = null;
            NodeControl.IsConnectedToSource = null;
            UpdateAllNodesConnectionMode();
            
            // Remove event handlers
            TreeCanvasElement.MouseMove -= OnConnectionDrag;
            TreeCanvasElement.MouseLeftButtonUp -= OnConnectionDragMouseUp;
            TreeCanvasElement.MouseRightButtonDown -= OnConnectionDragRightClick;
            TreeCanvasElement.ReleaseMouseCapture();

            // Remove drag preview path
            if (_dragPreviewPath != null)
            {
                NodesCanvas.Children.Remove(_dragPreviewPath);
                _dragPreviewPath = null;
            }

            // Clear any highlight
            HighlightDropTarget(null);

            // Create or delete connection if we have a valid target and createConnection is true
            if (createConnection && _familyTree != null && _connectionStartNode != null && targetNode != null && 
                _connectionStartNode.Id != targetNode.Id)
            {
                // Check if connection already exists
                var existingConnection = _familyTree.Connections
                    .FirstOrDefault(c => 
                        (c.FromNodeId == _connectionStartNode.Id && c.ToNodeId == targetNode.Id) ||
                        (c.FromNodeId == targetNode.Id && c.ToNodeId == _connectionStartNode.Id));

                if (existingConnection != null)
                {
                    // Connection exists - DELETE it (toggle behavior)
                    _familyTree.RemoveConnection(existingConnection);
                }
                else
                {
                    // No connection exists - CREATE one (no validation for manual connections)
                    var connectionType = DetermineConnectionType(_connectionStartNode, targetNode);
                    
                    // Create the connection directly - manual connections are always allowed
                    _familyTree.AddConnection(_connectionStartNode.Id, targetNode.Id, connectionType);
                }
                RefreshConnections();
            }

            _isDraggingConnection = false;
            _connectionStartNode = null;
            _dragDirection = "";
        }

        private Node? FindNodeAtPosition(Point position)
        {
            if (_familyTree == null) return null;

            foreach (var node in _familyTree.Nodes)
            {
                var rect = new Rect(node.Position.X, node.Position.Y, 120, 60);
                if (rect.Contains(position))
                {
                    return node;
                }
            }

            return null;
        }

        /// <summary>
        /// Determines the connection type based on the drag direction and node positions.
        /// </summary>
        private ConnectionType DetermineConnectionType(Node fromNode, Node toNode)
        {
            // Use the drag direction to determine connection type
            // Left/Right drags = Partner connections
            // Top/Bottom drags = Parent-Child (Biological) connections
            
            if (_dragDirection == "left" || _dragDirection == "right" || 
                _dragDirection == "partner-left" || _dragDirection == "partner-right" ||
                _dragDirection == "partner")
            {
                return ConnectionType.Partner;
            }
            
            // Default to Biological for top/bottom connections
            return ConnectionType.Biological;
        }

        private Node? _highlightedNode = null;

        private void HighlightDropTarget(Node? node)
        {
            // Remove highlight from previous node
            if (_highlightedNode != null && _nodeControls.TryGetValue(_highlightedNode.Id, out var prevControl))
            {
                prevControl.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D2D"));
            }

            _highlightedNode = node;

            // Don't highlight the source node
            if (node != null && node != _connectionStartNode && _nodeControls.TryGetValue(node.Id, out var control))
            {
                // Check if connection exists to determine highlight color
                bool connectionExists = false;
                if (_familyTree != null && _connectionStartNode != null)
                {
                    connectionExists = _familyTree.Connections.Any(c =>
                        (c.FromNodeId == _connectionStartNode.Id && c.ToNodeId == node.Id) ||
                        (c.FromNodeId == node.Id && c.ToNodeId == _connectionStartNode.Id));
                }

                if (connectionExists)
                {
                    // Red tint = will DELETE connection
                    control.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5A3030"));
                }
                else
                {
                    // Green tint = will CREATE connection
                    control.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#305A30"));
                }
            }
        }

        #endregion

        #region Pan and Zoom

        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var mousePos = e.GetPosition(TreeCanvasElement);
            var delta = e.Delta > 0 ? ZoomStep : -ZoomStep;
            var newZoom = Math.Clamp(_currentZoom + delta, MinZoom, MaxZoom);

            if (Math.Abs(newZoom - _currentZoom) > 0.001)
            {
                // Zoom towards mouse position
                var scaleFactor = newZoom / _currentZoom;

                TranslateTransform.X = mousePos.X - scaleFactor * (mousePos.X - TranslateTransform.X);
                TranslateTransform.Y = mousePos.Y - scaleFactor * (mousePos.Y - TranslateTransform.Y);

                _currentZoom = newZoom;
                ScaleTransform.ScaleX = _currentZoom;
                ScaleTransform.ScaleY = _currentZoom;

                ZoomChanged?.Invoke(this, _currentZoom);
            }

            e.Handled = true;
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Only start panning if clicking on empty canvas area
            if (e.OriginalSource == this || e.OriginalSource == MainGrid || 
                e.OriginalSource == TreeCanvasElement || e.OriginalSource == GridBackground)
            {
                _isPanning = true;
                _panStartPoint = e.GetPosition(this);
                _panStartOffset = new Point(TranslateTransform.X, TranslateTransform.Y);
                CaptureMouse();

                // Deselect current node when clicking on empty space
                if (_selectedNode != null)
                {
                    _selectedNode.IsSelected = false;
                    _selectedNode = null;
                }
            }
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isPanning = false;
            ReleaseMouseCapture();
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            // Handle panning
            if (_isPanning)
            {
                var currentPos = e.GetPosition(this);
                var delta = currentPos - _panStartPoint;

                TranslateTransform.X = _panStartOffset.X + delta.X;
                TranslateTransform.Y = _panStartOffset.Y + delta.Y;
                return;
            }

            // If not in connection mode, show circles based on proximity
            if (!_isDraggingConnection && _familyTree != null)
            {
                var mousePos = e.GetPosition(NodesCanvas);
                UpdateProximityCircles(mousePos);
            }
        }

        private string? _lastProximityNodeId = null;
        private string _lastProximityDirection = "";

        private void UpdateProximityCircles(Point mousePos)
        {
            // Find nearest circle
            var (nearestNode, nearestDir, _, distance) = FindNearestCircle(mousePos);

            // Clear previous proximity circle
            if (_lastProximityNodeId != null && _nodeControls.TryGetValue(_lastProximityNodeId, out var prevControl))
            {
                prevControl.UpdateConnectionModeVisuals(); // Will hide since not in connection mode
            }

            if (nearestNode != null && distance < 25)
            {
                // Show only the nearest circle
                if (_nodeControls.TryGetValue(nearestNode.Id, out var control))
                {
                    ShowSingleCircle(control, nearestDir);
                    _lastProximityNodeId = nearestNode.Id;
                    _lastProximityDirection = nearestDir;
                }
            }
            else
            {
                _lastProximityNodeId = null;
                _lastProximityDirection = "";
            }
        }

        private void ShowSingleCircle(NodeControl control, string direction)
        {
            control.ShowSingleCircle(direction, false);
        }

        private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Context menu handling will be added later
        }

        public void ZoomIn()
        {
            var newZoom = Math.Min(_currentZoom + ZoomStep, MaxZoom);
            SetZoom(newZoom);
        }

        public void ZoomOut()
        {
            var newZoom = Math.Max(_currentZoom - ZoomStep, MinZoom);
            SetZoom(newZoom);
        }

        public void SetZoom(double zoom)
        {
            _currentZoom = Math.Clamp(zoom, MinZoom, MaxZoom);
            ScaleTransform.ScaleX = _currentZoom;
            ScaleTransform.ScaleY = _currentZoom;
            ZoomChanged?.Invoke(this, _currentZoom);
        }

        public void ResetView()
        {
            _currentZoom = 1.0;
            ScaleTransform.ScaleX = 1.0;
            ScaleTransform.ScaleY = 1.0;
            TranslateTransform.X = 0;
            TranslateTransform.Y = 0;
            ZoomChanged?.Invoke(this, _currentZoom);
        }

        #endregion

        public void AddNewNodeAtCenter()
        {
            if (_familyTree == null) return;

            // Calculate center of visible area
            var centerX = (ActualWidth / 2 - TranslateTransform.X) / _currentZoom;
            var centerY = (ActualHeight / 2 - TranslateTransform.Y) / _currentZoom;

            var newNode = _familyTree.AddNode("New Person");
            var desiredPos = new Point(centerX - 60, centerY - 30);
            newNode.Position = FindNonOverlappingPosition(newNode, desiredPos);

            AddNodeControl(newNode);
            ApplyAutoLayoutIfFixed();
        }

        public void DeleteSelectedNode()
        {
            if (_selectedNode == null || _familyTree == null) return;

            _familyTree.RemoveNode(_selectedNode);

            if (_nodeControls.TryGetValue(_selectedNode.Id, out var control))
            {
                NodesCanvas.Children.Remove(control);
                _nodeControls.Remove(_selectedNode.Id);
            }

            _selectedNode = null;
            RefreshConnections();
            ApplyAutoLayoutIfFixed();
        }

        public Node? GetSelectedNode() => _selectedNode;

        #region Layout Engine

        public LayoutEngine.AlignmentMode Alignment
        {
            get => _layoutEngine.Alignment;
            set
            {
                _layoutEngine.Alignment = value;
                AlignmentChanged?.Invoke(this, value);
            }
        }

        public event EventHandler<LayoutEngine.AlignmentMode>? AlignmentChanged;

        /// <summary>
        /// Applies automatic layout to the tree.
        /// </summary>
        public void ApplyAutoLayout()
        {
            if (_familyTree == null) return;

            _layoutEngine.ApplyLayout(_familyTree);
            
            // Update node positions on canvas without full rebuild
            foreach (var node in _familyTree.Nodes)
            {
                if (_nodeControls.TryGetValue(node.Id, out var control))
                {
                    Canvas.SetLeft(control, node.Position.X);
                    Canvas.SetTop(control, node.Position.Y);
                }
            }
            
            RefreshConnections();
        }

        /// <summary>
        /// Toggles between Top-Down and Left-Right alignment.
        /// </summary>
        public void ToggleAlignment()
        {
            Alignment = Alignment == LayoutEngine.AlignmentMode.TopDown
                ? LayoutEngine.AlignmentMode.LeftRight
                : LayoutEngine.AlignmentMode.TopDown;

            ApplyAutoLayout();
        }

        /// <summary>
        /// Sets the alignment mode and reflows the tree.
        /// </summary>
        public void SetAlignment(LayoutEngine.AlignmentMode mode)
        {
            if (Alignment != mode)
            {
                Alignment = mode;
                ApplyAutoLayout();
            }
        }

        /// <summary>
        /// Sets the line style (curves or square) and refreshes connections.
        /// </summary>
        public void SetLineStyle(LineStyle style)
        {
            if (_lineStyle != style)
            {
                _lineStyle = style;
                RefreshConnections();
            }
        }

        /// <summary>
        /// Sets the layout mode (fixed or free).
        /// In Fixed mode, nodes are automatically positioned.
        /// In Free mode, users can manually drag nodes.
        /// </summary>
        public void SetLayoutMode(LayoutMode mode)
        {
            if (_layoutMode != mode)
            {
                _layoutMode = mode;
                
                // When switching to Fixed mode, apply auto-layout
                if (mode == LayoutMode.Fixed)
                {
                    ApplyAutoLayout();
                }
            }
        }

        /// <summary>
        /// Gets or sets the current layout mode.
        /// </summary>
        public LayoutMode CurrentLayoutMode => _layoutMode;

        /// <summary>
        /// Gets or sets the current line style.
        /// </summary>
        public LineStyle CurrentLineStyle => _lineStyle;

        #endregion

        #region Snapping Options

        private bool _snapToGrid = false;
        private bool _snapToAngle = false;
        private bool _snapToGeometry = false;
        private int _gridSize = 20;

        /// <summary>
        /// Enables or disables snap to grid.
        /// </summary>
        public void SetSnapToGrid(bool enabled)
        {
            _snapToGrid = enabled;
        }

        /// <summary>
        /// Enables or disables snap to angle (45° increments).
        /// </summary>
        public void SetSnapToAngle(bool enabled)
        {
            _snapToAngle = enabled;
        }

        /// <summary>
        /// Enables or disables snap to existing geometry (other nodes, guidelines).
        /// </summary>
        public void SetSnapToGeometry(bool enabled)
        {
            _snapToGeometry = enabled;
        }

        /// <summary>
        /// Applies snapping to the given position based on current snap settings.
        /// </summary>
        private Point ApplySnapping(Point position, Node? currentNode = null)
        {
            var result = position;

            // Snap to grid
            if (_snapToGrid)
            {
                result.X = Math.Round(result.X / _gridSize) * _gridSize;
                result.Y = Math.Round(result.Y / _gridSize) * _gridSize;
            }

            // Snap to existing geometry (other nodes' X or Y positions)
            if (_snapToGeometry && _familyTree != null)
            {
                const double snapThreshold = 10;
                
                foreach (var node in _familyTree.Nodes)
                {
                    if (currentNode != null && node.Id == currentNode.Id) continue;
                    
                    // Snap X
                    if (Math.Abs(result.X - node.Position.X) < snapThreshold)
                    {
                        result.X = node.Position.X;
                    }
                    
                    // Snap Y
                    if (Math.Abs(result.Y - node.Position.Y) < snapThreshold)
                    {
                        result.Y = node.Position.Y;
                    }
                }
            }

            return result;
        }

        #endregion

        #region Export Methods

        /// <summary>
        /// Exports the canvas to a PNG file by rendering the actual canvas content.
        /// </summary>
        public void ExportToPng(string filePath)
        {
            try
            {
                if (_familyTree == null || _familyTree.Nodes.Count == 0)
                {
                    MessageBox.Show("Nothing to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Calculate bounds of all content
                var bounds = CalculateContentBounds();
                if (bounds.Width <= 0 || bounds.Height <= 0)
                {
                    MessageBox.Show("Nothing to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Add padding
                double padding = 50;
                int width = (int)Math.Ceiling(bounds.Width + padding * 2);
                int height = (int)Math.Ceiling(bounds.Height + padding * 2);

                // Save current transform
                var savedScaleX = ScaleTransform.ScaleX;
                var savedScaleY = ScaleTransform.ScaleY;
                var savedTranslateX = TranslateTransform.X;
                var savedTranslateY = TranslateTransform.Y;

                // Reset transform for export - set to 1:1 scale and translate to show all content
                ScaleTransform.ScaleX = 1;
                ScaleTransform.ScaleY = 1;
                TranslateTransform.X = -bounds.X + padding;
                TranslateTransform.Y = -bounds.Y + padding;

                // Force layout update
                TreeCanvasElement.UpdateLayout();

                // Create a DrawingVisual to render the content
                var drawingVisual = new DrawingVisual();
                using (var context = drawingVisual.RenderOpen())
                {
                    // Draw background
                    var bgBrush = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                    context.DrawRectangle(bgBrush, null, new Rect(0, 0, width, height));

                    // Render the connections canvas
                    var connectionsBrush = new VisualBrush(ConnectionsCanvas);
                    connectionsBrush.Stretch = Stretch.None;
                    connectionsBrush.AlignmentX = AlignmentX.Left;
                    connectionsBrush.AlignmentY = AlignmentY.Top;
                    
                    // Render connections with offset
                    context.PushTransform(new TranslateTransform(-bounds.X + padding, -bounds.Y + padding));
                    foreach (var path in _connectionPaths.Values)
                    {
                        if (path.Data is PathGeometry pathGeom)
                        {
                            var brush = path.Stroke;
                            var pen = new Pen(brush, path.StrokeThickness);
                            if (path.StrokeDashArray != null && path.StrokeDashArray.Count > 0)
                            {
                                pen.DashStyle = new DashStyle(path.StrokeDashArray, 0);
                            }
                            context.DrawGeometry(null, pen, pathGeom);
                        }
                    }
                    
                    // Also render any additional connection canvas children (sibling bars, etc.)
                    foreach (var child in ConnectionsCanvas.Children)
                    {
                        if (child is Path path && !_connectionPaths.Values.Contains(path))
                        {
                            if (path.Data is PathGeometry pathGeom)
                            {
                                var brush = path.Stroke;
                                var pen = new Pen(brush, path.StrokeThickness);
                                if (path.StrokeDashArray != null && path.StrokeDashArray.Count > 0)
                                {
                                    pen.DashStyle = new DashStyle(path.StrokeDashArray, 0);
                                }
                                context.DrawGeometry(null, pen, pathGeom);
                            }
                        }
                        else if (child is Line line)
                        {
                            var pen = new Pen(line.Stroke, line.StrokeThickness);
                            if (line.StrokeDashArray != null && line.StrokeDashArray.Count > 0)
                            {
                                pen.DashStyle = new DashStyle(line.StrokeDashArray, 0);
                            }
                            context.DrawLine(pen, new Point(line.X1, line.Y1), new Point(line.X2, line.Y2));
                        }
                    }
                    context.Pop();

                    // Draw nodes
                    foreach (var node in _familyTree.Nodes)
                    {
                        DrawNodeForExport(context, node, -bounds.X + padding, -bounds.Y + padding);
                    }
                }

                // Render to bitmap
                var renderBitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                renderBitmap.Render(drawingVisual);

                // Restore transform
                ScaleTransform.ScaleX = savedScaleX;
                ScaleTransform.ScaleY = savedScaleY;
                TranslateTransform.X = savedTranslateX;
                TranslateTransform.Y = savedTranslateY;
                TreeCanvasElement.UpdateLayout();

                // Save to file
                using (var stream = System.IO.File.Create(filePath))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
                    encoder.Save(stream);
                }

                MessageBox.Show($"Exported to {filePath}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Generates a bitmap preview for the export preview dialog.
        /// </summary>
        public RenderTargetBitmap? GenerateExportPreview()
        {
            if (_familyTree == null || _familyTree.Nodes.Count == 0)
            {
                return null;
            }

            var bounds = CalculateContentBounds();
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return null;
            }

            double padding = 50;
            int width = (int)Math.Ceiling(bounds.Width + padding * 2);
            int height = (int)Math.Ceiling(bounds.Height + padding * 2);

            // Create a DrawingVisual to render the content
            var drawingVisual = new DrawingVisual();
            using (var context = drawingVisual.RenderOpen())
            {
                // Draw background
                var bgBrush = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                context.DrawRectangle(bgBrush, null, new Rect(0, 0, width, height));

                // Render connections with offset
                context.PushTransform(new TranslateTransform(-bounds.X + padding, -bounds.Y + padding));
                foreach (var child in ConnectionsCanvas.Children)
                {
                    if (child is Path path)
                    {
                        if (path.Data is PathGeometry pathGeom)
                        {
                            var brush = path.Stroke;
                            var pen = new Pen(brush, path.StrokeThickness);
                            if (path.StrokeDashArray != null && path.StrokeDashArray.Count > 0)
                            {
                                pen.DashStyle = new DashStyle(path.StrokeDashArray, 0);
                            }
                            context.DrawGeometry(null, pen, pathGeom);
                        }
                    }
                    else if (child is Line line)
                    {
                        var pen = new Pen(line.Stroke, line.StrokeThickness);
                        if (line.StrokeDashArray != null && line.StrokeDashArray.Count > 0)
                        {
                            pen.DashStyle = new DashStyle(line.StrokeDashArray, 0);
                        }
                        context.DrawLine(pen, new Point(line.X1, line.Y1), new Point(line.X2, line.Y2));
                    }
                }
                context.Pop();

                // Draw nodes
                foreach (var node in _familyTree.Nodes)
                {
                    DrawNodeForExport(context, node, -bounds.X + padding, -bounds.Y + padding);
                }
            }

            // Render to bitmap
            var renderBitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            renderBitmap.Render(drawingVisual);

            return renderBitmap;
        }

        private void DrawNodeForExport(DrawingContext context, Node node, double offsetX, double offsetY)
        {
            double x = node.Position.X + offsetX;
            double y = node.Position.Y + offsetY;
            double nodeWidth = 120;
            double nodeHeight = 60;

            // Get actual size if available
            if (_nodeControls.TryGetValue(node.Id, out var nodeControl))
            {
                if (nodeControl.ActualWidth > 0) nodeWidth = nodeControl.ActualWidth;
                if (nodeControl.ActualHeight > 0) nodeHeight = nodeControl.ActualHeight;
            }

            // Node background color based on gender
            Color bgColor = node.Gender switch
            {
                Gender.Male => Color.FromRgb(70, 130, 180),    // Steel blue
                Gender.Female => Color.FromRgb(219, 112, 147), // Pale violet red
                _ => Color.FromRgb(128, 128, 128)              // Gray
            };

            // Draw rounded rectangle background
            var rect = new Rect(x, y, nodeWidth, nodeHeight);
            var bgBrush = new SolidColorBrush(Color.FromRgb(45, 45, 48)); // Dark background
            var borderBrush = new SolidColorBrush(bgColor);
            var borderPen = new Pen(borderBrush, 2);
            
            // Create rounded rectangle geometry
            var geometry = new RectangleGeometry(rect, 8, 8);
            context.DrawGeometry(bgBrush, borderPen, geometry);

            // Draw name text
            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            var textBrush = new SolidColorBrush(Colors.White);
            var formattedText = new FormattedText(
                node.Name ?? "Unknown",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                14,
                textBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            
            // Center text in node
            double textX = x + (nodeWidth - formattedText.Width) / 2;
            double textY = y + (nodeHeight - formattedText.Height) / 2;
            context.DrawText(formattedText, new Point(textX, textY));
        }

        private void DrawConnectionForExport(DrawingContext context, Connection connection, double offsetX, double offsetY)
        {
            var fromNode = _familyTree?.Nodes.FirstOrDefault(n => n.Id == connection.FromNodeId);
            var toNode = _familyTree?.Nodes.FirstOrDefault(n => n.Id == connection.ToNodeId);
            
            if (fromNode == null || toNode == null) return;

            // Get node sizes
            double fromWidth = 120, fromHeight = 60;
            double toWidth = 120, toHeight = 60;
            
            if (_nodeControls.TryGetValue(connection.FromNodeId, out var fromControl))
            {
                if (fromControl.ActualWidth > 0) fromWidth = fromControl.ActualWidth;
                if (fromControl.ActualHeight > 0) fromHeight = fromControl.ActualHeight;
            }
            if (_nodeControls.TryGetValue(connection.ToNodeId, out var toControl))
            {
                if (toControl.ActualWidth > 0) toWidth = toControl.ActualWidth;
                if (toControl.ActualHeight > 0) toHeight = toControl.ActualHeight;
            }

            // Use the same direction logic as the canvas
            var (fromDir, toDir) = GetBestConnectionDirections(fromNode, toNode, connection.ConnectionType);
            
            // Calculate connection points based on direction
            Point fromPoint = GetConnectionPointForExport(fromNode.Position, fromWidth, fromHeight, fromDir, offsetX, offsetY);
            Point toPoint = GetConnectionPointForExport(toNode.Position, toWidth, toHeight, toDir, offsetX, offsetY);

            // Line color based on connection type - use the same colors as canvas
            var brush = GetConnectionBrush(connection.ConnectionType);
            var pen = new Pen(brush, 2);
            
            // Apply dash pattern if needed
            var dashArray = GetConnectionDashArray(connection.ConnectionType);
            if (dashArray != null)
            {
                pen.DashStyle = new DashStyle(dashArray, 0);
            }

            // Use the same control point calculation as the canvas
            var (cp1, cp2) = CalculateControlPoints(fromPoint, toPoint, fromDir, toDir);

            // Draw bezier curve
            var pathGeometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = fromPoint };
            figure.Segments.Add(new BezierSegment(cp1, cp2, toPoint, true));
            pathGeometry.Figures.Add(figure);
            
            context.DrawGeometry(null, pen, pathGeometry);
        }

        private Point GetConnectionPointForExport(Point nodePos, double width, double height, string direction, double offsetX, double offsetY)
        {
            double x = nodePos.X + offsetX;
            double y = nodePos.Y + offsetY;
            
            return direction switch
            {
                "top" => new Point(x + width / 2, y),
                "bottom" => new Point(x + width / 2, y + height),
                "left" => new Point(x, y + height / 2),
                "right" => new Point(x + width, y + height / 2),
                _ => new Point(x + width / 2, y + height / 2)
            };
        }

        /// <summary>
        /// Exports the canvas to an SVG file.
        /// </summary>
        public void ExportToSvg(string filePath)
        {
            try
            {
                var bounds = CalculateContentBounds();
                if (bounds.Width <= 0 || bounds.Height <= 0)
                {
                    MessageBox.Show("Nothing to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                double padding = 50;
                bounds.Inflate(padding, padding);

                using (var writer = new System.IO.StreamWriter(filePath))
                {
                    writer.WriteLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{bounds.Width}\" height=\"{bounds.Height}\" viewBox=\"{bounds.X} {bounds.Y} {bounds.Width} {bounds.Height}\">");
                    writer.WriteLine($"  <rect width=\"100%\" height=\"100%\" fill=\"#1E1E1E\"/>");

                    // Export connections
                    foreach (var path in _connectionPaths.Values)
                    {
                        if (path.Data is PathGeometry pathGeom)
                        {
                            var stroke = (path.Stroke as SolidColorBrush)?.Color ?? Colors.Gray;
                            var strokeWidth = path.StrokeThickness;
                            var dashArray = path.StrokeDashArray;
                            string dashStr = dashArray != null && dashArray.Count > 0 
                                ? $" stroke-dasharray=\"{string.Join(",", dashArray)}\"" 
                                : "";
                            
                            writer.WriteLine($"  <path d=\"{pathGeom}\" stroke=\"#{stroke.R:X2}{stroke.G:X2}{stroke.B:X2}\" stroke-width=\"{strokeWidth}\" fill=\"none\"{dashStr}/>");
                        }
                    }

                    // Export nodes
                    foreach (var nodeControl in _nodeControls.Values)
                    {
                        var node = nodeControl.Node;
                        if (node == null) continue;

                        var x = node.Position.X;
                        var y = node.Position.Y;
                        
                        // Draw node rectangle
                        writer.WriteLine($"  <rect x=\"{x}\" y=\"{y}\" width=\"120\" height=\"60\" rx=\"5\" fill=\"#2D2D2D\" stroke=\"#3E3E3E\" stroke-width=\"2\"/>");
                        
                        // Draw name text
                        writer.WriteLine($"  <text x=\"{x + 60}\" y=\"{y + 35}\" text-anchor=\"middle\" fill=\"white\" font-family=\"Segoe UI\" font-size=\"14\">{System.Security.SecurityElement.Escape(node.Name)}</text>");
                    }

                    writer.WriteLine("</svg>");
                }

                MessageBox.Show($"Exported to {filePath}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Rect CalculateContentBounds()
        {
            if (_familyTree == null || _familyTree.Nodes.Count == 0)
                return Rect.Empty;

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var node in _familyTree.Nodes)
            {
                // Get actual node control size if available
                double nodeWidth = 120;
                double nodeHeight = 60;
                
                if (_nodeControls.TryGetValue(node.Id, out var nodeControl))
                {
                    if (nodeControl.ActualWidth > 0)
                        nodeWidth = nodeControl.ActualWidth;
                    if (nodeControl.ActualHeight > 0)
                        nodeHeight = nodeControl.ActualHeight;
                }

                minX = Math.Min(minX, node.Position.X);
                minY = Math.Min(minY, node.Position.Y);
                maxX = Math.Max(maxX, node.Position.X + nodeWidth);
                maxY = Math.Max(maxY, node.Position.Y + nodeHeight);
            }

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        #endregion
    }
}
