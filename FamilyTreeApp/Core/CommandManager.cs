using System;
using System.Collections.Generic;

namespace FamilyTreeApp.Core
{
    /// <summary>
    /// Interface for undoable commands.
    /// </summary>
    public interface ICommand
    {
        void Execute();
        void Undo();
        string Description { get; }
    }

    /// <summary>
    /// Manages undo/redo operations.
    /// </summary>
    public class CommandManager
    {
        private readonly Stack<ICommand> _undoStack = new();
        private readonly Stack<ICommand> _redoStack = new();
        private const int MaxUndoLevels = 50;

        public event EventHandler? StateChanged;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public string? UndoDescription => CanUndo ? _undoStack.Peek().Description : null;
        public string? RedoDescription => CanRedo ? _redoStack.Peek().Description : null;

        /// <summary>
        /// Executes a command and adds it to the undo stack.
        /// </summary>
        public void Execute(ICommand command)
        {
            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear();

            // Limit undo levels
            if (_undoStack.Count > MaxUndoLevels)
            {
                var temp = new Stack<ICommand>();
                for (int i = 0; i < MaxUndoLevels; i++)
                {
                    temp.Push(_undoStack.Pop());
                }
                _undoStack.Clear();
                while (temp.Count > 0)
                {
                    _undoStack.Push(temp.Pop());
                }
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Undoes the last command.
        /// </summary>
        public void Undo()
        {
            if (CanUndo)
            {
                var command = _undoStack.Pop();
                command.Undo();
                _redoStack.Push(command);
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Redoes the last undone command.
        /// </summary>
        public void Redo()
        {
            if (CanRedo)
            {
                var command = _redoStack.Pop();
                command.Execute();
                _undoStack.Push(command);
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Clears all undo/redo history.
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    #region Command Implementations

    /// <summary>
    /// Command to add a node.
    /// </summary>
    public class AddNodeCommand : ICommand
    {
        private readonly FamilyTree _tree;
        private readonly Node _node;

        public string Description => $"Add node: {_node.Name}";

        public AddNodeCommand(FamilyTree tree, Node node)
        {
            _tree = tree;
            _node = node;
        }

        public void Execute()
        {
            if (!_tree.Nodes.Contains(_node))
                _tree.Nodes.Add(_node);
        }

        public void Undo()
        {
            _tree.Nodes.Remove(_node);
        }
    }

    /// <summary>
    /// Command to delete a node.
    /// </summary>
    public class DeleteNodeCommand : ICommand
    {
        private readonly FamilyTree _tree;
        private readonly Node _node;
        private readonly List<Connection> _removedConnections = new();

        public string Description => $"Delete node: {_node.Name}";

        public DeleteNodeCommand(FamilyTree tree, Node node)
        {
            _tree = tree;
            _node = node;
        }

        public void Execute()
        {
            // Store connections that will be removed
            _removedConnections.Clear();
            foreach (var conn in _tree.Connections.ToList())
            {
                if (conn.FromNodeId == _node.Id || conn.ToNodeId == _node.Id)
                {
                    _removedConnections.Add(conn);
                    _tree.Connections.Remove(conn);
                }
            }
            _tree.Nodes.Remove(_node);
        }

        public void Undo()
        {
            _tree.Nodes.Add(_node);
            foreach (var conn in _removedConnections)
            {
                _tree.Connections.Add(conn);
            }
        }
    }

    /// <summary>
    /// Command to add a connection.
    /// </summary>
    public class AddConnectionCommand : ICommand
    {
        private readonly FamilyTree _tree;
        private readonly Connection _connection;

        public string Description => "Add connection";

        public AddConnectionCommand(FamilyTree tree, Connection connection)
        {
            _tree = tree;
            _connection = connection;
        }

        public void Execute()
        {
            if (!_tree.Connections.Contains(_connection))
                _tree.Connections.Add(_connection);
        }

        public void Undo()
        {
            _tree.Connections.Remove(_connection);
        }
    }

    /// <summary>
    /// Command to delete a connection.
    /// </summary>
    public class DeleteConnectionCommand : ICommand
    {
        private readonly FamilyTree _tree;
        private readonly Connection _connection;

        public string Description => "Delete connection";

        public DeleteConnectionCommand(FamilyTree tree, Connection connection)
        {
            _tree = tree;
            _connection = connection;
        }

        public void Execute()
        {
            _tree.Connections.Remove(_connection);
        }

        public void Undo()
        {
            _tree.Connections.Add(_connection);
        }
    }

    /// <summary>
    /// Command to rename a node.
    /// </summary>
    public class RenameNodeCommand : ICommand
    {
        private readonly Node _node;
        private readonly string _oldName;
        private readonly string _newName;

        public string Description => $"Rename: {_oldName} → {_newName}";

        public RenameNodeCommand(Node node, string oldName, string newName)
        {
            _node = node;
            _oldName = oldName;
            _newName = newName;
        }

        public void Execute()
        {
            _node.Name = _newName;
        }

        public void Undo()
        {
            _node.Name = _oldName;
        }
    }

    /// <summary>
    /// Generic command that takes action and undo lambdas.
    /// </summary>
    public class ActionCommand : ICommand
    {
        private readonly Action _executeAction;
        private readonly Action _undoAction;
        private readonly string _description;

        public string Description => _description;

        public ActionCommand(Action executeAction, Action undoAction, string description = "Action")
        {
            _executeAction = executeAction;
            _undoAction = undoAction;
            _description = description;
        }

        public void Execute()
        {
            _executeAction();
        }

        public void Undo()
        {
            _undoAction();
        }
    }

    #endregion
}
