namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Política de teclado y clic de cubicación. No referencia WPF: la vista observa el estado,
/// pide una acción y la ejecuta (foco, commit, pintura).
/// </summary>
public static class CubicacionCellSession
{
    public static CubicacionCellDecision Decide(CubicacionCellInput input) => input.Source switch
    {
        CubicacionInputSource.UnidadHeader => DecideUnidad(input),
        CubicacionInputSource.CantHeader => DecideCant(input),
        CubicacionInputSource.ObservacionHeader => DecideFieldChrome(input),
        CubicacionInputSource.PartidaCodigo => DecidePartidaBox(input, toDescription: true),
        CubicacionInputSource.PartidaDescripcion => DecidePartidaBox(input, toDescription: false),
        CubicacionInputSource.PartidasPanel => DecidePartidasPanel(input),
        CubicacionInputSource.Grid => DecideGrid(input),
        CubicacionInputSource.CodigoEditor => DecideCodigoEditor(input),
        CubicacionInputSource.Shell => DecideShell(input),
        CubicacionInputSource.ResourceClick => DecideClick(input),
        _ => CubicacionCellDecision.Pass
    };

    private static CubicacionCellDecision DecideUnidad(CubicacionCellInput input)
    {
        if (input.SuggestionsOpen)
        {
            var popup = DecideSuggestionPopup(input);
            if (popup.Action != CubicacionCellAction.PassThrough)
                return popup;
        }

        if (input.Key == CubicacionKey.Escape)
            return CubicacionCellDecision.Of(CubicacionCellAction.CancelStay);

        var nav = DecideUnidadNavigation(input);
        if (nav.Action != CubicacionCellAction.PassThrough)
            return nav;

        return DecideFieldChrome(input);
    }

    private static CubicacionCellDecision DecideCant(CubicacionCellInput input)
    {
        var nav = DecideCantNavigation(input);
        if (nav.Action != CubicacionCellAction.PassThrough)
            return nav;
        return DecideFieldChrome(input);
    }

    private static CubicacionCellDecision DecideSuggestionPopup(CubicacionCellInput input) => input.Key switch
    {
        CubicacionKey.Down => CubicacionCellDecision.Move(1),
        CubicacionKey.Up => CubicacionCellDecision.Move(-1),
        CubicacionKey.Enter or CubicacionKey.Tab when input.SuggestionsHaveSelection =>
            CubicacionCellDecision.Of(CubicacionCellAction.ApplySuggestion),
        CubicacionKey.Escape => CubicacionCellDecision.Of(CubicacionCellAction.CloseSuggestions),
        _ => CubicacionCellDecision.Pass
    };

    private static CubicacionCellDecision DecideUnidadNavigation(CubicacionCellInput input)
    {
        switch (input.Key)
        {
            case CubicacionKey.Tab:
            case CubicacionKey.Enter:
                return Commit(CubicacionMoveTarget.CantHeader);
            case CubicacionKey.Right when input.FormulaMode:
                return Commit(CubicacionMoveTarget.CantHeader);
            case CubicacionKey.Right when CaretInside(input):
                return CubicacionCellDecision.Pass;
            case CubicacionKey.Right:
                return Commit(CubicacionMoveTarget.CantHeader);
            case CubicacionKey.Left when input.FormulaMode:
                return CubicacionCellDecision.Swallow();
            case CubicacionKey.Left:
                return CubicacionCellDecision.Pass;
            case CubicacionKey.Down when input.FormulaMode:
                return CubicacionCellDecision.Reference(NextIndex(input, +1));
            case CubicacionKey.Down:
                return Commit(CubicacionMoveTarget.GridUnidad, 0);
            case CubicacionKey.Up when input.FormulaMode:
                return CubicacionCellDecision.Reference(NextIndex(input, -1));
            case CubicacionKey.Up:
                return CubicacionCellDecision.Swallow();
            default:
                return CubicacionCellDecision.Pass;
        }
    }

