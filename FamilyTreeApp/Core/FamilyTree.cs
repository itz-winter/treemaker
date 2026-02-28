using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace FamilyTreeApp.Core
{
    /// <summary>
    /// Represents the entire family tree with all nodes and connections.
    /// </summary>
    public class FamilyTree : INotifyPropertyChanged
    {
        private string _name = "Untitled Tree";
        private AlignmentMode _alignmentMode = AlignmentMode.TopDown;
        private LineStyle _lineStyle = LineStyle.Curves;
        private LayoutMode _layoutMode = LayoutMode.Fixed;
        private bool _allowIncest = false;
        private bool _allowThreesome = false;
        private bool _showGenderIcons = false;
        private string _fontFamily = "Segoe UI";
        private double _fontSize = 14;
        private bool _fontBold = false;
        private bool _fontItalic = false;

        public FamilyTree()
        {
            Nodes = new ObservableCollection<Node>();
            Connections = new ObservableCollection<Connection>();
            Groups = new ObservableCollection<Group>();
            TextBoxes = new ObservableCollection<CanvasText>();
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public AlignmentMode AlignmentMode
        {
            get => _alignmentMode;
            set { _alignmentMode = value; OnPropertyChanged(); }
        }

        public LineStyle LineStyle
        {
            get => _lineStyle;
            set { _lineStyle = value; OnPropertyChanged(); }
        }

        public LayoutMode LayoutMode
        {
            get => _layoutMode;
            set { _layoutMode = value; OnPropertyChanged(); }
        }

        public bool AllowIncest
        {
            get => _allowIncest;
            set { _allowIncest = value; OnPropertyChanged(); }
        }

        public bool AllowThreesome
        {
            get => _allowThreesome;
            set { _allowThreesome = value; OnPropertyChanged(); }
        }

        public bool ShowGenderIcons
        {
            get => _showGenderIcons;
            set { _showGenderIcons = value; OnPropertyChanged(); }
        }

        public string FontFamily
        {
            get => _fontFamily;
            set { _fontFamily = value; OnPropertyChanged(); }
        }

        public double FontSize
        {
            get => _fontSize;
            set { _fontSize = value; OnPropertyChanged(); }
        }

        public bool FontBold
        {
            get => _fontBold;
            set { _fontBold = value; OnPropertyChanged(); }
        }

        public bool FontItalic
        {
            get => _fontItalic;
            set { _fontItalic = value; OnPropertyChanged(); }
        }

        public ObservableCollection<Node> Nodes { get; }
        public ObservableCollection<Connection> Connections { get; }
        public ObservableCollection<Group> Groups { get; }
        public ObservableCollection<CanvasText> TextBoxes { get; }

        /// <summary>
        /// Adds a new node to the tree.
        /// </summary>
        public Node AddNode(string name = "New Person")
        {
            var node = new Node { Name = name };
            Nodes.Add(node);
            return node;
        }

        /// <summary>
        /// Removes a node and all its connections from the tree.
        /// </summary>
        public void RemoveNode(Node node)
        {
            // Remove all connections involving this node
            var connectionsToRemove = Connections
                .Where(c => c.FromNodeId == node.Id || c.ToNodeId == node.Id)
                .ToList();

            foreach (var conn in connectionsToRemove)
            {
                Connections.Remove(conn);
            }

            Nodes.Remove(node);
        }

        /// <summary>
        /// Adds a connection between two nodes.
        /// </summary>
        public Connection AddConnection(string fromNodeId, string toNodeId, ConnectionType type = ConnectionType.Biological)
        {
            // Check if connection already exists
            var existing = Connections.FirstOrDefault(c =>
                (c.FromNodeId == fromNodeId && c.ToNodeId == toNodeId) ||
                (c.FromNodeId == toNodeId && c.ToNodeId == fromNodeId));

            if (existing != null)
                return existing;

            var connection = new Connection(fromNodeId, toNodeId, type);
            Connections.Add(connection);
            return connection;
        }

        /// <summary>
        /// Removes a connection from the tree.
        /// </summary>
        public void RemoveConnection(Connection connection)
        {
            Connections.Remove(connection);
        }

        /// <summary>
        /// Gets a node by its ID.
        /// </summary>
        public Node? GetNodeById(string id)
        {
            return Nodes.FirstOrDefault(n => n.Id == id);
        }

        /// <summary>
        /// Gets all connections for a specific node.
        /// </summary>
        public IEnumerable<Connection> GetConnectionsForNode(string nodeId)
        {
            return Connections.Where(c => c.FromNodeId == nodeId || c.ToNodeId == nodeId);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Tree alignment modes.
    /// </summary>
    public enum AlignmentMode
    {
        TopDown,
        LeftRight
    }

    /// <summary>
    /// Connection line style.
    /// </summary>
    public enum LineStyle
    {
        Curves,
        Square
    }

    /// <summary>
    /// Layout mode for node positioning.
    /// </summary>
    public enum LayoutMode
    {
        /// <summary>
        /// Automatic layout - nodes are positioned automatically and cannot be manually moved.
        /// </summary>
        Fixed,
        /// <summary>
        /// Free layout - nodes can be manually positioned by the user.
        /// </summary>
        Free
    }
}
