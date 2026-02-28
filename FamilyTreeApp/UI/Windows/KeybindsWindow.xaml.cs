using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FamilyTreeApp.Core;

namespace FamilyTreeApp.UI.Windows
{
    /// <summary>
    /// Window for customizing keyboard shortcuts.
    /// </summary>
    public partial class KeybindsWindow : Window
    {
        private KeybindSettings _keybinds;
        
        public KeybindsWindow()
        {
            InitializeComponent();
            _keybinds = SettingsManager.Current.Keybinds;
            LoadKeybinds();
        }
        
        private void LoadKeybinds()
        {
            AddNodeKeybind.Text = _keybinds.AddNode;
            RemoveConnectionKeybind.Text = _keybinds.RemoveConnection;
            DeleteNodeKeybind.Text = _keybinds.DeleteNode;
            UndoKeybind.Text = _keybinds.Undo;
            RedoKeybind.Text = _keybinds.Redo;
            SaveKeybind.Text = _keybinds.Save;
            OpenKeybind.Text = _keybinds.Open;
            NewFileKeybind.Text = _keybinds.NewFile;
            ZoomInKeybind.Text = _keybinds.ZoomIn;
            ZoomOutKeybind.Text = _keybinds.ZoomOut;
            ResetViewKeybind.Text = _keybinds.ResetView;
            ToggleGridKeybind.Text = _keybinds.ToggleGrid;
            SelectAllKeybind.Text = _keybinds.SelectAll;
            DuplicateKeybind.Text = _keybinds.Duplicate;
        }
        
        private void Keybind_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox)
            {
                return;
            }
            
            e.Handled = true;
            
            // Build the keybind string
            var keyString = "";
            
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                keyString += "Ctrl+";
            }
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                keyString += "Shift+";
            }
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                keyString += "Alt+";
            }
            
            // Get the actual key (not just modifier keys)
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            
            // Ignore if it's just a modifier key
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }
            
            // Special handling for some keys
            keyString += key switch
            {
                Key.OemPlus => "Plus",
                Key.OemMinus => "Minus",
                Key.Add => "Plus",
                Key.Subtract => "Minus",
                _ => key.ToString()
            };
            
            textBox.Text = keyString;
        }
        
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _keybinds.AddNode = AddNodeKeybind.Text;
            _keybinds.RemoveConnection = RemoveConnectionKeybind.Text;
            _keybinds.DeleteNode = DeleteNodeKeybind.Text;
            _keybinds.Undo = UndoKeybind.Text;
            _keybinds.Redo = RedoKeybind.Text;
            _keybinds.Save = SaveKeybind.Text;
            _keybinds.Open = OpenKeybind.Text;
            _keybinds.NewFile = NewFileKeybind.Text;
            _keybinds.ZoomIn = ZoomInKeybind.Text;
            _keybinds.ZoomOut = ZoomOutKeybind.Text;
            _keybinds.ResetView = ResetViewKeybind.Text;
            _keybinds.ToggleGrid = ToggleGridKeybind.Text;
            _keybinds.SelectAll = SelectAllKeybind.Text;
            _keybinds.Duplicate = DuplicateKeybind.Text;
            
            SettingsManager.Current.Keybinds = _keybinds;
            SettingsManager.Save();
            
            DialogResult = true;
            Close();
        }
        
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to restore all keybinds to their defaults?",
                "Restore Defaults",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );
            
            if (result == MessageBoxResult.Yes)
            {
                _keybinds = KeybindSettings.GetDefaults();
                LoadKeybinds();
            }
        }
    }
}