    private static CubicacionCellDecision DecideCantNavigation(CubicacionCellInput input)
    {
        bool picking = input.PickIndex >= 0;
        switch (input.Key)
        {
            case CubicacionKey.Tab when input.Modifiers.HasFlag(CubicacionModifiers.Shift):
                return Commit(CubicacionMoveTarget.UnidadHeader);
            case CubicacionKey.Tab:
                return Commit(input.HasSectorColumns ? CubicacionMoveTarget.GridFirstSector : CubicacionMoveTarget.GridCodigo, 0);
            case CubicacionKey.Left when picking:
                return CubicacionCellDecision.Swallow();
            case CubicacionKey.Left when CaretInsideBackward(input):
                return CubicacionCellDecision.Pass;
            case CubicacionKey.Left:
                return CubicacionCellDecision.Go(CubicacionMoveTarget.UnidadHeader);
            case CubicacionKey.Right when picking:
                return CubicacionCellDecision.Swallow();
            case CubicacionKey.Right when CaretInside(input):
                return CubicacionCellDecision.Pass;
            case CubicacionKey.Right:
                return Commit(input.HasSectorColumns ? CubicacionMoveTarget.GridFirstSector : CubicacionMoveTarget.GridCant, 0);
            case CubicacionKey.Down when !input.FormulaMode && !picking:
                return Commit(CubicacionMoveTarget.GridCant, 0);
            case CubicacionKey.Down:
                return CubicacionCellDecision.Reference(NextIndex(input, +1));
            case CubicacionKey.Up when !input.FormulaMode && !picking:
                return CubicacionCellDecision.Swallow();
            case CubicacionKey.Up:
                return CubicacionCellDecision.Reference(NextIndex(input, -1));
            case CubicacionKey.Enter:
                return Commit(CubicacionMoveTarget.GridCodigo, picking ? input.PickIndex : 0);
            case CubicacionKey.Escape:
                return CubicacionCellDecision.Of(CubicacionCellAction.CancelAndFocusGrid, CubicacionMoveTarget.GridCodigo, 0);
            default:
                return CubicacionCellDecision.Pass;
        }
    }

    private static CubicacionCellDecision DecideFieldChrome(CubicacionCellInput input)
    {
        if (input.Key == CubicacionKey.F2)
            return CubicacionCellDecision.Of(CubicacionCellAction.StartEdit);

        if (input.Key == CubicacionKey.Escape)
            return CubicacionCellDecision.Of(CubicacionCellAction.CancelStay);

        if (input.Key == CubicacionKey.Enter)
            return Commit(EnterTarget(input));

        if (!input.EditingField && input.Key is CubicacionKey.Delete or CubicacionKey.Backspace)
            return CubicacionCellDecision.Of(CubicacionCellAction.ClearField);

        if ((input.Modifiers == CubicacionModifiers.Control || !input.EditingField) &&
            input.Key is CubicacionKey.Left or CubicacionKey.Right)
        {
            var moved = DecideFieldArrow(input);
            if (moved.Action != CubicacionCellAction.PassThrough)
                return moved;
        }

        if (!IsChromeKey(input.Key) && !input.EditingField)
            return new CubicacionCellDecision(CubicacionCellAction.BeginEditing, Handled: false);

        return CubicacionCellDecision.Pass;
    }

    private static CubicacionMoveTarget EnterTarget(CubicacionCellInput input) => input.Source switch
    {
        CubicacionInputSource.CantHeader => CubicacionMoveTarget.GridCodigo,
        CubicacionInputSource.UnidadHeader => CubicacionMoveTarget.CantHeader,
        _ => CubicacionMoveTarget.None
    };

