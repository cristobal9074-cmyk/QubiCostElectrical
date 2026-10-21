using QubiCostElectrical.Wpf.Features.Cubicacion;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public class CubicacionCellSessionTests
{
    [Fact]
    public void Cant_Down_SinFormula_ConfirmaYBajaALaGrilla()
    {
        var d = CubicacionCellSession.Decide(Cant(CubicacionKey.Down));
        Assert.Equal(CubicacionCellAction.CommitAndMove, d.Action);
        Assert.Equal(CubicacionMoveTarget.GridCant, d.Target);
        Assert.Equal(0, d.RowIndex);
        Assert.True(d.Handled);
    }

    [Fact]
    public void Cant_Down_EnFormula_InsertaLaReferenciaSiguiente()
    {
        var d = CubicacionCellSession.Decide(Cant(CubicacionKey.Down) with
        {
            FormulaMode = true,
            ResolvedRefIndex = 1,
            RowCount = 5
        });
        Assert.Equal(CubicacionCellAction.InsertReference, d.Action);
        Assert.Equal(2, d.RowIndex);
    }

    [Fact]
    public void Cant_Up_SinFormula_NoSaleDelCuadro()
    {
        var d = CubicacionCellSession.Decide(Cant(CubicacionKey.Up));
        Assert.Equal(CubicacionCellAction.Swallow, d.Action);
        Assert.True(d.Handled);
    }

    [Fact]
    public void Cant_Escape_CancelaYVuelveAlCodigo()
    {
        var d = CubicacionCellSession.Decide(Cant(CubicacionKey.Escape) with { PickIndex = 4 });
        Assert.Equal(CubicacionCellAction.CancelAndFocusGrid, d.Action);
        Assert.Equal(CubicacionMoveTarget.GridCodigo, d.Target);
        Assert.Equal(0, d.RowIndex);
    }

    [Fact]
    public void Cant_Enter_ConfirmaEnLaFilaTomada()
    {
        var d = CubicacionCellSession.Decide(Cant(CubicacionKey.Enter) with { PickIndex = 3 });
        Assert.Equal(CubicacionCellAction.CommitAndMove, d.Action);
        Assert.Equal(CubicacionMoveTarget.GridCodigo, d.Target);
        Assert.Equal(3, d.RowIndex);
    }

    [Fact]
    public void Cant_Left_ConTodoSeleccionado_VaAUnidad()
    {
        var d = CubicacionCellSession.Decide(Cant(CubicacionKey.Left) with { SelectionLength = 4, TextLength = 4 });
        Assert.Equal(CubicacionCellAction.MoveFocus, d.Action);
        Assert.Equal(CubicacionMoveTarget.UnidadHeader, d.Target);
    }

    [Fact]
    public void Cant_Left_EditandoEnMedio_DejaMoverElCursor()
    {
        var d = CubicacionCellSession.Decide(Cant(CubicacionKey.Left) with
        {
            EditingField = true,
            SelectionLength = 0,
            CaretIndex = 2,
            TextLength = 5
        });
        Assert.Equal(CubicacionCellAction.PassThrough, d.Action);
        Assert.False(d.Handled);
    }

    [Fact]
    public void Cant_Right_AlFinal_ConfirmaYEntraALaPrimeraCasilla()
    {
        var d = CubicacionCellSession.Decide(Cant(CubicacionKey.Right) with
        {
            HasSectorColumns = true,
            SelectionLength = 3,
            TextLength = 3
        });
        Assert.Equal(CubicacionCellAction.CommitAndMove, d.Action);
        Assert.Equal(CubicacionMoveTarget.GridFirstSector, d.Target);
    }

    [Fact]
    public void Cant_F2_EmpiezaEdicion()
    {
        var d = CubicacionCellSession.Decide(Cant(CubicacionKey.F2));
        Assert.Equal(CubicacionCellAction.StartEdit, d.Action);
    }

    [Fact]
    public void Cant_SuprimirSeleccionado_Limpia()
    {
        var d = CubicacionCellSession.Decide(Cant(CubicacionKey.Delete));
        Assert.Equal(CubicacionCellAction.ClearField, d.Action);
    }

    [Fact]
    public void Cant_PrimerCaracter_EntraAEditarSinTragarseLaTecla()
    {
        var d = CubicacionCellSession.Decide(Cant(CubicacionKey.Other));
        Assert.Equal(CubicacionCellAction.BeginEditing, d.Action);
        Assert.False(d.Handled);
    }

    [Fact]
    public void Unidad_EscapeConSugerencias_CierraLaLista()
    {
        var d = CubicacionCellSession.Decide(Unidad(CubicacionKey.Escape) with { SuggestionsOpen = true });
        Assert.Equal(CubicacionCellAction.CloseSuggestions, d.Action);
    }

    [Fact]
    public void Unidad_Escape_RestauraYSeQueda()
    {
        var d = CubicacionCellSession.Decide(Unidad(CubicacionKey.Escape));
        Assert.Equal(CubicacionCellAction.CancelStay, d.Action);
    }

    [Fact]
    public void Unidad_Enter_ConfirmaYPasaACantidad()
    {
        var d = CubicacionCellSession.Decide(Unidad(CubicacionKey.Enter));
        Assert.Equal(CubicacionCellAction.CommitAndMove, d.Action);
        Assert.Equal(CubicacionMoveTarget.CantHeader, d.Target);
    }

    [Fact]
    public void Unidad_Down_EnFormula_InsertaReferencia()
    {
        var d = CubicacionCellSession.Decide(Unidad(CubicacionKey.Down) with
        {
            FormulaMode = true,
            ResolvedRefIndex = 0,
            RowCount = 4
        });
        Assert.Equal(CubicacionCellAction.InsertReference, d.Action);
        Assert.Equal(1, d.RowIndex);
    }

    [Fact]
    public void Unidad_Down_SinFormula_BajaALaColumnaUnidad()
    {
        var d = CubicacionCellSession.Decide(Unidad(CubicacionKey.Down));
        Assert.Equal(CubicacionCellAction.CommitAndMove, d.Action);
        Assert.Equal(CubicacionMoveTarget.GridUnidad, d.Target);
    }

    [Fact]
    public void Grid_UpEnFila0_Cantidad_SubeALaCabeceraCant()
    {
        var d = CubicacionCellSession.Decide(Grid(CubicacionKey.Up) with { RowIndex = 0, QtyColumn = true });
        Assert.Equal(CubicacionCellAction.FocusHeader, d.Action);
        Assert.Equal(CubicacionMoveTarget.CantHeader, d.Target);
    }

    [Fact]
    public void Grid_UpEnFila0_Codigo_SubeAUnidad()
    {
        var d = CubicacionCellSession.Decide(Grid(CubicacionKey.Up) with { RowIndex = 0, QtyColumn = false });
        Assert.Equal(CubicacionMoveTarget.UnidadHeader, d.Target);
    }

    [Fact]
    public void Grid_UpEnFila2_PrimeroOfreceElAtajo()
    {
        var d = CubicacionCellSession.Decide(Grid(CubicacionKey.Up) with { RowIndex = 2 });
        Assert.Equal(CubicacionCellAction.RunShortcut, d.Action);
    }

    [Fact]
    public void Grid_CtrlV_SinEditar_Pega()
    {
        var d = CubicacionCellSession.Decide(Grid(CubicacionKey.V) with
        {
            IgnoreShortcut = true,
            Modifiers = CubicacionModifiers.Control
        });
        Assert.Equal(CubicacionCellAction.Paste, d.Action);
    }

    [Fact]
    public void Grid_DeleteConCeldas_LimpiaYNoBorraLaFila()
    {
        var d = CubicacionCellSession.Decide(Grid(CubicacionKey.Delete) with
        {
            IgnoreShortcut = true,
            HasDeletableCells = true
        });
        Assert.Equal(CubicacionCellAction.ClearCells, d.Action);
    }

    [Fact]
    public void Grid_DeleteSinCeldasDeletables_TragaYNoBorraLaFila()
    {
        var d = CubicacionCellSession.Decide(Grid(CubicacionKey.Delete) with
        {
            IgnoreShortcut = true,
            HasDeletableCells = false
        });
        Assert.NotEqual(CubicacionCellAction.DeleteRows, d.Action);
        Assert.True(d.Handled);
    }

    [Fact]
    public void Grid_FlechaEnSector_TomaReferencia()
    {
        var d = CubicacionCellSession.Decide(Grid(CubicacionKey.Down) with
        {
            IgnoreShortcut = true,
            RowIndex = 2,
            GridEditing = true,
            SectorMatrixReady = true,
            SectorColumnActive = true,
            CanStartSectorPick = true,
            FocusedEditorIsTextBox = true
        });
        Assert.Equal(CubicacionCellAction.SectorPickArrow, d.Action);
        Assert.False(d.FinalizeSectorPick);
    }

    [Fact]
    public void Click_AltEnCasilla_AbreLaHoja()
    {
        var d = CubicacionCellSession.Decide(new CubicacionCellInput
        {
            Source = CubicacionInputSource.ResourceClick,
            AltClickSector = true,
            SectorSlot = 2,
            RowIndex = 4
        });
        Assert.Equal(CubicacionCellAction.OpenHoja, d.Action);
        Assert.Equal(4, d.RowIndex);
        Assert.Equal(2, d.Delta);
    }

    [Fact]
    public void Click_CantActiva_TomaLaCelda()
    {
        var d = CubicacionCellSession.Decide(new CubicacionCellInput
        {
            Source = CubicacionInputSource.ResourceClick,
            CantActive = true,
            ClickOnCantColumn = true,
            RowIndex = 1
        });
        Assert.Equal(CubicacionCellAction.PickCantCell, d.Action);
        Assert.Equal(1, d.RowIndex);
    }

    [Fact]
    public void Click_CeldaEditable_EmpiezaEdicionSinTragarseElClic()
    {
        var d = CubicacionCellSession.Decide(new CubicacionCellInput
        {
            Source = CubicacionInputSource.ResourceClick,
            CellEditable = true
        });
        Assert.Equal(CubicacionCellAction.BeginGridEdit, d.Action);
        Assert.False(d.Handled);
    }

    [Fact]
    public void Click_SoloLectura_NoEdita()
    {
        var d = CubicacionCellSession.Decide(new CubicacionCellInput
        {
            Source = CubicacionInputSource.ResourceClick,
            ReadOnly = true,
            CellEditable = true
        });
        Assert.Equal(CubicacionCellAction.PassThrough, d.Action);
    }

    [Fact]
    public void Partidas_EscapeConFiltro_LoLimpia()
    {
        var d = CubicacionCellSession.Decide(new CubicacionCellInput
        {
            Source = CubicacionInputSource.PartidasPanel,
            Key = CubicacionKey.Escape,
            PartidaFilterActive = true
        });
        Assert.Equal(CubicacionCellAction.ClearPartidaFilter, d.Action);
    }

    [Fact]
    public void Codigo_EnterConLista_AplicaLaSugerencia()
    {
        var d = CubicacionCellSession.Decide(new CubicacionCellInput
        {
            Source = CubicacionInputSource.CodigoEditor,
            Key = CubicacionKey.Enter,
            AutocompleteOpen = true,
            AutocompleteHasItem = true
        });
        Assert.Equal(CubicacionCellAction.ApplyAutocomplete, d.Action);
    }

    [Fact]
    public void Shell_CtrlMas_AgregaPartidaSoloSiElComandoPuede()
    {
        var blocked = CubicacionCellSession.Decide(Shell(CubicacionKey.Plus, CubicacionModifiers.Control));
        var allowed = CubicacionCellSession.Decide(Shell(CubicacionKey.Plus, CubicacionModifiers.Control) with { CanAddPartida = true });
        Assert.Equal(CubicacionCellAction.PassThrough, blocked.Action);
        Assert.Equal(CubicacionCellAction.AddPartida, allowed.Action);
    }

    private static CubicacionCellInput Cant(CubicacionKey key) => new()
    {
        Source = CubicacionInputSource.CantHeader,
        Key = key
    };

    private static CubicacionCellInput Unidad(CubicacionKey key) => new()
    {
        Source = CubicacionInputSource.UnidadHeader,
        Key = key
    };

    private static CubicacionCellInput Grid(CubicacionKey key) => new()
    {
        Source = CubicacionInputSource.Grid,
        Key = key,
        RowIndex = 1
    };

    private static CubicacionCellInput Shell(CubicacionKey key, CubicacionModifiers modifiers) => new()
    {
        Source = CubicacionInputSource.Shell,
        Key = key,
        Modifiers = modifiers,
        IgnoreShortcut = true
    };
}
