using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using FamilyTreeApp.Core;

namespace FamilyTreeApp.UI.Windows
{
    /// <summary>
    /// Interaction logic for GroupsWindow.xaml
    /// </summary>
    public partial class GroupsWindow : Window
    {
        private FamilyTree _familyTree;
        private bool _isUpdating = false;
        private bool _hasUnsavedChanges = false;
        
        // Store pending changes - we'll work with copies
        private List<GroupEditState> _editableGroups = new();

        public GroupsWindow(FamilyTree familyTree)
        {
            InitializeComponent();
            _familyTree = familyTree;
            LoadGroupsCopy();
            RefreshGroupsList();
        }

        /// <summary>
        /// Create editable copies of all groups
        /// </summary>
        private void LoadGroupsCopy()
        {
            _editableGroups.Clear();
            foreach (var group in _familyTree.Groups)
            {
                _editableGroups.Add(new GroupEditState
                {
                    OriginalGroup = group,
                    Name = group.Name,
                    Color = group.Color,
                    IsVisible = group.IsVisible,
                    IsNew = false,
                    IsDeleted = false
                });
            }
        }

        private void RefreshGroupsList()
        {
            var selectedName = (GroupsListBox.SelectedItem as GroupEditState)?.Name;
            
            GroupsListBox.ItemsSource = null;
            GroupsListBox.ItemsSource = _editableGroups.Where(g => !g.IsDeleted).ToList();
            
            // Restore selection by name
            if (selectedName != null)
            {
                var toSelect = _editableGroups.FirstOrDefault(g => g.Name == selectedName && !g.IsDeleted);
                if (toSelect != null)
                {
                    GroupsListBox.SelectedItem = toSelect;
                }
            }
        }

        private void GroupsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var selectedGroup = GroupsListBox.SelectedItem as GroupEditState;
            GroupPropertiesPanel.IsEnabled = selectedGroup != null;

            if (selectedGroup != null)
            {
                _isUpdating = true;
                GroupNameTextBox.Text = selectedGroup.Name;
                GroupColorPicker.SelectedColor = selectedGroup.Color;
                GroupVisibleCheckBox.IsChecked = selectedGroup.IsVisible;
                _isUpdating = false;
            }
        }

        private void AddGroup_Click(object sender, RoutedEventArgs e)
        {
            var newGroup = new Group($"Group {_editableGroups.Count(g => !g.IsDeleted) + 1}");
            newGroup.Color = GetRandomColor();
            
            var editState = new GroupEditState
            {
                OriginalGroup = newGroup,
                Name = newGroup.Name,
                Color = newGroup.Color,
                IsVisible = newGroup.IsVisible,
                IsNew = true,
                IsDeleted = false
            };
            
            _editableGroups.Add(editState);
            _hasUnsavedChanges = true;
            RefreshGroupsList();
            GroupsListBox.SelectedItem = editState;
        }

        private void RemoveGroup_Click(object sender, RoutedEventArgs e)
        {
            var selectedGroup = GroupsListBox.SelectedItem as GroupEditState;
            if (selectedGroup == null) return;

            selectedGroup.IsDeleted = true;
            _hasUnsavedChanges = true;
            RefreshGroupsList();
        }

        private void GroupNameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isUpdating) return;
            
            var selectedGroup = GroupsListBox.SelectedItem as GroupEditState;
            if (selectedGroup != null)
            {
                selectedGroup.Name = GroupNameTextBox.Text;
                _hasUnsavedChanges = true;
                // Don't refresh list while typing - would cause cursor issues
            }
        }

        private void GroupColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (_isUpdating) return;
            
            var selectedGroup = GroupsListBox.SelectedItem as GroupEditState;
            if (selectedGroup != null && e.NewValue.HasValue)
            {
                selectedGroup.Color = e.NewValue.Value;
                _hasUnsavedChanges = true;
                RefreshGroupsList();
            }
        }

        private void GroupVisibleCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdating) return;
            
            var selectedGroup = GroupsListBox.SelectedItem as GroupEditState;
            if (selectedGroup != null)
            {
                selectedGroup.IsVisible = GroupVisibleCheckBox.IsChecked ?? true;
                _hasUnsavedChanges = true;
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            ApplyChanges();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Reload from original
            LoadGroupsCopy();
            _hasUnsavedChanges = false;
            RefreshGroupsList();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_hasUnsavedChanges && SettingsManager.Current.ConfirmUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Do you want to save them before closing?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                switch (result)
                {
                    case MessageBoxResult.Yes:
                        ApplyChanges();
                        break;
                    case MessageBoxResult.Cancel:
                        e.Cancel = true;
                        break;
                    // No = close without saving
                }
            }
        }

        private void ApplyChanges()
        {
            // Remove deleted groups
            foreach (var deleted in _editableGroups.Where(g => g.IsDeleted && !g.IsNew).ToList())
            {
                if (deleted.OriginalGroup != null)
                {
                    // Remove group assignment from all nodes
                    foreach (var node in _familyTree.Nodes)
                    {
                        if (node.GroupId == deleted.OriginalGroup.Id)
                        {
                            node.GroupId = null;
                        }
                    }
                    _familyTree.Groups.Remove(deleted.OriginalGroup);
                }
            }

            // Add new groups
            foreach (var newGroup in _editableGroups.Where(g => g.IsNew && !g.IsDeleted).ToList())
            {
                if (newGroup.OriginalGroup != null)
                {
                    newGroup.OriginalGroup.Name = newGroup.Name;
                    newGroup.OriginalGroup.Color = newGroup.Color;
                    newGroup.OriginalGroup.IsVisible = newGroup.IsVisible;
                    _familyTree.Groups.Add(newGroup.OriginalGroup);
                    newGroup.IsNew = false;
                }
            }

            // Update existing groups
            foreach (var existing in _editableGroups.Where(g => !g.IsNew && !g.IsDeleted).ToList())
            {
                if (existing.OriginalGroup != null)
                {
                    existing.OriginalGroup.Name = existing.Name;
                    existing.OriginalGroup.Color = existing.Color;
                    existing.OriginalGroup.IsVisible = existing.IsVisible;
                }
            }

            // Remove deleted entries from our list
            _editableGroups.RemoveAll(g => g.IsDeleted);
            
            _hasUnsavedChanges = false;
            RefreshGroupsList();
        }

        private Color GetRandomColor()
        {
            var random = new System.Random();
            var colors = new Color[]
            {
                Colors.Coral,
                Colors.CornflowerBlue,
                Colors.MediumPurple,
                Colors.LightGreen,
                Colors.Gold,
                Colors.Salmon,
                Colors.SkyBlue,
                Colors.Plum
            };
            return colors[random.Next(colors.Length)];
        }
    }

    /// <summary>
    /// Represents the editable state of a group for the dialog
    /// </summary>
    public class GroupEditState
    {
        public Group? OriginalGroup { get; set; }
        public string Name { get; set; } = "";
        public Color Color { get; set; } = Colors.Gray;
        public bool IsVisible { get; set; } = true;
        public bool IsNew { get; set; } = false;
        public bool IsDeleted { get; set; } = false;
    }
}