    private static CubicacionCellDecision DecideFieldArrow(CubicacionCellInput input)
    {
        if (input.Source == CubicacionInputSource.UnidadHeader && input.Key == CubicacionKey.Right)
            return CubicacionCellDecision.Go(CubicacionMoveTarget.CantHeader);
        if (input.Source == CubicacionInputSource.CantHeader && input.Key == CubicacionKey.Left)
            return CubicacionCellDecision.Go(CubicacionMoveTarget.UnidadHeader);
        if (input.Source == CubicacionInputSource.CantHeader && input.Key == CubicacionKey.Right)
            return Commit(input.HasSectorColumns ? CubicacionMoveTarget.GridFirstSector : CubicacionMoveTarget.GridCant, 0);
        if (input.Source == CubicacionInputSource.ObservacionHeader && input.Key == CubicacionKey.Left)
            return CubicacionCellDecision.Go(CubicacionMoveTarget.CantHeader);
        return CubicacionCellDecision.Pass;
    }

    private static CubicacionCellDecision DecidePartidaBox(CubicacionCellInput input, bool toDescription)
    {
        if (input.Key == CubicacionKey.Enter)
            return Commit(toDescription ? CubicacionMoveTarget.PartidaDescripcion : CubicacionMoveTarget.Grid);
        if (input.Key == CubicacionKey.Escape)
            return CubicacionCellDecision.Of(CubicacionCellAction.RevertAndFocusGrid, CubicacionMoveTarget.Grid);
        return CubicacionCellDecision.Pass;
    }

    private static CubicacionCellDecision DecidePartidasPanel(CubicacionCellInput input)
    {
        bool ctrl = input.Modifiers.HasFlag(CubicacionModifiers.Control);
        bool alt = input.Modifiers.HasFlag(CubicacionModifiers.Alt);
        bool shift = input.Modifiers.HasFlag(CubicacionModifiers.Shift);

        if (input.Key == CubicacionKey.Escape && input.PartidaFilterActive)
            return CubicacionCellDecision.Of(CubicacionCellAction.ClearPartidaFilter);
        if (ctrl && !alt && input.Key == CubicacionKey.Plus)
            return CubicacionCellDecision.Of(CubicacionCellAction.AddPartida);
        if (ctrl && !alt && !shift && input.Key == CubicacionKey.Minus)
            return CubicacionCellDecision.Of(CubicacionCellAction.DeletePartida);
        if (!ctrl && !alt && input.Key == CubicacionKey.Space && !input.FocusedIsTextBox && input.SelectedIsPartida)
            return CubicacionCellDecision.Of(CubicacionCellAction.TogglePartidaTag);
        return CubicacionCellDecision.Pass;
    }

    private static CubicacionCellDecision DecideShell(CubicacionCellInput input)
    {
        if (input.Key == CubicacionKey.Escape)
            return CubicacionCellDecision.Of(CubicacionCellAction.CloseAutocomplete);

        if (!input.IgnoreShortcut)
            return CubicacionCellDecision.Of(CubicacionCellAction.RunShortcut);

        bool ctrl = input.Modifiers.HasFlag(CubicacionModifiers.Control);
        bool alt = input.Modifiers.HasFlag(CubicacionModifiers.Alt);
        bool shift = input.Modifiers.HasFlag(CubicacionModifiers.Shift);
        bool onlyCtrl = input.Modifiers == CubicacionModifiers.Control;

        if (ctrl && !alt && (input.Key == CubicacionKey.Plus || (shift && input.Key is CubicacionKey.Oem1 or CubicacionKey.OemQuestion)))
            return input.CanAddPartida
                ? CubicacionCellDecision.Of(CubicacionCellAction.AddPartida)
                : CubicacionCellDecision.Pass;
        if (ctrl && !alt && !shift && input.Key == CubicacionKey.Minus)
            return input.CanDeletePartida
                ? CubicacionCellDecision.Of(CubicacionCellAction.DeletePartida)
                : CubicacionCellDecision.Pass;
        if (onlyCtrl && input.Key == CubicacionKey.S)
            return CubicacionCellDecision.Of(CubicacionCellAction.Save);
        if (onlyCtrl && input.Key == CubicacionKey.V)
            return input.PasteAllowed
                ? CubicacionCellDecision.Of(CubicacionCellAction.Paste)
                : CubicacionCellDecision.Pass;
        return CubicacionCellDecision.Pass;
    }

