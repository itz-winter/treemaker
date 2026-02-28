using System.Windows;
using System.Windows.Controls;
using FamilyTreeApp.Core;

namespace FamilyTreeApp.UI.Windows
{
    /// <summary>
    /// Dialog for creating or editing custom tools.
    /// </summary>
    public partial class CustomToolWindow : Window
    {
        public ToolItem? CreatedTool { get; private set; }

        public CustomToolWindow()
        {
            InitializeComponent();
            LoadCommands();
            
            // Update preview on text changes
            NameTextBox.TextChanged += UpdatePreview;
            IconTextBox.TextChanged += UpdatePreview;
        }

        private void LoadCommands()
        {
            foreach (var cmd in ToolbarManager.AvailableCommands)
            {
                var item = new ComboBoxItem
                {
                    Content = $"{cmd.Key} - {cmd.Value}",
                    Tag = cmd.Key
                };
                CommandComboBox.Items.Add(item);
            }
            
            if (CommandComboBox.Items.Count > 0)
                CommandComboBox.SelectedIndex = 0;
        }

        private void UpdatePreview(object sender, TextChangedEventArgs e)
        {
            var icon = string.IsNullOrWhiteSpace(IconTextBox.Text) ? "🔧" : IconTextBox.Text;
            var name = string.IsNullOrWhiteSpace(NameTextBox.Text) ? "New Tool" : NameTextBox.Text;
            PreviewButton.Content = $"{icon} {name}";
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("Please enter a tool name.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var commandName = "AddNode"; // Default
            if (CommandComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                commandName = selectedItem.Tag?.ToString() ?? "AddNode";
            }

            CreatedTool = new ToolItem
            {
                Name = NameTextBox.Text.Trim(),
                Icon = string.IsNullOrWhiteSpace(IconTextBox.Text) ? "🔧" : IconTextBox.Text.Trim(),
                CommandName = commandName,
                Tooltip = TooltipTextBox.Text.Trim(),
                IsBuiltIn = false,
                IsVisible = true
            };

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
