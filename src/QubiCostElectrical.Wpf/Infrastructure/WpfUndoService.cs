using System.ComponentModel;
using System.Runtime.CompilerServices;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Historial de Deshacer/Rehacer para la sesión WPF.
/// Usa snapshots completos de ProjectFileData (deep clone vía JSON).
/// Límite: 15 pasos — proyectos reales son varios MB; 15 es conservador (~15-75 MB pico).
/// </summary>
public sealed class WpfUndoService : INotifyPropertyChanged
{
    private readonly Stack<(ProjectFileData Snapshot, string Description)> _undo = new();
    private readonly Stack<(ProjectFileData Snapshot, string Description)> _redo = new();
    private int _savedDepth; // count of undo stack at last save; -1 = saved state no longer reachable
    private const int MaxSteps = 15;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public bool IsUndoingOrRedoing { get; private set; }

    public string LastUndoDescription => _undo.Count > 0 ? _undo.Peek().Description : "";
    public string LastRedoDescription => _redo.Count > 0 ? _redo.Peek().Description : "";

    /// <summary>
    /// Captura el estado actual ANTES de una edición confirmada.
    /// Las ediciones no confirmadas (mid-TextBox) no deben llamar este método.
    /// </summary>
    public void PushUndoPoint(BudgetProjectSession session, string description = "Edición")
    {
        if (IsUndoingOrRedoing)
            return;
        PushSnapshot(session.CreateSnapshot(), description);
    }

    /// <summary>
    /// Registra un snapshot pre-capturado (tomado antes de un diálogo modal).
    /// Usar cuando se necesita capturar el estado antes de mostrar el diálogo
    /// pero solo registrar el punto de Undo si el usuario confirma.
    /// </summary>
    public void PushSnapshot(ProjectFileData snapshot, string description = "Edición")
    {
        if (IsUndoingOrRedoing)
            return;

        if (_savedDepth > _undo.Count)
            _savedDepth = -1;

        _undo.Push((snapshot, description));

        while (_undo.Count > MaxSteps)
            TrimOldest();

        _redo.Clear();
        NotifyCanChanged();
    }

    public bool Undo(BudgetProjectSession session) => Undo(session, out _);

    public bool Undo(BudgetProjectSession session, out string actionDescription)
    {
        actionDescription = "";
        if (_undo.Count == 0) return false;

        IsUndoingOrRedoing = true;
        try
        {
            var (previous, desc) = _undo.Pop();
            actionDescription = desc;
            _redo.Push((session.CreateSnapshot(), desc));

            bool isDirty = _savedDepth < 0 || _undo.Count != _savedDepth;
            session.ApplySnapshot(previous, markDirty: isDirty);

            NotifyCanChanged();
            return true;
        }
        finally
        {
            IsUndoingOrRedoing = false;
        }
    }

    public bool Redo(BudgetProjectSession session) => Redo(session, out _);

    public bool Redo(BudgetProjectSession session, out string actionDescription)
    {
        actionDescription = "";
        if (_redo.Count == 0) return false;

        IsUndoingOrRedoing = true;
        try
        {
            var (next, desc) = _redo.Pop();
            actionDescription = desc;
            _undo.Push((session.CreateSnapshot(), desc));

            bool isDirty = _savedDepth < 0 || _undo.Count != _savedDepth;
            session.ApplySnapshot(next, markDirty: isDirty);

            NotifyCanChanged();
            return true;
        }
        finally
        {
            IsUndoingOrRedoing = false;
        }
    }

    /// <summary>Registra el estado actual como guardado para gestión de IsDirty.</summary>
    public void OnSaved()
    {
        _savedDepth = _undo.Count;
        NotifyCanChanged();
    }

    /// <summary>Limpia todo el historial al abrir/cerrar/nuevo proyecto.</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _savedDepth = 0;
        NotifyCanChanged();
    }

    public int UndoCount => _undo.Count;
    public int RedoCount => _redo.Count;

    private void TrimOldest()
    {
        var all = _undo.ToArray();
        _undo.Clear();
        for (int i = MaxSteps - 1; i >= 0; i--)
            _undo.Push(all[i]);
    }

    private void NotifyCanChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(LastUndoDescription));
        OnPropertyChanged(nameof(LastRedoDescription));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