    private static CubicacionCellDecision DecideGrid(CubicacionCellInput input)
    {
        if (input.EventFromOutsideCell || input.AutocompleteOpen)
            return CubicacionCellDecision.Pass;

        if (!input.GridEditing && input.Key == CubicacionKey.Up && input.Modifiers == CubicacionModifiers.None && input.RowIndex <= 0)
            return CubicacionCellDecision.Of(
                CubicacionCellAction.FocusHeader,
                input.QtyColumn ? CubicacionMoveTarget.CantHeader : CubicacionMoveTarget.UnidadHeader);

        if (!input.IgnoreShortcut)
            return new CubicacionCellDecision(
                CubicacionCellAction.RunShortcut,
                CommitEditBefore: input.GridEditing && input.ShortcutMatches);

        if (!input.GridEditing && input.Modifiers == CubicacionModifiers.Control && input.Key == CubicacionKey.V)
            return CubicacionCellDecision.Of(CubicacionCellAction.Paste);
        if (!input.GridEditing && input.Modifiers == CubicacionModifiers.Control && input.Key == CubicacionKey.C)
            return CubicacionCellDecision.Of(CubicacionCellAction.Copy);
        if (!input.GridEditing && input.Modifiers == CubicacionModifiers.Control && input.Key == CubicacionKey.X)
            return CubicacionCellDecision.Of(CubicacionCellAction.Cut);
        if (!input.GridEditing && input.Key is CubicacionKey.Delete or CubicacionKey.Backspace && input.HasDeletableCells)
            return CubicacionCellDecision.Of(CubicacionCellAction.ClearCells);

        if (input.SectorMatrixReady)
        {
            if (IsSectorPickArrow(input))
                return CubicacionCellDecision.Of(CubicacionCellAction.SectorPickArrow, delta: ArrowDelta(input.Key));

            var sectorMove = DecideSectorCtrl(input);
            var rest = sectorMove.Action == CubicacionCellAction.PassThrough
                ? DecideGridCommands(input)
                : sectorMove;
            if (input.SectorPicking)
                rest = rest with { FinalizeSectorPick = true };
            return rest;
        }

        return DecideGridCommands(input);
    }

    private static CubicacionCellDecision DecideSectorCtrl(CubicacionCellInput input)
    {
        if (input.Modifiers != CubicacionModifiers.Control || input.Key is not (CubicacionKey.Left or CubicacionKey.Right))
            return CubicacionCellDecision.Pass;

        if (input.Key == CubicacionKey.Right && input.OnCodigoColumn)
            return CubicacionCellDecision.Of(CubicacionCellAction.FocusSectorSlot, rowIndex: 0);

        if (input.SectorSlot < 0)
            return CubicacionCellDecision.Pass;

        if (input.Key == CubicacionKey.Left && input.SectorSlot == 0)
            return CubicacionCellDecision.Of(CubicacionCellAction.FocusCodigoColumn);

        int delta = input.Key == CubicacionKey.Right ? 1 : -1;
        return new CubicacionCellDecision(CubicacionCellAction.SectorEdge, Delta: delta, CommitEditBefore: true);
    }

