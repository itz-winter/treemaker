using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace FamilyTreeApp.Core
{
    /// <summary>
    /// Manages the toolbar configuration, including custom tools and ordering.
    /// </summary>
    public class ToolbarManager
    {
        private static readonly string ToolbarConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FamilyTreeApp",
            "toolbar.json"
        );

        private static ToolbarManager? _instance;
        public static ToolbarManager Instance => _instance ??= new ToolbarManager();

        public ObservableCollection<ToolItem> Tools { get; private set; }

        /// <summary>
        /// Available commands that can be assigned to tools.
        /// </summary>
        public static readonly Dictionary<string, string> AvailableCommands = new()
        {
            { "AddNode", "Add a new node to the tree" },
            { "AddLink", "Add a connection between nodes" },
            { "Royalty", "Manage royalty settings" },
            { "Colors", "Open colors window" },
            { "Groups", "Manage groups" },
            { "ZoomIn", "Zoom in on the canvas" },
            { "ZoomOut", "Zoom out on the canvas" },
            { "ResetView", "Reset the canvas view" },
            { "ToggleGrid", "Toggle grid visibility" },
            { "Settings", "Open settings" },
            { "Save", "Save the current tree" },
            { "Open", "Open a tree file" },
            { "New", "Create a new tree" },
            { "Undo", "Undo last action" },
            { "Redo", "Redo last undone action" },
            { "Delete", "Delete selected items" },
            { "AutoLayout", "Auto-arrange nodes" },
            { "ExportPng", "Export as PNG" },
            { "ExportSvg", "Export as SVG" },
            { "FontSettings", "Change tree font settings" }
        };

        public ToolbarManager()
        {
            Tools = new ObservableCollection<ToolItem>();
        }

        /// <summary>
        /// Gets the default built-in tools.
        /// </summary>
        private List<ToolItem> GetDefaultTools()
        {
            return new List<ToolItem>
            {
                new ToolItem { Id = "addnode", Name = "Add Node", Icon = "➕", CommandName = "AddNode", IsBuiltIn = true, Order = 0, Tooltip = "Add a new person node" },
                new ToolItem { Id = "addlink", Name = "Add Link", Icon = "🔗", CommandName = "AddLink", IsBuiltIn = true, Order = 1, Tooltip = "Add a connection (select a node first)", IsEnabled = false },
                new ToolItem { Id = "colors", Name = "Colors", Icon = "🎨", CommandName = "Colors", IsBuiltIn = true, Order = 2, Tooltip = "Open colors window" },
                new ToolItem { Id = "groups", Name = "Groups", Icon = "🧩", CommandName = "Groups", IsBuiltIn = true, Order = 3, Tooltip = "Manage groups" },
            };
        }

        /// <summary>
        /// Loads toolbar configuration from disk, or creates defaults.
        /// </summary>
        public void Load()
        {
            Tools.Clear();

            try
            {
                if (File.Exists(ToolbarConfigPath))
                {
                    var json = File.ReadAllText(ToolbarConfigPath);
                    var savedTools = JsonConvert.DeserializeObject<List<ToolItem>>(json);
                    
                    if (savedTools != null && savedTools.Count > 0)
                    {
                        foreach (var tool in savedTools.OrderBy(t => t.Order))
                        {
                            Tools.Add(tool);
                        }
                        return;
                    }
                }
            }
            catch
            {
                // Ignore errors, use defaults
            }

            // Load defaults
            foreach (var tool in GetDefaultTools())
            {
                Tools.Add(tool);
            }
        }

        /// <summary>
        /// Saves the current toolbar configuration to disk.
        /// </summary>
        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(ToolbarConfigPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonConvert.SerializeObject(Tools.ToList(), Formatting.Indented);
                File.WriteAllText(ToolbarConfigPath, json);
            }
            catch
            {
                // Ignore save errors
            }
        }

        /// <summary>
        /// Adds a new custom tool.
        /// </summary>
        public ToolItem AddCustomTool(string name, string icon, string commandName, string tooltip = "")
        {
            var tool = new ToolItem
            {
                Name = name,
                Icon = icon,
                CommandName = commandName,
                Tooltip = tooltip,
                IsBuiltIn = false,
                IsVisible = true,
                Order = Tools.Count
            };

            Tools.Add(tool);
            Save();
            return tool;
        }

        /// <summary>
        /// Adds a pre-created custom tool.
        /// </summary>
        public void AddCustomTool(ToolItem tool)
        {
            tool.Order = Tools.Count;
            tool.IsBuiltIn = false;
            Tools.Add(tool);
            Save();
        }

        /// <summary>
        /// Deletes a custom tool (alias for RemoveTool).
        /// </summary>
        public bool DeleteCustomTool(ToolItem tool)
        {
            return RemoveTool(tool);
        }

        /// <summary>
        /// Moves a tool up in the list.
        /// </summary>
        public void MoveToolUp(ToolItem tool)
        {
            var index = Tools.IndexOf(tool);
            if (index > 0)
            {
                MoveTool(tool, index - 1);
            }
        }

        /// <summary>
        /// Moves a tool down in the list.
        /// </summary>
        public void MoveToolDown(ToolItem tool)
        {
            var index = Tools.IndexOf(tool);
            if (index >= 0 && index < Tools.Count - 1)
            {
                MoveTool(tool, index + 1);
            }
        }

        /// <summary>
        /// Removes a custom tool (built-in tools cannot be removed).
        /// </summary>
        public bool RemoveTool(ToolItem tool)
        {
            if (tool.IsBuiltIn)
                return false;

            var result = Tools.Remove(tool);
            if (result)
            {
                ReorderTools();
                Save();
            }
            return result;
        }

        /// <summary>
        /// Hides a tool (sets IsVisible to false).
        /// </summary>
        public void HideTool(ToolItem tool)
        {
            tool.IsVisible = false;
            Save();
        }

        /// <summary>
        /// Shows a hidden tool.
        /// </summary>
        public void ShowTool(ToolItem tool)
        {
            tool.IsVisible = true;
            Save();
        }

        /// <summary>
        /// Moves a tool to a new position.
        /// </summary>
        public void MoveTool(ToolItem tool, int newIndex)
        {
            if (newIndex < 0 || newIndex >= Tools.Count)
                return;

            var oldIndex = Tools.IndexOf(tool);
            if (oldIndex == newIndex || oldIndex < 0)
                return;

            Tools.Move(oldIndex, newIndex);
            ReorderTools();
            Save();
        }

        /// <summary>
        /// Reorders all tools based on their position in the collection.
        /// </summary>
        private void ReorderTools()
        {
            for (int i = 0; i < Tools.Count; i++)
            {
                Tools[i].Order = i;
            }
        }

        /// <summary>
        /// Resets toolbar to default configuration.
        /// </summary>
        public void ResetToDefaults()
        {
            Tools.Clear();
            foreach (var tool in GetDefaultTools())
            {
                Tools.Add(tool);
            }
            Save();
        }

        /// <summary>
        /// Gets all hidden tools.
        /// </summary>
        public IEnumerable<ToolItem> GetHiddenTools()
        {
            return Tools.Where(t => !t.IsVisible && !t.IsSeparator);
        }

        /// <summary>
        /// Gets all visible tools.
        /// </summary>
        public IEnumerable<ToolItem> GetVisibleTools()
        {
            return Tools.Where(t => t.IsVisible).OrderBy(t => t.Order);
        }
    }
}
