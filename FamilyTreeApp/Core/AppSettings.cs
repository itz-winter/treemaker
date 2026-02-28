using System;
using System.Windows.Media;
using Newtonsoft.Json;

namespace FamilyTreeApp.Core
{
    /// <summary>
    /// Application settings that can be saved and loaded.
    /// </summary>
    public class AppSettings
    {
        // First run flag
        public bool FirstRunComplete { get; set; } = false;
        
        // General settings
        public bool DarkMode { get; set; } = true;
        public bool AllowIncest { get; set; } = false;
        public bool AllowThreesome { get; set; } = false;
        public bool ShowGenderIcons { get; set; } = true;
        public bool ShowGrid { get; set; } = false;
        
        // Layout settings
        public string Alignment { get; set; } = "TopDown"; // "TopDown" or "LeftRight"
        public string LineStyle { get; set; } = "Curves"; // "Curves" or "Square"
        public string LayoutMode { get; set; } = "Fixed"; // "Fixed" or "Free"
        
        // Crown settings
        public string CrownDisplay { get; set; } = "QueenOnly"; // "QueenOnly", "KingOnly", "Both", "None"
        
        // Gender icon style settings
        public string GenderIconStyle { get; set; } = "Dots"; // "Dots" (new default), "Symbols" (current), "ColoredCircles"
        
        // Snapping settings for free mode
        public bool SnapToAngle { get; set; } = false;
        public bool SnapToGrid { get; set; } = true;
        public bool SnapToGeometry { get; set; } = false;
        public double GridSnapSize { get; set; } = 20;
        public double AngleSnapDegrees { get; set; } = 45;
        
        // Confirmation prompts
        public bool ConfirmUnsavedChanges { get; set; } = true;
        
        // Color settings (stored as hex strings for JSON serialization)
        public string NodeFillColor { get; set; } = "#1E1E1E";
        public string NodeBorderColor { get; set; } = "#3F3F46";
        public string NodeTextColor { get; set; } = "#FFFFFF";
        public string CanvasBackgroundColor { get; set; } = "#121212";
        public string GridColor { get; set; } = "#2A2A2A";
        
        // Font settings
        public string FontFamily { get; set; } = "Segoe UI";
        public double FontSize { get; set; } = 14;
        public bool FontBold { get; set; } = false;
        public bool FontItalic { get; set; } = false;
        
        // Connection style settings
        public ConnectionStyleSettings ConnectionStyles { get; set; } = new();
        
        // Keybinds (action name -> key string)
        public KeybindSettings Keybinds { get; set; } = new();
        
        /// <summary>
        /// Returns the default settings.
        /// </summary>
        public static AppSettings GetDefaults()
        {
            return new AppSettings();
        }
        
        /// <summary>
        /// Resets this settings instance to defaults.
        /// </summary>
        public void ResetToDefaults()
        {
            var defaults = GetDefaults();
            DarkMode = defaults.DarkMode;
            AllowIncest = defaults.AllowIncest;
            AllowThreesome = defaults.AllowThreesome;
            ShowGenderIcons = defaults.ShowGenderIcons;
            ShowGrid = defaults.ShowGrid;
            Alignment = defaults.Alignment;
            LineStyle = defaults.LineStyle;
            LayoutMode = defaults.LayoutMode;
            CrownDisplay = defaults.CrownDisplay;
            NodeFillColor = defaults.NodeFillColor;
            NodeBorderColor = defaults.NodeBorderColor;
            NodeTextColor = defaults.NodeTextColor;
            CanvasBackgroundColor = defaults.CanvasBackgroundColor;
            GridColor = defaults.GridColor;
            FontFamily = defaults.FontFamily;
            FontSize = defaults.FontSize;
            FontBold = defaults.FontBold;
            FontItalic = defaults.FontItalic;
            ConnectionStyles = new ConnectionStyleSettings();
            Keybinds = new KeybindSettings();
        }
    }
    
    /// <summary>
    /// Keybind settings for customizable keyboard shortcuts.
    /// </summary>
    public class KeybindSettings
    {
        public string AddNode { get; set; } = "Z";
        public string RemoveConnection { get; set; } = "X";
        public string DeleteNode { get; set; } = "Delete";
        public string Undo { get; set; } = "Ctrl+Z";
        public string Redo { get; set; } = "Ctrl+Y";
        public string Save { get; set; } = "Ctrl+S";
        public string Open { get; set; } = "Ctrl+O";
        public string NewFile { get; set; } = "Ctrl+N";
        public string ZoomIn { get; set; } = "Ctrl+Plus";
        public string ZoomOut { get; set; } = "Ctrl+Minus";
        public string ResetView { get; set; } = "R";
        public string ToggleGrid { get; set; } = "G";
        public string SelectAll { get; set; } = "Ctrl+A";
        public string Duplicate { get; set; } = "Ctrl+D";
        
        /// <summary>
        /// Returns the default keybinds.
        /// </summary>
        public static KeybindSettings GetDefaults()
        {
            return new KeybindSettings();
        }
    }
}
