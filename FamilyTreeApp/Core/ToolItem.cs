using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FamilyTreeApp.Core
{
    /// <summary>
    /// Represents a tool item in the toolbar.
    /// </summary>
    public class ToolItem : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _name = string.Empty;
        private string _icon = string.Empty;
        private string _tooltip = string.Empty;
        private string _commandName = string.Empty;
        private bool _isBuiltIn = true;
        private bool _isVisible = true;
        private bool _isEnabled = true;
        private int _order = 0;
        private bool _isSeparator = false;

        public ToolItem()
        {
            _id = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Unique identifier for the tool.
        /// </summary>
        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Display name of the tool.
        /// </summary>
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Icon/emoji displayed on the button.
        /// </summary>
        public string Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Tooltip text shown on hover.
        /// </summary>
        public string Tooltip
        {
            get => _tooltip;
            set { _tooltip = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Name of the command to execute when clicked.
        /// </summary>
        public string CommandName
        {
            get => _commandName;
            set { _commandName = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Whether this is a built-in tool (cannot be deleted).
        /// </summary>
        public bool IsBuiltIn
        {
            get => _isBuiltIn;
            set { _isBuiltIn = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Whether the tool is visible in the toolbar.
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Whether the tool is currently enabled.
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Order/position in the toolbar.
        /// </summary>
        public int Order
        {
            get => _order;
            set { _order = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Whether this item is a separator.
        /// </summary>
        public bool IsSeparator
        {
            get => _isSeparator;
            set { _isSeparator = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Display text combining icon and name.
        /// </summary>
        public string DisplayText => string.IsNullOrEmpty(Icon) ? Name : $"{Icon} {Name}";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Creates a copy of this tool item.
        /// </summary>
        public ToolItem Clone()
        {
            return new ToolItem
            {
                Id = Guid.NewGuid().ToString(),
                Name = this.Name,
                Icon = this.Icon,
                Tooltip = this.Tooltip,
                CommandName = this.CommandName,
                IsBuiltIn = false, // Clones are never built-in
                IsVisible = this.IsVisible,
                IsEnabled = this.IsEnabled,
                Order = this.Order,
                IsSeparator = this.IsSeparator
            };
        }
    }
}
