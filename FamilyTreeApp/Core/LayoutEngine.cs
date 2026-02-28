using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace FamilyTreeApp.Core
{
    /// <summary>
    /// Layout engine for automatic family tree positioning.
    /// Uses a hybrid force-directed + hierarchy approach.
    /// </summary>
    public class LayoutEngine
    {
        public enum AlignmentMode
        {
            TopDown,    // Generations horizontal, children below
            LeftRight   // Generations vertical, children to right
        }

        // Layout parameters
        private const double NodeWidth = 140;
        private const double NodeHeight = 70;
        private const double HorizontalSpacing = 80;
        private const double VerticalSpacing = 120;
        private const double GenerationSpacing = 180;
        private const double PartnerSpacing = 20; // Close spacing between partners

        // Force-directed parameters
        private const double RepulsionForce = 5000;
        private const double AttractionForce = 0.01;
        private const double Damping = 0.85;
        private const int MaxIterations = 100;
        private const double MinMovement = 0.5;

        public AlignmentMode Alignment { get; set; } = AlignmentMode.TopDown;

        /// <summary>
        /// Applies automatic layout to the family tree.
        /// </summary>
        public void ApplyLayout(FamilyTree tree)
        {
            if (tree.Nodes.Count == 0) return;

            // Step 1: Assign generation levels
            var generations = AssignGenerations(tree);

            // Step 2: Build family structure and layout
            LayoutFamilyTree(tree, generations);

            // Step 3: Center the tree
            CenterTree(tree);
        }

        /// <summary>
        /// Lays out the family tree with proper parent-child-partner positioning.
        /// </summary>
        private void LayoutFamilyTree(FamilyTree tree, Dictionary<string, int> generations)
        {
            // Group nodes by generation
            var genGroups = generations
                .GroupBy(kvp => kvp.Value)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Select(kvp => tree.Nodes.FirstOrDefault(n => n.Id == kvp.Key)).Where(n => n != null).ToList());

            // Find partner pairs
            var partnerPairs = new Dictionary<string, string>(); // nodeId -> partnerId
            foreach (var conn in tree.Connections.Where(c => 
                c.ConnectionType == ConnectionType.Partner || c.ConnectionType == ConnectionType.FormerPartner))
            {
                partnerPairs[conn.FromNodeId] = conn.ToNodeId;
                partnerPairs[conn.ToNodeId] = conn.FromNodeId;
            }

            // Track which nodes have been positioned
            var positioned = new HashSet<string>();
            
            // Position each generation
            foreach (var genKvp in genGroups.OrderBy(g => g.Key))
            {
                int gen = genKvp.Key;
                var nodesInGen = genKvp.Value;
                
                // Calculate Y position for this generation
                double genY = Alignment == AlignmentMode.TopDown 
                    ? 100 + gen * GenerationSpacing 
                    : 100;
                double genX = Alignment == AlignmentMode.TopDown 
                    ? 100 
                    : 100 + gen * GenerationSpacing;

                // Group nodes: partner pairs together, singles alone
                var groups = new List<List<Node>>();
                var processed = new HashSet<string>();

                foreach (var node in nodesInGen)
                {
                    if (node == null || processed.Contains(node.Id)) continue;
                    
                    var group = new List<Node> { node };
                    processed.Add(node.Id);
                    
                    // Check if this node has a partner in the same generation
                    if (partnerPairs.TryGetValue(node.Id, out var partnerId))
                    {
                        var partner = nodesInGen.FirstOrDefault(n => n?.Id == partnerId);
                        if (partner != null && !processed.Contains(partner.Id))
                        {
                            group.Add(partner);
                            processed.Add(partner.Id);
                        }
                    }
                    
                    groups.Add(group);
                }

                // Position groups with spacing
                double currentX = genX;
                double currentY = genY;
                
                foreach (var group in groups)
                {
                    // Check if this group's children should influence positioning
                    Point? childrenCenter = GetChildrenCenter(tree, group, generations, gen);
                    
                    if (childrenCenter.HasValue)
                    {
                        if (Alignment == AlignmentMode.TopDown)
                        {
                            // Center parents above their children (horizontally)
                            double groupWidth = group.Count * NodeWidth + (group.Count - 1) * HorizontalSpacing;
                            currentX = childrenCenter.Value.X - groupWidth / 2 + NodeWidth / 2;
                        }
                        else
                        {
                            // Center parents left of their children (vertically)
                            // Partners are close together with PartnerSpacing
                            double groupHeight = group.Count * NodeHeight + (group.Count - 1) * PartnerSpacing;
                            currentY = childrenCenter.Value.Y - groupHeight / 2 + NodeHeight / 2;
                        }
                    }

                    foreach (var node in group)
                    {
                        if (node == null) continue;
                        
                        if (Alignment == AlignmentMode.TopDown)
                        {
                            node.Position = new Point(currentX, genY);
                            currentX += NodeWidth + HorizontalSpacing;
                        }
                        else
                        {
                            // Left-Right: partners stacked vertically at same X, close together
                            node.Position = new Point(genX, currentY);
                            currentY += NodeHeight + PartnerSpacing;
                        }
                        positioned.Add(node.Id);
                    }
                    
                    // Add extra spacing between groups (different families)
                    if (Alignment == AlignmentMode.TopDown)
                        currentX += HorizontalSpacing;
                    else
                        currentY += VerticalSpacing - PartnerSpacing; // Extra spacing between family groups
                }
            }

            // Second pass: center children relative to their parents
            CenterChildrenRelativeToParents(tree, generations);
        }

        /// <summary>
        /// Gets the center position of children for a group of parents.
        /// </summary>
        private Point? GetChildrenCenter(FamilyTree tree, List<Node> parents, Dictionary<string, int> generations, int parentGen)
        {
            var childIds = new HashSet<string>();
            foreach (var parent in parents)
            {
                if (parent == null) continue;
                var children = tree.Connections
                    .Where(c => c.FromNodeId == parent.Id && 
                           (c.ConnectionType == ConnectionType.Biological || 
                            c.ConnectionType == ConnectionType.Adopted || 
                            c.ConnectionType == ConnectionType.Step))
                    .Select(c => c.ToNodeId);
                foreach (var childId in children)
                    childIds.Add(childId);
            }

            if (childIds.Count == 0) return null;

            var childNodes = tree.Nodes.Where(n => childIds.Contains(n.Id)).ToList();
            if (childNodes.Count == 0) return null;

            double avgX = childNodes.Average(n => n.Position.X);
            double avgY = childNodes.Average(n => n.Position.Y);
            return new Point(avgX, avgY);
        }

        /// <summary>
        /// Centers children relative to their parents after initial layout.
        /// </summary>
        private void CenterChildrenRelativeToParents(FamilyTree tree, Dictionary<string, int> generations)
        {
            // Group children by their parent pair
            var parentToChildren = new Dictionary<string, List<Node>>();
            
            foreach (var conn in tree.Connections.Where(c => 
                c.ConnectionType == ConnectionType.Biological || 
                c.ConnectionType == ConnectionType.Adopted || 
                c.ConnectionType == ConnectionType.Step))
            {
                var parent = tree.Nodes.FirstOrDefault(n => n.Id == conn.FromNodeId);
                var child = tree.Nodes.FirstOrDefault(n => n.Id == conn.ToNodeId);
                if (parent == null || child == null) continue;

                // Use parent pair key
                var partnerConn = tree.Connections.FirstOrDefault(c => 
                    (c.FromNodeId == parent.Id || c.ToNodeId == parent.Id) &&
                    (c.ConnectionType == ConnectionType.Partner || c.ConnectionType == ConnectionType.FormerPartner));
                
                string parentKey = parent.Id;
                if (partnerConn != null)
                {
                    var p1 = partnerConn.FromNodeId;
                    var p2 = partnerConn.ToNodeId;
                    parentKey = string.Compare(p1, p2) < 0 ? $"{p1}_{p2}" : $"{p2}_{p1}";
                }

                if (!parentToChildren.ContainsKey(parentKey))
                    parentToChildren[parentKey] = new List<Node>();
                
                if (!parentToChildren[parentKey].Contains(child))
                    parentToChildren[parentKey].Add(child);
            }

            // For each parent pair, center children relative to parents
            foreach (var kvp in parentToChildren)
            {
                var children = kvp.Value;
                if (children.Count == 0) continue;

                // Find the parents
                var parentIds = kvp.Key.Split('_');
                var parents = tree.Nodes.Where(n => parentIds.Contains(n.Id)).ToList();
                if (parents.Count == 0) continue;

                if (Alignment == AlignmentMode.TopDown)
                {
                    // Order children by X and center horizontally under parents
                    children = children.OrderBy(c => c.Position.X).ToList();
                    double parentCenterX = parents.Average(p => p.Position.X + NodeWidth / 2);
                    double childrenWidth = children.Count * NodeWidth + (children.Count - 1) * HorizontalSpacing;
                    double childStartX = parentCenterX - childrenWidth / 2;

                    for (int i = 0; i < children.Count; i++)
                    {
                        var child = children[i];
                        child.Position = new Point(
                            childStartX + i * (NodeWidth + HorizontalSpacing),
                            child.Position.Y);
                    }
                }
                else
                {
                    // Order children by Y and center vertically to the right of parents
                    children = children.OrderBy(c => c.Position.Y).ToList();
                    double parentCenterY = parents.Average(p => p.Position.Y + NodeHeight / 2);
                    double childrenHeight = children.Count * NodeHeight + (children.Count - 1) * VerticalSpacing;
                    double childStartY = parentCenterY - childrenHeight / 2;

                    for (int i = 0; i < children.Count; i++)
                    {
                        var child = children[i];
                        child.Position = new Point(
                            child.Position.X,
                            childStartY + i * (NodeHeight + VerticalSpacing));
                    }
                }
            }
        }

        /// <summary>
        /// Assigns generation levels to each node.
        /// Root nodes (no parents) are generation 0.
        /// </summary>
        private Dictionary<string, int> AssignGenerations(FamilyTree tree)
        {
            var generations = new Dictionary<string, int>();
            var visited = new HashSet<string>();

            // Find root nodes (nodes with no parents)
            var childNodes = new HashSet<string>();
            foreach (var conn in tree.Connections)
            {
                if (conn.ConnectionType == ConnectionType.Biological ||
                    conn.ConnectionType == ConnectionType.Adopted ||
                    conn.ConnectionType == ConnectionType.Step)
                {
                    childNodes.Add(conn.ToNodeId);
                }
            }

            var rootNodes = tree.Nodes.Where(n => !childNodes.Contains(n.Id)).ToList();

            // If no clear roots, use the first node
            if (rootNodes.Count == 0 && tree.Nodes.Count > 0)
            {
                rootNodes.Add(tree.Nodes.First());
            }

            // BFS to assign generations
            var queue = new Queue<(Node node, int gen)>();
            foreach (var root in rootNodes)
            {
                queue.Enqueue((root, 0));
                generations[root.Id] = 0;
            }

            while (queue.Count > 0)
            {
                var (node, gen) = queue.Dequeue();
                if (visited.Contains(node.Id)) continue;
                visited.Add(node.Id);

                generations[node.Id] = gen;

                // Find children
                var childConnections = tree.Connections
                    .Where(c => c.FromNodeId == node.Id &&
                               (c.ConnectionType == ConnectionType.Biological ||
                                c.ConnectionType == ConnectionType.Adopted ||
                                c.ConnectionType == ConnectionType.Step));

                foreach (var conn in childConnections)
                {
                    var childNode = tree.Nodes.FirstOrDefault(n => n.Id == conn.ToNodeId);
                    if (childNode != null && !visited.Contains(childNode.Id))
                    {
                        queue.Enqueue((childNode, gen + 1));
                    }
                }

                // Also handle partner connections (same generation)
                var partnerConnections = tree.Connections
                    .Where(c => (c.FromNodeId == node.Id || c.ToNodeId == node.Id) &&
                               (c.ConnectionType == ConnectionType.Partner ||
                                c.ConnectionType == ConnectionType.FormerPartner));

                foreach (var conn in partnerConnections)
                {
                    var partnerId = conn.FromNodeId == node.Id ? conn.ToNodeId : conn.FromNodeId;
                    var partner = tree.Nodes.FirstOrDefault(n => n.Id == partnerId);
                    if (partner != null && !visited.Contains(partner.Id))
                    {
                        generations[partner.Id] = gen;
                        queue.Enqueue((partner, gen));
                    }
                }
            }

            // Assign remaining unvisited nodes
            foreach (var node in tree.Nodes)
            {
                if (!generations.ContainsKey(node.Id))
                {
                    generations[node.Id] = 0;
                }
            }

            return generations;
        }

        /// <summary>
        /// Centers the tree in the visible area.
        /// </summary>
        private void CenterTree(FamilyTree tree)
        {
            if (tree.Nodes.Count == 0) return;

            var minX = tree.Nodes.Min(n => n.Position.X);
            var minY = tree.Nodes.Min(n => n.Position.Y);

            // Shift all nodes to start from (100, 100)
            var offsetX = 100 - minX;
            var offsetY = 100 - minY;

            foreach (var node in tree.Nodes)
            {
                if (!node.IsLocked)
                {
                    node.Position = new Point(
                        node.Position.X + offsetX,
                        node.Position.Y + offsetY);
                }
            }
        }

        /// <summary>
        /// Quick repositioning for adding a single node.
        /// </summary>
        public void PositionNewNode(FamilyTree tree, Node newNode, Node? relatedNode, ConnectionType connectionType)
        {
            if (relatedNode == null)
            {
                // Position at center
                newNode.Position = new Point(400, 300);
                return;
            }

            double offsetX = 0, offsetY = 0;

            switch (connectionType)
            {
                case ConnectionType.Biological:
                case ConnectionType.Adopted:
                case ConnectionType.Step:
                    // Child - position below
                    if (Alignment == AlignmentMode.TopDown)
                    {
                        offsetY = GenerationSpacing;
                    }
                    else
                    {
                        offsetX = GenerationSpacing;
                    }
                    break;

                case ConnectionType.Partner:
                case ConnectionType.FormerPartner:
                    // Partner - position to the side
                    if (Alignment == AlignmentMode.TopDown)
                    {
                        offsetX = NodeWidth + HorizontalSpacing;
                    }
                    else
                    {
                        offsetY = NodeHeight + VerticalSpacing;
                    }
                    break;

                default:
                    offsetX = NodeWidth + HorizontalSpacing;
                    break;
            }

            newNode.Position = new Point(
                relatedNode.Position.X + offsetX,
                relatedNode.Position.Y + offsetY);
        }
    }
}
