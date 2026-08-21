using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;


public sealed record CubicacionCloneOptions(
    bool ReplaceTarget = true,
    bool CopyQtyHeaders = true,
    bool CopyCellComments = true);

public sealed record CubicacionCloneResult(
    int RowsCopied,
    int RowsRemoved,
    bool ReplacedTarget,
    bool QtyHeadersCopied,
    int CellCommentsCopied,
    string SourceItemId,
    string TargetItemId);

public interface ICubicacionService
{
    List<CubicacionRow> GetRowsForItem(IEnumerable<CubicacionRow> cubicacion, string itemId, bool onlyWithCode = false);
    CubicacionRow? GetVisualRow(IEnumerable<CubicacionRow> cubicacion, string itemId, int visualIndex);
    CubicacionRow GetOrCreateVisualRow(List<CubicacionRow> cubicacion, string itemId, int visualIndex);
    void ClearRow(CubicacionRow row);
    void RemoveEmptyRowsForItem(List<CubicacionRow> cubicacion, string itemId);
    void ApplyMaterial(CubicacionRow row, MaterialRow material, decimal valorUfClp = 0m);
    /// <summary>Resincroniza MAT/MO de todas las filas cuyo código coincide con el catálogo del
    /// proyecto — se usa cuando cambia un precio en Materiales (p. ej. al aplicar el Banco de
    /// Precios) para que las líneas ya cubicadas reflejen el precio nuevo automáticamente.</summary>
    int SyncPricesFromCatalog(IEnumerable<CubicacionRow> cubicacion, IEnumerable<MaterialRow> materiales, decimal valorUfClp);
    int SyncPricesFromCatalog(
        IEnumerable<CubicacionRow> cubicacion,
        IEnumerable<MaterialRow> materiales,
        decimal valorUfClp,
        IList<CubicacionValorizacionRow>? valorizaciones);
    CubicacionRow CloneRowForItem(CubicacionRow source, string targetItemId);
    CubicacionCloneResult CloneItemCubicacion(
        List<CubicacionRow> cubicacion,
        string sourceItemId,
        string targetItemId,
        Dictionary<string, string[]>? qtyHeadersByItem = null,
        IList<CubicacionCellCommentRow>? cellComments = null,
        CubicacionCloneOptions? options = null);
    string DefaultQtyHeaderLabel(int index);
    string NormalizeQtyHeaderLabel(int index, string? value);
    string[] GetHeadersForItem(Dictionary<string, string[]> headersByItem, string itemId, int columnCount);
    QtyHeaderUpdateResult SaveHeadersForItem(Dictionary<string, string[]> headersByItem, string itemId, IReadOnlyList<string?> labels, int columnCount);
    bool SameQtyHeaders(string[]? left, string[] right, int columnCount);
    bool AllQtyHeadersAreDefaults(string[] values, int columnCount);
}

public interface IItemizadoService
{
    IReadOnlyList<ItemizadoTreeRow> FilterForCubicacion(IEnumerable<ItemizadoRow> itemizado, string filter, string itemFilter);
    int CountPartidas(IEnumerable<ItemizadoRow> itemizado);
    int CountTitulos(IEnumerable<ItemizadoRow> itemizado);
    string FirstEditableItemId(IEnumerable<ItemizadoRow> itemizado);
    ItemizadoMutationResult AddBelow(List<ItemizadoRow> itemizado, int selectedIndex, string desc, string unidad);
    ItemizadoMutationResult AddTitle(List<ItemizadoRow> itemizado, int selectedIndex = -1);
    ItemizadoMutationResult Move(List<ItemizadoRow> itemizado, int selectedIndex, int direction);
    ItemizadoMutationResult DuplicateAt(List<ItemizadoRow> itemizado, int selectedIndex);
    ItemizadoMutationResult DeleteAt(List<ItemizadoRow> itemizado, List<CubicacionRow> cubicacion, Dictionary<string, string[]> qtyHeadersByItem, int selectedIndex);
    void Clear(List<ItemizadoRow> itemizado, List<CubicacionRow> cubicacion, Dictionary<string, string[]> qtyHeadersByItem);
    void ReplaceAll(List<ItemizadoRow> itemizado, List<CubicacionRow> cubicacion, Dictionary<string, string[]> qtyHeadersByItem, IEnumerable<ItemizadoRow> newRows);
    void SyncEditableRows(List<ItemizadoRow> itemizado, IReadOnlyList<ItemizadoEditableCell> cells);
    void ApplyAssistedPartidaDetails(List<ItemizadoRow> itemizado, int rowIndex, string code, string description, string unit);
    string ParentOf(string id, IReadOnlyList<ItemizadoRow>? rows = null);
    string NextChildId(IEnumerable<ItemizadoRow> itemizado, string parent);
}