    private static CubicacionCellDecision DecideGridCommands(CubicacionCellInput input)
    {
        bool none = input.Modifiers == CubicacionModifiers.None;
        bool onlyCtrl = input.Modifiers == CubicacionModifiers.Control;

        if (!input.GridEditing && ((input.Key == CubicacionKey.Insert && none) || (input.Key == CubicacionKey.N && onlyCtrl)))
            return CubicacionCellDecision.Of(CubicacionCellAction.AddRow);
        if (!input.GridEditing && input.Key is CubicacionKey.Delete or CubicacionKey.Backspace && none)
            return input.HasDeletableCells
                ? CubicacionCellDecision.Of(CubicacionCellAction.ClearCells)
                : CubicacionCellDecision.Swallow();
        if (!input.GridEditing && input.Key == CubicacionKey.D && onlyCtrl)
        {
            bool single = input.SelectedItemCount <= 1 && input.SelectedCellCount <= 1;
            return single
                ? CubicacionCellDecision.Of(CubicacionCellAction.DuplicateRow)
                : CubicacionCellDecision.Swallow();
        }
        if (onlyCtrl && input.Key == CubicacionKey.Plus)
            return new CubicacionCellDecision(CubicacionCellAction.InsertRow, CommitEditBefore: input.GridEditing);
        if (onlyCtrl && input.Key == CubicacionKey.Minus)
            return new CubicacionCellDecision(CubicacionCellAction.DeleteRows, CommitEditBefore: input.GridEditing);
        return CubicacionCellDecision.Pass;
    }

    private static CubicacionCellDecision DecideCodigoEditor(CubicacionCellInput input)
    {
        if (!input.AutocompleteOpen)
            return CubicacionCellDecision.Pass;

        switch (input.Key)
        {
            case CubicacionKey.Down:
                return CubicacionCellDecision.Move(1, CubicacionCellAction.MoveAutocomplete);
            case CubicacionKey.Up:
                return CubicacionCellDecision.Move(-1, CubicacionCellAction.MoveAutocomplete);
            case CubicacionKey.Enter when input.AutocompleteHasItem:
                return CubicacionCellDecision.Of(CubicacionCellAction.ApplyAutocomplete);
            case CubicacionKey.Enter:
                return CubicacionCellDecision.Of(CubicacionCellAction.CommitTypedCodigo);
            case CubicacionKey.Tab when input.AutocompleteHasExplicitSelection || input.FirstIsExactMatch:
                return new CubicacionCellDecision(CubicacionCellAction.ApplyAutocompleteTab, Handled: false);
            case CubicacionKey.Escape:
                return CubicacionCellDecision.Of(CubicacionCellAction.CloseAutocomplete);
            default:
                return CubicacionCellDecision.Pass;
        }
    }

    private static CubicacionCellDecision DecideClick(CubicacionCellInput input)
    {
        if (input.AltClickSector && input.SectorSlot >= 0 && input.RowIndex >= 0)
            return CubicacionCellDecision.Of(CubicacionCellAction.OpenHoja, rowIndex: input.RowIndex, delta: input.SectorSlot);

            if (input.UnidadActive && input.RowIndex >= 0)
            {
                return input.ClickCount >= 2
                    ? CubicacionCellDecision.Of(CubicacionCellAction.CommitAndMove, CubicacionMoveTarget.CantHeader, input.RowIndex)
                    : CubicacionCellDecision.Of(CubicacionCellAction.PickUnidadFromRow, rowIndex: input.RowIndex);
            }

        if (input.EditingUnidadCell && input.CanPickUnidadFromRow)
            return CubicacionCellDecision.Of(CubicacionCellAction.PickUnidadIntoEditor, rowIndex: input.RowIndex);

        if (input.CantActive && input.RowIndex >= 0 && input.ClickOnCantColumn)
            return CubicacionCellDecision.Of(CubicacionCellAction.PickCantCell, rowIndex: input.RowIndex);

        if (input.SectorEditorClickOther && input.SectorFormulaArmed && input.CanPickSector)
            return CubicacionCellDecision.Of(CubicacionCellAction.PickSectorReference, rowIndex: input.RowIndex, delta: input.SectorSlot);

        if (!input.ReadOnly && input.CellEditable && !input.CellIsEditing)
            return new CubicacionCellDecision(CubicacionCellAction.BeginGridEdit, Handled: false);

        return CubicacionCellDecision.Pass;
    }

