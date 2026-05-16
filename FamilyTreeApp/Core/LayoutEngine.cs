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
        /// Uses a two-pass approach inspired by Sugiyama:
        ///   Pass 1 – place each generation, ordering nodes by parents' positions to minimise crossings.
        ///   Pass 2 – centre children under/beside their parents (repeated until stable).
        /// </summary>
        private void LayoutFamilyTree(FamilyTree tree, Dictionary<string, int> generations)
        {
            bool isTopDown = Alignment == AlignmentMode.TopDown;

            // ── Build lookup maps ──────────────────────────────────────────────────────
            // partnerOf[id] = id of partner
            var partnerOf = new Dictionary<string, string>();
            foreach (var c in tree.Connections.Where(c =>
                c.ConnectionType == ConnectionType.Partner ||
                c.ConnectionType == ConnectionType.FormerPartner))
            {
                partnerOf[c.FromNodeId] = c.ToNodeId;
                partnerOf[c.ToNodeId] = c.FromNodeId;
            }

            // parentsOf[childId] = list of parent node ids
            var parentsOf = new Dictionary<string, List<string>>();
            // childrenOf[parentId] = list of child node ids
            var childrenOf = new Dictionary<string, List<string>>();
            foreach (var c in tree.Connections.Where(c =>
                c.ConnectionType == ConnectionType.Biological ||
                c.ConnectionType == ConnectionType.Adopted ||
                c.ConnectionType == ConnectionType.Step))
            {
                if (!parentsOf.ContainsKey(c.ToNodeId)) parentsOf[c.ToNodeId] = new List<string>();
                parentsOf[c.ToNodeId].Add(c.FromNodeId);

                if (!childrenOf.ContainsKey(c.FromNodeId)) childrenOf[c.FromNodeId] = new List<string>();
                childrenOf[c.FromNodeId].Add(c.ToNodeId);
            }

            // ── Group nodes by generation ──────────────────────────────────────────────
            var genGroups = new SortedDictionary<int, List<Node>>();
            foreach (var kvp in generations)
            {
                var node = tree.Nodes.FirstOrDefault(n => n.Id == kvp.Key);
                if (node == null) continue;
                if (!genGroups.ContainsKey(kvp.Value)) genGroups[kvp.Value] = new List<Node>();
                genGroups[kvp.Value].Add(node);
            }

            if (genGroups.Count == 0) return;

            // ── Pass 1: Position generation by generation ──────────────────────────────
            // For each generation, order nodes by their parents' average axis position
            // (or by their position in tree.Nodes for gen 0), then group partners adjacently.
            foreach (var kvp in genGroups)
            {
                int gen = kvp.Key;
                var nodes = kvp.Value;

                // Order nodes in this generation to minimise crossings
                var orderedNodes = OrderNodesByParentAxis(nodes, parentsOf, isTopDown);

                // Build adjacent partner groups: keep partners next to each other
                var groups = BuildPartnerGroups(orderedNodes, partnerOf);

                // Calculate the fixed axis for this generation
                double fixedAxis = 100.0 + gen * GenerationSpacing;
                double cursor = 100.0;

                foreach (var group in groups)
                {
                    for (int i = 0; i < group.Count; i++)
                    {
                        var node = group[i];
                        if (node == null) continue;
                        bool isLastInGroup = i == group.Count - 1;

                        if (isTopDown)
                        {
                            node.Position = new Point(cursor, fixedAxis);
                            cursor += NodeWidth + (isLastInGroup ? HorizontalSpacing : PartnerSpacing);
                        }
                        else
                        {
                            node.Position = new Point(fixedAxis, cursor);
                            cursor += NodeHeight + (isLastInGroup ? VerticalSpacing : PartnerSpacing);
                        }
                    }
                    // Extra gap between distinct family groups
                    if (isTopDown)
                        cursor += HorizontalSpacing * 0.5;
                    else
                        cursor += VerticalSpacing * 0.5;
                }
            }

            // ── Pass 2: Centre children under/beside parents (up to 3 iterations) ─────
            for (int iter = 0; iter < 3; iter++)
            {
                CenterChildrenUnderParents(tree, genGroups, childrenOf, partnerOf, isTopDown);
            }
        }

        /// <summary>
        /// Orders nodes in a generation by the average axis position of their parents
        /// so that sibling lines don't cross.  Nodes without parents come last.
        /// </summary>
        private List<Node> OrderNodesByParentAxis(
            List<Node> nodes,
            Dictionary<string, List<string>> parentsOf,
            bool isTopDown)
        {
            return nodes
                .OrderBy(n =>
                {
                    if (!parentsOf.TryGetValue(n.Id, out var pids) || pids.Count == 0)
                        return double.MaxValue; // roots / partnerless nodes go at end of sort
                    var parentNodes = pids
                        .Select(pid => nodes.FirstOrDefault(x => x.Id == pid)
                                       ?? new Node { Position = new Point(0, 0) })
                        .ToList();
                    return isTopDown
                        ? parentNodes.Average(p => p.Position.X)
                        : parentNodes.Average(p => p.Position.Y);
                })
                .ToList();
        }

        /// <summary>
        /// Given an ordered list of nodes, groups partners adjacent to each other.
        /// Each group is a list of 1 or 2 nodes (a person + their partner if in the same generation).
        /// </summary>
        private List<List<Node>> BuildPartnerGroups(List<Node> orderedNodes, Dictionary<string, string> partnerOf)
        {
            var groups = new List<List<Node>>();
            var placed = new HashSet<string>();

            foreach (var node in orderedNodes)
            {
                if (placed.Contains(node.Id)) continue;
                placed.Add(node.Id);

                var group = new List<Node> { node };

                if (partnerOf.TryGetValue(node.Id, out var partnerId))
                {
                    // Find the partner in the same generation
                    var partner = orderedNodes.FirstOrDefault(n => n.Id == partnerId && !placed.Contains(n.Id));
                    if (partner != null)
                    {
                        placed.Add(partner.Id);
                        group.Add(partner);
                    }
                }

                groups.Add(group);
            }

            return groups;
        }

        /// <summary>
        /// Centres each group of children under/beside their parents.
        /// Iterates from the deepest generation upward so that repositioning
        /// propagates correctly through multiple levels.
        /// </summary>
        private void CenterChildrenUnderParents(
            FamilyTree tree,
            SortedDictionary<int, List<Node>> genGroups,
            Dictionary<string, List<string>> childrenOf,
            Dictionary<string, string> partnerOf,
            bool isTopDown)
        {
            // Build parent-pair → children mapping
            // Key: canonical partner-pair key (or single parent id if no partner)
            var familyChildren = new Dictionary<string, List<string>>();

            foreach (var kvp in childrenOf)
            {
                var parentId = kvp.Key;
                // Canonical key for this parent (+ their partner if any)
                string familyKey = parentId;
                if (partnerOf.TryGetValue(parentId, out var partnerId))
                {
                    familyKey = string.Compare(parentId, partnerId, StringComparison.Ordinal) < 0
                        ? $"{parentId}_{partnerId}"
                        : $"{partnerId}_{parentId}";
                }

                if (!familyChildren.ContainsKey(familyKey))
                    familyChildren[familyKey] = new List<string>();

                foreach (var childId in kvp.Value)
                    if (!familyChildren[familyKey].Contains(childId))
                        familyChildren[familyKey].Add(childId);
            }

            foreach (var kvp in familyChildren)
            {
                var childIds = kvp.Value;
                var childNodes = childIds
                    .Select(id => tree.Nodes.FirstOrDefault(n => n.Id == id))
                    .Where(n => n != null).Cast<Node>()
                    .OrderBy(n => isTopDown ? n.Position.X : n.Position.Y)
                    .ToList();
                if (childNodes.Count == 0) continue;

                // Find parents
                var parentIds = kvp.Key.Split('_');
                var parents = parentIds
                    .Select(id => tree.Nodes.FirstOrDefault(n => n.Id == id))
                    .Where(n => n != null).Cast<Node>()
                    .ToList();
                if (parents.Count == 0) continue;

                if (isTopDown)
                {
                    // Centre of parent pair
                    double parentCentreX = parents.Average(p => p.Position.X + NodeWidth / 2.0);

                    // Spread children evenly and centre them
                    double totalChildWidth = childNodes.Count * NodeWidth
                                          + (childNodes.Count - 1) * HorizontalSpacing;
                    double startX = parentCentreX - totalChildWidth / 2.0;

                    // Move children with external partners to the outermost positions
                    // so their partner line doesn't cross siblings.
                    var siblingIds = new HashSet<string>(childNodes.Select(n => n.Id));
                    var childrenWithExternalPartner = childNodes
                        .Where(n => partnerOf.TryGetValue(n.Id, out var pid) && !siblingIds.Contains(pid))
                        .ToList();

                    if (childrenWithExternalPartner.Any())
                    {
                        // Remove them, then re-insert at edges
                        foreach (var cwp in childrenWithExternalPartner)
                            childNodes.Remove(cwp);

                        // Place children whose partner is to the right at the right end, others at left
                        var toRight = childrenWithExternalPartner
                            .Where(n => partnerOf.TryGetValue(n.Id, out var pid) &&
                                        tree.Nodes.FirstOrDefault(x => x.Id == pid) is Node pn &&
                                        pn.Position.X >= n.Position.X)
                            .ToList();
                        var toLeft = childrenWithExternalPartner.Except(toRight).ToList();

                        foreach (var n in toLeft) childNodes.Insert(0, n);
                        foreach (var n in toRight) childNodes.Add(n);

                        // Recalculate spread
                        totalChildWidth = childNodes.Count * NodeWidth
                                        + (childNodes.Count - 1) * HorizontalSpacing;
                        startX = parentCentreX - totalChildWidth / 2.0;
                    }

                    // Preserve Y (generation row) — only move X
                    for (int i = 0; i < childNodes.Count; i++)
                    {
                        var child = childNodes[i];
                        child.Position = new Point(
                            startX + i * (NodeWidth + HorizontalSpacing),
                            child.Position.Y);
                    }
                }
                else
                {
                    // Centre of parent pair (Y axis in LeftRight mode)
                    double parentCentreY = parents.Average(p => p.Position.Y + NodeHeight / 2.0);

                    double totalChildHeight = childNodes.Count * NodeHeight
                                           + (childNodes.Count - 1) * VerticalSpacing;
                    double startY = parentCentreY - totalChildHeight / 2.0;

                    // Move children with external partners to outermost positions
                    var siblingIds = new HashSet<string>(childNodes.Select(n => n.Id));
                    var childrenWithExternalPartner = childNodes
                        .Where(n => partnerOf.TryGetValue(n.Id, out var pid) && !siblingIds.Contains(pid))
                        .ToList();

                    if (childrenWithExternalPartner.Any())
                    {
                        foreach (var cwp in childrenWithExternalPartner)
                            childNodes.Remove(cwp);

                        var toBottom = childrenWithExternalPartner
                            .Where(n => partnerOf.TryGetValue(n.Id, out var pid) &&
                                        tree.Nodes.FirstOrDefault(x => x.Id == pid) is Node pn &&
                                        pn.Position.Y >= n.Position.Y)
                            .ToList();
                        var toTop = childrenWithExternalPartner.Except(toBottom).ToList();

                        foreach (var n in toTop) childNodes.Insert(0, n);
                        foreach (var n in toBottom) childNodes.Add(n);

                        totalChildHeight = childNodes.Count * NodeHeight
                                         + (childNodes.Count - 1) * VerticalSpacing;
                        startY = parentCentreY - totalChildHeight / 2.0;
                    }

                    // Preserve X (generation column) — only move Y
                    for (int i = 0; i < childNodes.Count; i++)
                    {
                        var child = childNodes[i];
                        child.Position = new Point(
                            child.Position.X,
                            startY + i * (NodeHeight + VerticalSpacing));
                    }
                }
            }
        }

        /// <summary>
        /// Assigns generation levels to each node.
        /// Root nodes (no parents) are generation 0.
        /// Partners are always in the same generation (reconciled to the maximum).
        /// </summary>
        private Dictionary<string, int> AssignGenerations(FamilyTree tree)
        {
            var generations = new Dictionary<string, int>();
            var visited = new HashSet<string>();

            // Find root nodes: nodes that are never a "child" (ToNode) in a parent-child connection
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

            // True roots: not a child of anyone
            var rootNodes = tree.Nodes.Where(n => !childNodes.Contains(n.Id)).ToList();

            if (rootNodes.Count == 0 && tree.Nodes.Count > 0)
                rootNodes.Add(tree.Nodes.First());

            // BFS from roots following parent→child and partner edges
            var queue = new Queue<(Node node, int gen)>();
            foreach (var root in rootNodes)
            {
                if (!generations.ContainsKey(root.Id))
                {
                    generations[root.Id] = 0;
                    queue.Enqueue((root, 0));
                }
            }

            while (queue.Count > 0)
            {
                var (node, gen) = queue.Dequeue();
                if (visited.Contains(node.Id)) continue;
                visited.Add(node.Id);

                // Propagate to children (gen + 1)
                foreach (var conn in tree.Connections.Where(c =>
                    c.FromNodeId == node.Id &&
                    (c.ConnectionType == ConnectionType.Biological ||
                     c.ConnectionType == ConnectionType.Adopted ||
                     c.ConnectionType == ConnectionType.Step)))
                {
                    var child = tree.Nodes.FirstOrDefault(n => n.Id == conn.ToNodeId);
                    if (child != null && !visited.Contains(child.Id))
                    {
                        // Assign max to avoid overwriting a deeper assignment
                        int newGen = gen + 1;
                        if (!generations.TryGetValue(child.Id, out int existing) || existing < newGen)
                            generations[child.Id] = newGen;
                        queue.Enqueue((child, newGen));
                    }
                }

                // Propagate to partners (same gen)
                foreach (var conn in tree.Connections.Where(c =>
                    (c.FromNodeId == node.Id || c.ToNodeId == node.Id) &&
                    (c.ConnectionType == ConnectionType.Partner ||
                     c.ConnectionType == ConnectionType.FormerPartner)))
                {
                    var partnerId = conn.FromNodeId == node.Id ? conn.ToNodeId : conn.FromNodeId;
                    var partner = tree.Nodes.FirstOrDefault(n => n.Id == partnerId);
                    if (partner != null && !visited.Contains(partner.Id))
                    {
                        if (!generations.TryGetValue(partner.Id, out int existing) || existing < gen)
                            generations[partner.Id] = gen;
                        queue.Enqueue((partner, gen));
                    }
                }
            }

            // Catch any nodes not reached by BFS
            foreach (var node in tree.Nodes)
            {
                if (!generations.ContainsKey(node.Id))
                    generations[node.Id] = 0;
            }

            // --- Reconciliation pass ---
            // Partners must be at the same generation.
            // A partner added to a child (with no parents of their own) starts at gen 0
            // but should be elevated to match their partner's generation.
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var conn in tree.Connections.Where(c =>
                    c.ConnectionType == ConnectionType.Partner ||
                    c.ConnectionType == ConnectionType.FormerPartner))
                {
                    if (generations.TryGetValue(conn.FromNodeId, out int g1) &&
                        generations.TryGetValue(conn.ToNodeId, out int g2))
                    {
                        int target = Math.Max(g1, g2);
                        if (g1 != target) { generations[conn.FromNodeId] = target; changed = true; }
                        if (g2 != target) { generations[conn.ToNodeId] = target; changed = true; }
                    }
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
