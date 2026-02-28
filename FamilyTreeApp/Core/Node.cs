using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace FamilyTreeApp.Core
{
    /// <summary>
    /// Represents a person in the family tree.
    /// </summary>
    public class Node : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _name = string.Empty;
        private Gender _gender;
        private bool _isAlive;
        private bool _isRoyal;
        private RoyalTitle _royalTitle;
        private string? _groupId;
        private Point _position;
        private bool _isSelected;
        private bool _isLocked;
        private DateTime? _birthDate;
        private DateTime? _deathDate;
        
        // Line indicators (manually assigned)
        private bool _showContinuationUp;      // Fade-out line going up (ancestors continue)
        private bool _showContinuationDown;    // Fade-out line going down (descendants continue)  
        private bool _showNoDescendants;       // X indicator showing no descendants
        private bool _isAdopted;               // Whether this person is adopted
        private int _generation;               // Generation level for layout
        private double _width = double.NaN;    // Custom width (NaN = auto)
        private double _height = double.NaN;   // Custom height (NaN = auto)

        public Node()
        {
            _id = Guid.NewGuid().ToString();
            _name = "New Person";
            _gender = Gender.Unspecified;
            _isAlive = true;
            _isRoyal = false;
            _royalTitle = RoyalTitle.None;
            _position = new Point(0, 0);
            _isSelected = false;
            _isLocked = false;
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

        public Gender Gender
        {
            get => _gender;
            set { _gender = value; OnPropertyChanged(); }
        }

        public bool IsAlive
        {
            get => _isAlive;
            set { _isAlive = value; OnPropertyChanged(); }
        }

        public bool IsRoyal
        {
            get => _isRoyal;
            set { _isRoyal = value; OnPropertyChanged(); }
        }

        public RoyalTitle RoyalTitle
        {
            get => _royalTitle;
            set { _royalTitle = value; OnPropertyChanged(); }
        }

        public string? GroupId
        {
            get => _groupId;
            set { _groupId = value; OnPropertyChanged(); }
        }

        public Point Position
        {
            get => _position;
            set { _position = value; OnPropertyChanged(); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public bool IsLocked
        {
            get => _isLocked;
            set { _isLocked = value; OnPropertyChanged(); }
        }

        public DateTime? BirthDate
        {
            get => _birthDate;
            set { _birthDate = value; OnPropertyChanged(); }
        }

        public DateTime? DeathDate
        {
            get => _deathDate;
            set { _deathDate = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Shows a fade-out line going up indicating ancestors continue beyond view.
        /// </summary>
        public bool ShowContinuationUp
        {
            get => _showContinuationUp;
            set { _showContinuationUp = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Shows a fade-out line going down indicating descendants continue beyond view.
        /// </summary>
        public bool ShowContinuationDown
        {
            get => _showContinuationDown;
            set { _showContinuationDown = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Shows an X indicator meaning this person has no descendants.
        /// </summary>
        public bool ShowNoDescendants
        {
            get => _showNoDescendants;
            set { _showNoDescendants = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Whether this person is adopted (shown with dashed line and "A" indicator).
        /// </summary>
        public bool IsAdopted
        {
            get => _isAdopted;
            set { _isAdopted = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Generation level for layout purposes (0 = root generation).
        /// </summary>
        public int Generation
        {
            get => _generation;
            set { _generation = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Custom width for the node. NaN means auto-size.
        /// </summary>
        public double Width
        {
            get => _width;
            set { _width = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Custom height for the node. NaN means auto-size.
        /// </summary>
        public double Height
        {
            get => _height;
            set { _height = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Forces a visual refresh by triggering PropertyChanged event
        /// </summary>
        public void NotifyDisplayRefresh()
        {
            OnPropertyChanged(null);
        }
    }

    /// <summary>
    /// Gender options for a person.
    /// </summary>
    public enum Gender
    {
        Unspecified,
        Female,
        Male,
        Other,
        Custom
    }

    /// <summary>
    /// Royal title options.
    /// </summary>
    public enum RoyalTitle
    {
        None,
        King,
        Queen,
        FormerKing,
        FormerQueen,
        Prince,
        Princess,
        Heir
    }
}