    private static bool IsSectorPickArrow(CubicacionCellInput input) =>
        input.GridEditing
        && input.Modifiers == CubicacionModifiers.None
        && input.Key is CubicacionKey.Up or CubicacionKey.Down or CubicacionKey.Left or CubicacionKey.Right
        && input.SectorColumnActive
        && (input.SectorPicking || input.CanStartSectorPick)
        && input.FocusedEditorIsTextBox;

    private static int ArrowDelta(CubicacionKey key) => key switch
    {
        CubicacionKey.Down or CubicacionKey.Right => 1,
        _ => -1
    };

    private static bool CaretInside(CubicacionCellInput input) =>
        input.SelectionLength == 0 && input.CaretIndex < input.TextLength;

    private static bool CaretInsideBackward(CubicacionCellInput input) =>
        input.SelectionLength == 0 && input.CaretIndex > 0;

    private static int NextIndex(CubicacionCellInput input, int delta)
    {
        int max = input.RowCount > 0 ? input.RowCount - 1 : 0;
        int cur = input.ResolvedRefIndex;
        if (delta > 0)
            return cur < 0 ? 0 : Math.Min(cur + 1, max);
        return cur <= 0 ? 0 : cur - 1;
    }

    private static bool IsChromeKey(CubicacionKey key) => key is
        CubicacionKey.Left or CubicacionKey.Right or CubicacionKey.Up or CubicacionKey.Down or
        CubicacionKey.Tab or CubicacionKey.F2 or CubicacionKey.Enter or CubicacionKey.Escape or
        CubicacionKey.Shift or CubicacionKey.Control or CubicacionKey.Alt;

    private static CubicacionCellDecision Commit(CubicacionMoveTarget target, int row = 0) =>
        CubicacionCellDecision.Of(CubicacionCellAction.CommitAndMove, target, row);
}

public enum CubicacionInputSource
{
    None,
    UnidadHeader,
    CantHeader,
    ObservacionHeader,
    PartidaCodigo,
    PartidaDescripcion,
    PartidasPanel,
    Grid,
    CodigoEditor,
    Shell,
    ResourceClick
}

public enum CubicacionKey
{
    None,
    Enter,
    Escape,
    Tab,
    F2,
    Up,
    Down,
    Left,
    Right,
    Delete,
    Backspace,
    Insert,
    Space,
    C,
    X,
    V,
    S,
    D,
    N,
    Plus,
    Minus,
    Oem1,
    OemQuestion,
    Shift,
    Control,
    Alt,
    Other
}

[Flags]
public enum CubicacionModifiers
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4
}

public enum CubicacionCellAction
{
    PassThrough,
    RunShortcut,
    Swallow,
    StartEdit,
    BeginEditing,
    ClearField,
    CancelStay,
    CancelAndFocusGrid,
    RevertAndFocusGrid,
    CommitAndMove,
    MoveFocus,
    InsertReference,
    CloseSuggestions,
    MoveSuggestion,
    ApplySuggestion,
    FocusHeader,
    Paste,
    Copy,
    Cut,
    ClearCells,
    AddRow,
    DeleteRows,
    DuplicateRow,
    InsertRow,
    AddPartida,
    DeletePartida,
    Save,
    ClearPartidaFilter,
    TogglePartidaTag,
    CloseAutocomplete,
    MoveAutocomplete,
    ApplyAutocomplete,
    CommitTypedCodigo,
    ApplyAutocompleteTab,
    OpenHoja,
    PickUnidadFromRow,
    PickUnidadIntoEditor,
    PickCantCell,
    PickSectorReference,
    BeginGridEdit,
    SectorPickArrow,
    FocusSectorSlot,
    FocusCodigoColumn,
    SectorEdge
}

public enum CubicacionMoveTarget
{
    None,
    UnidadHeader,
    CantHeader,
    PartidaDescripcion,
    GridCodigo,
    Grid,
    GridCant,
    GridUnidad,
    GridFirstSector
}