public interface IProjectRepository
{
    void SaveProjectFile(string path, ProjectFileData data, System.Text.Json.JsonSerializerOptions options);
    ProjectFileData LoadProjectFile(string path, System.Text.Json.JsonSerializerOptions options);
    Task SaveProjectFileAsync(string path, ProjectFileData data, System.Text.Json.JsonSerializerOptions options, CancellationToken cancellationToken = default);
    Task<ProjectFileData> LoadProjectFileAsync(string path, System.Text.Json.JsonSerializerOptions options, CancellationToken cancellationToken = default);
    string GetSQLiteDatabasePathForProject(string projectPath, string fallbackDirectory, string fallbackProjectName);
    void SaveSQLiteSnapshot(string databasePath, ProjectFileData data);
    void SaveSQLiteIncremental(string databasePath, ProjectFileData data, string[] scopes);
    Task SaveSQLiteSnapshotAsync(string databasePath, ProjectFileData data, CancellationToken cancellationToken = default);
    Task SaveSQLiteIncrementalAsync(string databasePath, ProjectFileData data, string[] scopes, CancellationToken cancellationToken = default);
    ProjectFileData LoadSQLiteSnapshot(string databasePath);
    Task<ProjectFileData> LoadSQLiteSnapshotAsync(string databasePath, CancellationToken cancellationToken = default);
    DateTime GetSQLiteLastSavedAt(string databasePath);
    string? PeekSQLiteEstadoPresupuesto(string databasePath);
    string CreateNumberedSQLiteBackup(string databasePath, int slots, string reason);
    Task<string> CreateNumberedSQLiteBackupAsync(string databasePath, int slots, string reason, CancellationToken cancellationToken = default);
    string GetLatestUsableSQLiteBackup(string databasePath);
    bool RestoreSQLiteBackup(string backupPath, string databasePath);
    void ClearSQLitePools();
}

public interface IVersionComparisonService
{
    BudgetVersionComparisonSummary Compare(
        BudgetVersionData baseline,
        ProjectFileData current,
        string baselineLabel = "Versión base",
        string currentLabel = "Estado actual");

    BudgetVersionComparisonSummary Compare(
        BudgetVersionData baseline,
        BudgetVersionData current,
        string baselineLabel,
        string currentLabel);
}

public interface IPriceBankRepository
{
    System.Collections.Generic.IReadOnlyList<PriceBankEntry> GetAll();
    PriceBankEntry? GetByCodigo(string codigo);
    System.Collections.Generic.IReadOnlyList<PriceBankEntry> Search(string text);
    void Upsert(PriceBankEntry entry);
    bool Delete(string codigo);
    /// <summary>Elimina TODOS los precios del banco. Irreversible.</summary>
    int DeleteAll();
    int UpsertMany(System.Collections.Generic.IEnumerable<PriceBankEntry> entries);
    int Count();
    int SeedIfEmpty(System.Collections.Generic.IEnumerable<PriceBankEntry> entries);
}

public interface IPriceBankStore
{
    System.Collections.Generic.IReadOnlyList<PriceBankEntry> GetAll();
    PriceBankEntry? GetByCodigo(string codigo);
    System.Collections.Generic.IReadOnlyList<PriceBankEntry> Search(string text);
    void Upsert(PriceBankEntry entry);
    bool Delete(string codigo);
    /// <summary>Elimina TODOS los precios del banco. Irreversible.</summary>
    int DeleteAll();
    int UpsertMany(System.Collections.Generic.IEnumerable<PriceBankEntry> entries);
    int Count();
    /// <summary>Inserta las entradas solo si el banco está vacío. Idempotente: no actúa si ya hay datos.</summary>
    int SeedIfEmpty(System.Collections.Generic.IEnumerable<PriceBankEntry> entries);
}

