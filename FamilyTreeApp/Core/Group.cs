using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace FamilyTreeApp.Core
{
    /// <summary>
    /// Represents a group of nodes for bulk editing and styling.
    /// </summary>
    public class Group : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _name = string.Empty;
        private Color _color = Colors.Gray;
        private bool _isVisible = true;

        public Group()
        {
            _id = Guid.NewGuid().ToString();
            _name = "New Group";
        }

        public Group(string name) : this()
        {
            _name = name;
        }

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public Color Color
        {
            get => _color;
            set { _color = value; OnPropertyChanged(); }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