public sealed record CubicacionCellInput
{
    public CubicacionInputSource Source { get; init; }
    public CubicacionKey Key { get; init; }
    public CubicacionModifiers Modifiers { get; init; }
    public bool IgnoreShortcut { get; init; }
    public bool EditingField { get; init; }
    public bool FormulaMode { get; init; }
    public bool SuggestionsOpen { get; init; }
    public bool SuggestionsHaveSelection { get; init; }
    public int SelectionLength { get; init; }
    public int CaretIndex { get; init; }
    public int TextLength { get; init; }
    public int PickIndex { get; init; } = -1;
    public int ResolvedRefIndex { get; init; } = -1;
    public int RowCount { get; init; }
    public int RowIndex { get; init; } = -1;
    public bool HasSectorColumns { get; init; }
    public bool ReadOnly { get; init; }
    public bool GridEditing { get; init; }
    public bool ShortcutMatches { get; init; }
    public bool EventFromOutsideCell { get; init; }
    public bool QtyColumn { get; init; }
    public bool HasDeletableCells { get; init; }
    public int SelectedItemCount { get; init; }
    public int SelectedCellCount { get; init; }
    public bool SectorMatrixReady { get; init; }
    public bool SectorColumnActive { get; init; }
    public bool SectorPicking { get; init; }
    public bool CanStartSectorPick { get; init; }
    public bool FocusedEditorIsTextBox { get; init; }
    public int SectorSlot { get; init; } = -1;
    public bool OnCodigoColumn { get; init; }
    public bool PartidaFilterActive { get; init; }
    public bool FocusedIsTextBox { get; init; }
    public bool SelectedIsPartida { get; init; }
    public bool PasteAllowed { get; init; }
    public bool CanAddPartida { get; init; }
    public bool CanDeletePartida { get; init; }
    public bool AutocompleteOpen { get; init; }
    public bool AutocompleteHasItem { get; init; }
    public bool AutocompleteHasExplicitSelection { get; init; }
    public bool FirstIsExactMatch { get; init; }
    public int ClickCount { get; init; }
    public bool AltClickSector { get; init; }
    public bool UnidadActive { get; init; }
    public bool CantActive { get; init; }
    public bool ClickOnCantColumn { get; init; }
    public bool EditingUnidadCell { get; init; }
    public bool CanPickUnidadFromRow { get; init; }
    public bool SectorEditorClickOther { get; init; }
    public bool SectorFormulaArmed { get; init; }
    public bool CanPickSector { get; init; }
    public bool CellEditable { get; init; }
    public bool CellIsEditing { get; init; }
}

public readonly record struct CubicacionCellDecision(
    CubicacionCellAction Action,
    bool Handled = true,
    CubicacionMoveTarget Target = CubicacionMoveTarget.None,
    int RowIndex = 0,
    int Delta = 0,
    bool CommitEditBefore = false,
    bool FinalizeSectorPick = false)
{
    public static CubicacionCellDecision Pass { get; } = new(CubicacionCellAction.PassThrough, Handled: false);

    public static CubicacionCellDecision Of(CubicacionCellAction action, CubicacionMoveTarget target = default, int rowIndex = 0, int delta = 0) =>
        new(action, Target: target, RowIndex: rowIndex, Delta: delta);

    public static CubicacionCellDecision Go(CubicacionMoveTarget target) =>
        new(CubicacionCellAction.MoveFocus, Target: target);

    public static CubicacionCellDecision Swallow() => new(CubicacionCellAction.Swallow);

    public static CubicacionCellDecision Move(int delta, CubicacionCellAction action = CubicacionCellAction.MoveSuggestion) =>
        new(action, Delta: delta);

    public static CubicacionCellDecision Reference(int rowIndex) =>
        new(CubicacionCellAction.InsertReference, RowIndex: rowIndex);
}
