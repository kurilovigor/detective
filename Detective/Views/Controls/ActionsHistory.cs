using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Detective.Views.Controls
{
    /// <summary>
    /// Represents an action that can be undone/redone by the ActionsHistory.
    /// </summary>
    public interface IHistoryAction
    {
        void Undo();
        void Redo();
    }

    /// <summary>
    /// Simple history manager for undo/redo operations. Implements INotifyPropertyChanged and exposes
    /// CanUndo/CanRedo properties along with RegisterUndo/RegisterRedo and Undo/Redo methods.
    /// </summary>
    public class ActionsHistory : INotifyPropertyChanged
    {
        private readonly Stack<IHistoryAction> _undo = new();
        private readonly Stack<IHistoryAction> _redo = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        public void RegisterUndo(IHistoryAction action)
        {
            if (action == null) return;
            _undo.Push(action);
            _redo.Clear();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        public void RegisterRedo(IHistoryAction action)
        {
            if (action == null) return;
            _redo.Push(action);
            OnPropertyChanged(nameof(CanRedo));
        }

        public void Undo()
        {
            if (_undo.Count == 0) return;
            var a = _undo.Pop();
            a.Undo();
            _redo.Push(a);
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        public void Redo()
        {
            if (_redo.Count == 0) return;
            var a = _redo.Pop();
            a.Redo();
            _undo.Push(a);
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
