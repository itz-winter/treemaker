using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FamilyTreeApp.Core
{
    /// <summary>
    /// Represents a connection between two nodes in the family tree.
    /// </summary>
    public class Connection : INotifyPropertyChanged
    {
        private string _id;
        private string _fromNodeId = string.Empty;
        private string _toNodeId = string.Empty;
        private ConnectionType _connectionType;
        private bool _isSelected;

        public Connection()
        {
            _id = Guid.NewGuid().ToString();
            _connectionType = ConnectionType.Biological;
            _isSelected = false;
        }

        public Connection(string fromNodeId, string toNodeId, ConnectionType type = ConnectionType.Biological)
            : this()
        {
            _fromNodeId = fromNodeId;
            _toNodeId = toNodeId;
            _connectionType = type;
        }

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string FromNodeId
        {
            get => _fromNodeId;
            set { _fromNodeId = value; OnPropertyChanged(); }
        }

        public string ToNodeId
        {
            get => _toNodeId;
            set { _toNodeId = value; OnPropertyChanged(); }
        }

        public ConnectionType ConnectionType
        {
            get => _connectionType;
            set { _connectionType = value; OnPropertyChanged(); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Types of connections between family members.
    /// </summary>
    public enum ConnectionType
    {
        Biological,
        Adopted,
        Step,
        Partner,
        FormerPartner,
        Hidden
    }
}
