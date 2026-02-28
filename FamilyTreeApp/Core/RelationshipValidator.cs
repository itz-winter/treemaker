using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyTreeApp.Core
{
    /// <summary>
    /// Validation result for relationship checks.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<ValidationWarning> Warnings { get; set; } = new();
        public List<ValidationError> Errors { get; set; } = new();

        public bool HasWarnings => Warnings.Count > 0;
        public bool HasErrors => Errors.Count > 0;
    }

    /// <summary>
    /// A warning that doesn't prevent the action but should be displayed.
    /// </summary>
    public class ValidationWarning
    {
        public string Message { get; set; } = "";
        public string NodeId { get; set; } = "";
        public WarningType Type { get; set; }
    }

    /// <summary>
    /// An error that prevents the action.
    /// </summary>
    public class ValidationError
    {
        public string Message { get; set; } = "";
        public string NodeId { get; set; } = "";
        public ErrorType Type { get; set; }
    }

    public enum WarningType
    {
        Incest,
        Threesome,
        SelfReference,
        DuplicateConnection
    }

    public enum ErrorType
    {
        IncestNotAllowed,
        ThreesomeNotAllowed,
        SelfReference,
        CyclicRelationship
    }

    /// <summary>
    /// Validates relationships in the family tree based on rules.
    /// </summary>
    public class RelationshipValidator
    {
        private readonly FamilyTree _tree;

        public RelationshipValidator(FamilyTree tree)
        {
            _tree = tree;
        }

        /// <summary>
        /// Validates whether a new connection can be created.
        /// </summary>
        public ValidationResult ValidateNewConnection(string fromNodeId, string toNodeId, ConnectionType connectionType)
        {
            var result = new ValidationResult();

            // Check self-reference
            if (fromNodeId == toNodeId)
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Message = "Cannot create a connection from a node to itself.",
                    NodeId = fromNodeId,
                    Type = ErrorType.SelfReference
                });
                return result;
            }

            // Check for duplicate connection
            var existingConnection = _tree.Connections.FirstOrDefault(c =>
                (c.FromNodeId == fromNodeId && c.ToNodeId == toNodeId) ||
                (c.FromNodeId == toNodeId && c.ToNodeId == fromNodeId));

            if (existingConnection != null)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Message = "A connection already exists between these nodes.",
                    NodeId = fromNodeId,
                    Type = WarningType.DuplicateConnection
                });
            }

            // Check for incest (if it's a partner connection)
            if (connectionType == ConnectionType.Partner || connectionType == ConnectionType.FormerPartner)
            {
                if (AreRelated(fromNodeId, toNodeId))
                {
                    if (!_tree.AllowIncest)
                    {
                        result.IsValid = false;
                        result.Errors.Add(new ValidationError
                        {
                            Message = "Incest is not allowed. These nodes are blood relatives.",
                            NodeId = fromNodeId,
                            Type = ErrorType.IncestNotAllowed
                        });
                    }
                    else
                    {
                        result.Warnings.Add(new ValidationWarning
                        {
                            Message = "Warning: These nodes are blood relatives (incest).",
                            NodeId = fromNodeId,
                            Type = WarningType.Incest
                        });
                    }
                }
            }

            // Check for threesome+ (multiple partners)
            if (connectionType == ConnectionType.Partner)
            {
                var existingPartners = GetPartners(fromNodeId);
                var toNodePartners = GetPartners(toNodeId);

                if (existingPartners.Count > 0 || toNodePartners.Count > 0)
                {
                    if (!_tree.AllowThreesome)
                    {
                        result.IsValid = false;
                        result.Errors.Add(new ValidationError
                        {
                            Message = "Multiple partners (threesome+) is not allowed.",
                            NodeId = fromNodeId,
                            Type = ErrorType.ThreesomeNotAllowed
                        });
                    }
                    else
                    {
                        result.Warnings.Add(new ValidationWarning
                        {
                            Message = "Warning: This creates a multi-partner relationship.",
                            NodeId = fromNodeId,
                            Type = WarningType.Threesome
                        });
                    }
                }
            }

            // Check for cyclic parent-child relationships
            if (connectionType == ConnectionType.Biological ||
                connectionType == ConnectionType.Adopted ||
                connectionType == ConnectionType.Step)
            {
                if (IsAncestorOf(toNodeId, fromNodeId))
                {
                    result.IsValid = false;
                    result.Errors.Add(new ValidationError
                    {
                        Message = "Cannot create cyclic relationship: the child is already an ancestor of the parent.",
                        NodeId = fromNodeId,
                        Type = ErrorType.CyclicRelationship
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Gets all current partners of a node.
        /// </summary>
        public List<string> GetPartners(string nodeId)
        {
            return _tree.Connections
                .Where(c => c.ConnectionType == ConnectionType.Partner &&
                           (c.FromNodeId == nodeId || c.ToNodeId == nodeId))
                .Select(c => c.FromNodeId == nodeId ? c.ToNodeId : c.FromNodeId)
                .ToList();
        }

        /// <summary>
        /// Checks if two nodes are blood relatives.
        /// </summary>
        public bool AreRelated(string nodeId1, string nodeId2)
        {
            // Check if they share a common ancestor or one is ancestor of the other
            var ancestors1 = GetAllAncestors(nodeId1);
            var ancestors2 = GetAllAncestors(nodeId2);

            // Check if one is ancestor of the other
            if (ancestors1.Contains(nodeId2) || ancestors2.Contains(nodeId1))
                return true;

            // Check for common ancestors (siblings, cousins, etc.)
            return ancestors1.Intersect(ancestors2).Any();
        }

        /// <summary>
        /// Gets all ancestors of a node (biological only).
        /// </summary>
        public HashSet<string> GetAllAncestors(string nodeId)
        {
            var ancestors = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(nodeId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                
                // Find biological parents
                var parents = _tree.Connections
                    .Where(c => c.ToNodeId == current && c.ConnectionType == ConnectionType.Biological)
                    .Select(c => c.FromNodeId);

                foreach (var parent in parents)
                {
                    if (!ancestors.Contains(parent))
                    {
                        ancestors.Add(parent);
                        queue.Enqueue(parent);
                    }
                }
            }

            return ancestors;
        }

        /// <summary>
        /// Checks if nodeA is an ancestor of nodeB.
        /// </summary>
        public bool IsAncestorOf(string nodeA, string nodeB)
        {
            var ancestors = GetAllAncestors(nodeB);
            return ancestors.Contains(nodeA);
        }

        /// <summary>
        /// Gets all descendants of a node.
        /// </summary>
        public HashSet<string> GetAllDescendants(string nodeId)
        {
            var descendants = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(nodeId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                
                // Find children
                var children = _tree.Connections
                    .Where(c => c.FromNodeId == current && 
                               (c.ConnectionType == ConnectionType.Biological ||
                                c.ConnectionType == ConnectionType.Adopted ||
                                c.ConnectionType == ConnectionType.Step))
                    .Select(c => c.ToNodeId);

                foreach (var child in children)
                {
                    if (!descendants.Contains(child))
                    {
                        descendants.Add(child);
                        queue.Enqueue(child);
                    }
                }
            }

            return descendants;
        }

        /// <summary>
        /// Validates the entire tree and returns all warnings.
        /// </summary>
        public ValidationResult ValidateTree()
        {
            var result = new ValidationResult();

            // Check each connection for issues
            foreach (var connection in _tree.Connections)
            {
                var connResult = ValidateExistingConnection(connection);
                result.Warnings.AddRange(connResult.Warnings);
            }

            return result;
        }

        /// <summary>
        /// Validates an existing connection and returns any warnings.
        /// </summary>
        private ValidationResult ValidateExistingConnection(Connection connection)
        {
            var result = new ValidationResult();

            // Check for incest in partner relationships
            if (connection.ConnectionType == ConnectionType.Partner ||
                connection.ConnectionType == ConnectionType.FormerPartner)
            {
                if (AreRelated(connection.FromNodeId, connection.ToNodeId))
                {
                    result.Warnings.Add(new ValidationWarning
                    {
                        Message = "Incestuous relationship detected.",
                        NodeId = connection.FromNodeId,
                        Type = WarningType.Incest
                    });
                }
            }

            return result;
        }
    }
}