public interface IApuTemplateStore
{
    System.Collections.Generic.IReadOnlyList<ApuTemplateRow> GetAll();
    System.Collections.Generic.IReadOnlyList<ApuTemplateRow> Search(string text);
    void Save(ApuTemplateRow template);
    void Delete(string id);
}

public interface IPartidaCompletionService
{
    PartidaCompletionSummary Build(IEnumerable<ItemizadoRow>? itemizado, IEnumerable<CubicacionRow>? cubicacion);
    PartidaCompletionState GetPartidaState(string itemId, IEnumerable<CubicacionRow> cubicacion);
    string GetIcon(PartidaCompletionState state);
    string GetTooltip(PartidaCompletionState state);
    decimal GetLineCompletionPct(string itemId, IEnumerable<CubicacionRow> cubicacion);
}

public interface IBudgetCalculationService
{
    void MarkDirtyItem(string? itemId);
    void MarkDirtyItems(IEnumerable<string>? itemIds);
    void ResetIncrementalCache();
    CostBreakdown CalculateBudget(IEnumerable<CubicacionRow> cubicacion, BudgetSettings settings, IEnumerable<ApuRow>? apus = null, IEnumerable<ItemizadoRow>? itemizado = null, IEnumerable<ChangeOrderRow>? changeOrders = null);
    (decimal Material, decimal ManoObra, decimal Total) GetProjectTotals(IEnumerable<CubicacionRow> cubicacion);
    (decimal Material, decimal ManoObra, decimal Total) GetDirectCostTotals(IEnumerable<CubicacionRow> rows);
    List<(string Area, decimal Material, decimal ManoObra, decimal Total)> GetAreaCostBreakdown(IEnumerable<CubicacionRow> cubicacion);
    (decimal Directo, decimal GastosGenerales, decimal Subtotal, decimal Utilidad, decimal Venta, decimal UfM2) GetCostBreakdown(IEnumerable<CubicacionRow> cubicacion, decimal gastosGeneralesPct, decimal utilidadPct, decimal metrosCuadrados);
    List<(string Area, decimal Total)> GetAreaTotals(IEnumerable<CubicacionRow> cubicacion);
    List<(string Codigo, string Descripcion, string Unidad, decimal Cantidad, decimal Total)> GetMaterialTotals(IEnumerable<CubicacionRow> cubicacion);
}

public interface IMaterialSearchService
{
    string Normalize(string value);
    List<(MaterialRow Material, string CodigoKey, string DescripcionKey, string SearchKey)> BuildIndex(IEnumerable<MaterialRow> materiales);
    List<MaterialRow> FindMatches(string search, IReadOnlyList<MaterialRow> materiales, IReadOnlyList<(MaterialRow Material, string CodigoKey, string DescripcionKey, string SearchKey)> index, int limit = 50);
    List<MaterialRow> FindMatches(string search, IReadOnlyList<MaterialRow> materiales, IReadOnlyList<(MaterialRow Material, string CodigoKey, string DescripcionKey, string SearchKey)> index, MaterialCatalogFilter? filter, int limit = 50);
    bool MatchesFilter(MaterialRow material, MaterialCatalogFilter? filter);
    string GetHierarchy(MaterialRow material);
    IReadOnlyList<MaterialCatalogQuickFilter> BuildQuickFilters(IEnumerable<MaterialRow> matches, int max = 8);
}

public interface IBudgetKpiService
{
    IReadOnlyList<DashboardKpiViewModel> BuildDashboardKpis(decimal totalVentaUf, decimal materialesUf, decimal manoObraUf, decimal ufM2, bool metrosCuadradosConfigurados);
    string FormatKpiUf(decimal uf);
    string FormatKpiDelta(decimal pct);
    (decimal Pct, string Code)? GetDelta(string metricKey, decimal currentValue, IEnumerable<BudgetVersionRow> versions);
    List<decimal> GetHistoryValues(string metricKey, decimal currentValue, IEnumerable<BudgetVersionRow> versions);
    decimal GetMetricValue(string metricKey, BudgetVersionData snapshot);
}

