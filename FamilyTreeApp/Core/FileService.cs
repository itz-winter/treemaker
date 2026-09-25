using System;
using System.IO;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FamilyTreeApp.Core
{
    /// <summary>
    /// Service for saving and loading family tree files.
    /// </summary>
    public static class FileService
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new StringEnumConverter() }
        };

        /// <summary>
        /// Saves a FamilyTree to a .tree file.
        /// </summary>
        public static bool SaveTree(FamilyTree tree, string filePath)
        {
            try
            {
                var treeFile = new TreeFile
                {
                    Version = 2,
                    Name = tree.Name,
                    Settings = new TreeSettings
                    {
                        Alignment = tree.AlignmentMode.ToString().ToLower(),
                        LineStyle = tree.LineStyle.ToString().ToLower(),
                        LayoutMode = tree.LayoutMode.ToString().ToLower(),
                        AllowIncest = tree.AllowIncest,
                        AllowThreesome = tree.AllowThreesome,
                        ShowGenderIcons = tree.ShowGenderIcons,
                        FontFamily = tree.FontFamily,
                        FontSize = tree.FontSize,
                        FontBold = tree.FontBold,
                        FontItalic = tree.FontItalic
                    },
                    Nodes = tree.Nodes.Select(n => new TreeNode
                    {
                        Id = n.Id,
                        Name = n.Name,
                        Gender = n.Gender.ToString().ToLower(),
                        IsAlive = n.IsAlive,
                        IsRoyal = n.IsRoyal,
                        RoyalTitle = n.RoyalTitle.ToString(),
                        GroupId = n.GroupId,
                        X = n.Position.X,
                        Y = n.Position.Y,
                        BirthDate = n.BirthDate,
                        DeathDate = n.DeathDate,
                        IsLocked = n.IsLocked,
                        ShowContinuationUp = n.ShowContinuationUp,
                        ShowContinuationDown = n.ShowContinuationDown,
                        ShowNoDescendants = n.ShowNoDescendants,
                        IsAdopted = n.IsAdopted,
                        Generation = n.Generation,
                        Width = n.Width,
                        Height = n.Height
                    }).ToList(),
                    Connections = tree.Connections.Select(c => new TreeConnection
                    {
                        Id = c.Id,
                        FromNodeId = c.FromNodeId,
                        ToNodeId = c.ToNodeId,
                        ConnectionType = c.ConnectionType.ToString()
                    }).ToList(),
                    Groups = tree.Groups.Select(g => new TreeGroup
                    {
                        Id = g.Id,
                        Name = g.Name,
                        Color = g.Color.ToString(),
                        IsVisible = g.IsVisible
                    }).ToList(),
                    TextBoxes = tree.TextBoxes.Select(t => new TreeTextBox
                    {
                        Id = t.Id,
                        Text = t.Text,
                        X = t.Position.X,
                        Y = t.Position.Y,
                        Width = t.Width,
                        Height = t.Height,
                        FontFamily = t.FontFamily,
                        FontSize = t.FontSize,
                        TextColor = t.TextColor
                    }).ToList()
                };

                string json = JsonConvert.SerializeObject(treeFile, JsonSettings);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving file: {ex.Message}", "Save Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Loads a FamilyTree from a .tree file.
        /// </summary>
        public static FamilyTree? LoadTree(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                var treeFile = JsonConvert.DeserializeObject<TreeFile>(json, JsonSettings);
                
                if (treeFile == null)
                {
                    MessageBox.Show("Failed to parse file.", "Load Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                var tree = new FamilyTree
                {
                    Name = treeFile.Name ?? "Untitled Tree"
                };

                // Apply settings
                if (treeFile.Settings != null)
                {
                    tree.AlignmentMode = treeFile.Settings.Alignment?.ToLower() == "leftright" 
                        ? AlignmentMode.LeftRight 
                        : AlignmentMode.TopDown;
                    tree.LineStyle = ParseEnum<LineStyle>(treeFile.Settings.LineStyle);
                    tree.LayoutMode = ParseEnum<LayoutMode>(treeFile.Settings.LayoutMode);
                    tree.AllowIncest = treeFile.Settings.AllowIncest;
                    tree.AllowThreesome = treeFile.Settings.AllowThreesome;
                    tree.ShowGenderIcons = treeFile.Settings.ShowGenderIcons;
                    tree.FontFamily = treeFile.Settings.FontFamily ?? tree.FontFamily;
                    tree.FontSize = treeFile.Settings.FontSize > 0 ? treeFile.Settings.FontSize : tree.FontSize;
                    tree.FontBold = treeFile.Settings.FontBold;
                    tree.FontItalic = treeFile.Settings.FontItalic;
                }

                // Load groups first (nodes may reference them)
                if (treeFile.Groups != null)
                {
                    foreach (var g in treeFile.Groups)
                    {
                        tree.Groups.Add(new Group
                        {
                            Id = g.Id ?? Guid.NewGuid().ToString(),
                            Name = g.Name ?? "Unnamed",
                            Color = ParseColor(g.Color),
                            IsVisible = g.IsVisible
                        });
                    }
                }

                // Load nodes
                if (treeFile.Nodes != null)
                {
                    foreach (var n in treeFile.Nodes)
                    {
                        var node = new Node
                        {
                            Id = n.Id ?? Guid.NewGuid().ToString(),
                            Name = n.Name ?? "Unknown",
                            Gender = ParseEnum<Gender>(n.Gender),
                            IsAlive = n.IsAlive,
                            IsRoyal = n.IsRoyal,
                            RoyalTitle = ParseEnum<RoyalTitle>(n.RoyalTitle),
                            GroupId = n.GroupId,
                            Position = new Point(n.X, n.Y),
                            BirthDate = n.BirthDate,
                            DeathDate = n.DeathDate,
                            IsLocked = n.IsLocked,
                            ShowContinuationUp = n.ShowContinuationUp,
                            ShowContinuationDown = n.ShowContinuationDown,
                            ShowNoDescendants = n.ShowNoDescendants,
                            IsAdopted = n.IsAdopted,
                            Generation = n.Generation,
                            Width = n.Width,
                            Height = n.Height
                        };
                        tree.Nodes.Add(node);
                    }
                }

                if (treeFile.TextBoxes != null)
                {
                    foreach (var t in treeFile.TextBoxes)
                    {
                        tree.TextBoxes.Add(new CanvasText
                        {
                            Id = t.Id ?? Guid.NewGuid().ToString(),
                            Text = t.Text ?? "Text",
                            Position = new Point(t.X, t.Y),
                            Width = t.Width,
                            Height = t.Height,
                            FontFamily = t.FontFamily ?? "Segoe UI",
                            FontSize = t.FontSize > 0 ? t.FontSize : 14,
                            TextColor = t.TextColor ?? "#FFFFFF"
                        });
                    }
                }

                // Load connections
                if (treeFile.Connections != null)
                {
                    foreach (var c in treeFile.Connections)
                    {
                        var connection = new Connection
                        {
                            Id = c.Id ?? Guid.NewGuid().ToString(),
                            FromNodeId = c.FromNodeId ?? "",
                            ToNodeId = c.ToNodeId ?? "",
                            ConnectionType = ParseEnum<ConnectionType>(c.ConnectionType)
                        };
                        tree.Connections.Add(connection);
                    }
                }

                return tree;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading file: {ex.Message}", "Load Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        private static T ParseEnum<T>(string? value) where T : struct
        {
            if (string.IsNullOrEmpty(value))
                return default;
            
            if (Enum.TryParse<T>(value, true, out var result))
                return result;
            
            return default;
        }

        private static System.Windows.Media.Color ParseColor(string? colorString)
        {
            if (string.IsNullOrEmpty(colorString))
                return System.Windows.Media.Colors.Gray;

            try
            {
                return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorString);
            }
            catch
            {
                return System.Windows.Media.Colors.Gray;
            }
        }
    }

    #region File Structure Classes

    public class TreeFile
    {
        public int Version { get; set; } = 1;
        public string? Name { get; set; }
        public TreeSettings? Settings { get; set; }
        public List<TreeNode>? Nodes { get; set; }
        public List<TreeConnection>? Connections { get; set; }
        public List<TreeGroup>? Groups { get; set; }
        public List<TreeTextBox>? TextBoxes { get; set; }
    }

    public class TreeSettings
    {
        public string? Alignment { get; set; }
        public string? LineStyle { get; set; }
        public string? LayoutMode { get; set; }
        public bool AllowIncest { get; set; }
        public bool AllowThreesome { get; set; }
        public bool ShowGenderIcons { get; set; }
        public string? FontFamily { get; set; }
        public double FontSize { get; set; } = 14;
        public bool FontBold { get; set; }
        public bool FontItalic { get; set; }
    }

    public class TreeNode
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Gender { get; set; }
        public bool IsAlive { get; set; } = true;
        public bool IsRoyal { get; set; }
        public string? RoyalTitle { get; set; }
        public string? GroupId { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public DateTime? BirthDate { get; set; }
        public DateTime? DeathDate { get; set; }
        public bool IsLocked { get; set; }
        public bool ShowContinuationUp { get; set; }
        public bool ShowContinuationDown { get; set; }
        public bool ShowNoDescendants { get; set; }
        public bool IsAdopted { get; set; }
        public int Generation { get; set; }
        public double Width { get; set; } = double.NaN;
        public double Height { get; set; } = double.NaN;
    }

    public class TreeConnection
    {
        public string? Id { get; set; }
        public string? FromNodeId { get; set; }
        public string? ToNodeId { get; set; }
        public string? ConnectionType { get; set; }
    }

    public class TreeGroup
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Color { get; set; }
        public bool IsVisible { get; set; } = true;
    }

    public class TreeTextBox
    {
        public string? Id { get; set; }
        public string? Text { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string? FontFamily { get; set; }
        public double FontSize { get; set; }
        public string? TextColor { get; set; }
    }

    #endregion
}
