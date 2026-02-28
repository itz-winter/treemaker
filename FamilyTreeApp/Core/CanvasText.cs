using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace FamilyTreeApp.Core
{
    /// <summary>
    /// Represents a text box that can be placed on the canvas.
    /// </summary>
    public class CanvasText : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _text = "Text";
        private Point _position = new Point(100, 100);
        private double _width = 150;
        private double _height = 60;
        private string _fontFamily = "Segoe UI";
        private double _fontSize = 14;
        private string _textColor = "#FFFFFF";

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Text
        {
            get => _text;
            set { _text = value; OnPropertyChanged(); }
        }

        public Point Position
        {
            get => _position;
            set { _position = value; OnPropertyChanged(); }
        }

        public double Width
        {
            get => _width;
            set { _width = value; OnPropertyChanged(); }
        }

        public double Height
        {
            get => _height;
            set { _height = value; OnPropertyChanged(); }
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

        public string TextColor
        {
            get => _textColor;
            set { _textColor = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