public interface ICurrencyConversionService
{
    string NormalizeCurrency(string? currency);
    string GetNumberFormat(string? currency);
    decimal ConvertUfToClp(decimal uf, decimal valorUfClp);
    decimal ConvertClpToUf(decimal clp, decimal valorUfClp);
    decimal ConvertUfToUsd(decimal uf, decimal valorUfClp, decimal tipoCambioUsdClp);
    decimal ConvertUsdToUf(decimal usd, decimal valorUfClp, decimal tipoCambioUsdClp);
    decimal ConvertUfToCurrency(decimal uf, string? currency, decimal valorUfClp, decimal tipoCambioUsdClp);
    decimal ConvertCurrencyToUf(decimal value, string? currency, decimal valorUfClp, decimal tipoCambioUsdClp);
    decimal ConvertClpToCurrency(decimal clp, string? currency, decimal valorUfClp, decimal tipoCambioUsdClp);
    string FormatMoneyFromUf(decimal uf, string? currency, decimal valorUfClp, decimal tipoCambioUsdClp);
    string FormatMoneyFromUfCompact(decimal uf, string? currency, decimal valorUfClp, decimal tipoCambioUsdClp);
    string FormatMoneyFromClp(decimal clp, string? currency, decimal valorUfClp, decimal tipoCambioUsdClp);
}

public interface ICubicacionViewModelService
{
    CubicacionViewModel Build(IEnumerable<CubicacionRow> cubicacion, string itemId = "", decimal gastosGeneralesPct = 0m, decimal utilidadPct = 0m, decimal metrosCuadrados = 0m);
    CubicacionLineViewModel ToLine(CubicacionRow row);
    CostSummaryViewModel BuildSummary(IEnumerable<CubicacionLineViewModel> rows, decimal gastosGeneralesPct, decimal utilidadPct, decimal metrosCuadrados);
}

public interface IProyectoViewModelService
{
    ProyectoViewModel Build(string nombre, string cliente, string ubicacion, string usuarioNombre, string usuarioRol, string moneda, string estadoPresupuesto, DateTime fechaCreacion, DateTime fechaModificacion, decimal valorUfClp, decimal tipoCambioUsdClp, decimal gastosGeneralesPct, decimal utilidadPct, decimal metrosCuadrados, IEnumerable<ItemizadoRow> itemizado, IEnumerable<MaterialRow> materiales, IEnumerable<CubicacionRow> cubicacion);
}



public interface ITemplateService
{
    TemplateExpansionResult BuildCubicacionRowsFromTemplate(TemplateRow template, ItemizadoRow partida, IEnumerable<TemplateRow> plantillas, IEnumerable<MaterialRow> materiales);
    List<CubicacionRow> MergeTemplateRows(IEnumerable<CubicacionRow> rows);
}

public interface IPurchaseListService
{
    IReadOnlyList<CompraMaterialViewModel> Build(IEnumerable<CubicacionRow> cubicacion, IEnumerable<MaterialRow> materiales, decimal toleranciaPct);
}

public interface IApuService
{
    ApuRow CrearDesdeItem(ItemizadoRow item);
    ApuRow MigrarDesde(CubicacionRow cub, string descripcionItem, string unidadItem);
    (decimal MatUf, decimal MoUf) GetPreciosParaCubicacion(ApuRow apu);
    List<ApuResumenRow> GetResumenProyecto(IEnumerable<ApuRow> apus, IEnumerable<ItemizadoRow> itemizado);
    (decimal Mat, decimal Mo, decimal Equipo, decimal Sc, decimal Herramientas, decimal Imprevistos, decimal Total) GetTotalesProyecto(IEnumerable<ApuRow> apus, IEnumerable<ItemizadoRow> itemizado);
    List<string> Validar(ApuRow apu);
}

public interface IApuViewModelService
{
    ApuViewModel BuildFromItem(ItemizadoRow? item, IEnumerable<CubicacionRow> cubicacion, decimal gastosGeneralesPct, decimal utilidadPct);
}
public interface IValidationService
{
    BudgetValidationResult ValidateProject(ProjectFileData project);
    BudgetValidationResult ValidateBudget(ProjectInfoRow proyecto, IEnumerable<ItemizadoRow> itemizado, IEnumerable<MaterialRow> materiales, IEnumerable<CubicacionRow> cubicacion, IEnumerable<TemplateRow> plantillas);
    BudgetValidationResult ValidateApu(ApuViewModel apu);
}
