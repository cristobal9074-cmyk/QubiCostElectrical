using QubiCostElectrical;
using QubiCostElectrical.Tests.Fixtures;
using QubiCostElectrical.Infrastructure;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Pase de cubicación sobre una copia de PEL039A2026 ABIERTO.qce.
/// No abre el archivo de obra. Cubre guardar/reabrir, fórmula en CANT, pegado, deshacer y Visor.
/// </summary>
public sealed class Pel039ManualSmokeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _copyPath;

    public Pel039ManualSmokeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pel039_smoke_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _copyPath = Path.Combine(_tempDir, "PEL039A2026-SMOKE.qce");
        File.Copy(ResolveSourceProject(), _copyPath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Formula_Save_Reopen_Paste_Undo_And_Viewer_OnPel039Copy()
    {
        var session = OpenCopy();
        CubicacionRow row = FirstEditableRow(session);
        string rowId = row.Id;
        string codigoAntes = row.Codigo;

        var undo = new WpfUndoService();
        undo.PushUndoPoint(session, "antes de fórmula");

        row.QtyExpressions = new[] { "10+2" };
        row.TotalExpression = "";
        row.Cantidad = new CubicacionTakeoffQueryService().EvaluateStoredExpressions(session, row);
        Assert.Equal(12m, row.Cantidad);
        session.SyncCubicacionRowComposition(row);

        SQLiteProjectStore.SaveSnapshot(_copyPath, session.CreateSnapshot());

        var reopened = OpenCopy();
        CubicacionRow persisted = reopened.Cubicacion.Single(r => r.Id == rowId);
        Assert.Equal("10+2", persisted.QtyExpressions[0]);
        Assert.Equal(12m, persisted.Cantidad);

        undo = new WpfUndoService();
        undo.PushUndoPoint(reopened, "antes de pegar");
        string codigoTrasFormula = persisted.Codigo;

        var pasted = new CubicacionRow
        {
            Id = Guid.NewGuid().ToString("N"),
            ItemId = persisted.ItemId,
            Tipo = "MAT",
            Codigo = "SMOKE-PASTE",
            Descripcion = "Pegado smoke",
            Unidad = "UN",
            TotalExpression = "3+1",
            Cantidad = CubicacionClipboardService.ParseCantidad("3+1")
        };
        Assert.Equal(4m, pasted.Cantidad);
        reopened.Cubicacion.Add(pasted);
        reopened.SyncCubicacionRowComposition(pasted);
        reopened.CreateSnapshot();
        Assert.Contains(reopened.Cubicacion, r => r.Id == pasted.Id && r.Cantidad == 4m);

        persisted.Codigo = "SMOKE-EDIT";
        reopened.SyncCubicacionRowComposition(persisted);
        Assert.True(undo.Undo(reopened));
        CubicacionRow afterUndo = reopened.Cubicacion.Single(r => r.Id == rowId);
        Assert.Equal(codigoTrasFormula, afterUndo.Codigo);
        Assert.DoesNotContain(reopened.Cubicacion, r => r.Id == pasted.Id);

        reopened.SetRoleReadOnly(true, "Rol Visor");
        Assert.True(reopened.IsReadOnly);
        var viewerEdit = Assert.Throws<InvalidOperationException>(() => reopened.SyncCubicacionRowComposition(afterUndo));
        Assert.Contains("solo lectura", viewerEdit.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(codigoAntes, codigoTrasFormula);
    }

    private BudgetProjectSession OpenCopy()
    {
        var session = new BudgetProjectSession();
        session.ApplySnapshot(SQLiteProjectStore.LoadSnapshot(_copyPath), _copyPath);
        return session;
    }

    private static CubicacionRow FirstEditableRow(BudgetProjectSession session)
    {
        CubicacionRow? row = session.Cubicacion.FirstOrDefault(r =>
            !string.IsNullOrWhiteSpace(r.Id) && !string.IsNullOrWhiteSpace(r.ItemId) && !string.IsNullOrWhiteSpace(r.Codigo));
        Assert.NotNull(row);
        return row!;
    }

    private static string ResolveSourceProject()
    {
        // Orden de preferencia: corpus versionado (determinista) y, solo si la compuerta
        // de proyectos vivos está habilitada, el archivo de obra real.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string nested = Path.Combine(dir.FullName, "tests", "QubiCostElectrical.Tests", "Fixtures", "MigrationCorpus", "corpus", "PEL039A2026-ABIERTO.qce");
            if (File.Exists(nested))
                return nested;
            dir = dir.Parent;
        }

        if (LiveProjectGate.TryResolveProject("PEL039A2026 ABIERTO.qce", out string live))
            return live;

        throw new FileNotFoundException("No está el corpus PEL039A2026-ABIERTO.qce ni un proyecto de obra habilitado por QUBI_LIVE_PROJECTS=1.");
    }
}
