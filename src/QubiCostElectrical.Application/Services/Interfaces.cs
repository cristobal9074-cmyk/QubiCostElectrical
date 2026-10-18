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
    List<CubicacionRow> GetRowsForItem(IReadOnlyDictionary<string, List<CubicacionRow>> index, string itemId, bool onlyWithCode = false);
    CubicacionRow? GetVisualRow(IEnumerable<CubicacionRow> cubicacion, string itemId, int visualIndex);
    CubicacionRow GetOrCreateVisualRow(List<CubicacionRow> cubicacion, string itemId, int visualIndex);
    void ClearRow(CubicacionRow row);
    void RemoveEmptyRowsForItem(List<CubicacionRow> cubicacion, string itemId);
    void ApplyMaterial(CubicacionRow row, MaterialRow material, decimal valorUfClp = 0m, ProjectInfoRow? project = null);
    /// <summary>Resincroniza MAT/MO de todas las filas cuyo código coincide con el catálogo del
    /// proyecto — se usa cuando cambia un precio en Materiales (p. ej. al aplicar el Banco de
    /// Precios) para que las líneas ya cubicadas reflejen el precio nuevo automáticamente.
    /// Con <paramref name="project"/> el MO se resuelve por rendimiento si el proyecto lo pide.</summary>
    int SyncPricesFromCatalog(IEnumerable<CubicacionRow> cubicacion, IEnumerable<MaterialRow> materiales, decimal valorUfClp, ProjectInfoRow? project = null);
    int SyncPricesFromCatalog(
        IEnumerable<CubicacionRow> cubicacion,
        IEnumerable<MaterialRow> materiales,
        decimal valorUfClp,
        IList<CubicacionValorizacionRow>? valorizaciones,
        ProjectInfoRow? project = null);
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

    BudgetVersionComparisonSummary CompareWithIncomingItemizado(
        IReadOnlyList<ItemizadoRow> incomingItems,
        ProjectFileData currentProject,
        string incomingLabel = "Aclaración / Addenda",
        string currentLabel = "Presupuesto Actual");
}

public interface IBudgetRevisionDiffPdfExporter
{
    void Export(BudgetVersionComparisonSummary summary, string outputPath);
}

public interface IBudgetRevisionDiffExcelExporter
{
    void Export(BudgetVersionComparisonSummary summary, string outputPath);
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

/// <summary>
/// Biblioteca global de conjuntos eléctricos (plantillas de takeoff), guardada fuera del
/// .qce para que todas las sesiones/proyectos nuevos la vean sin volver a crearla.
/// </summary>
public interface IConjuntoTemplateStore
{
    IReadOnlyList<ConjuntoElectricoRow> GetAll();
    /// <summary>Upsert de un conjunto sin tocar los demás.</summary>
    void Save(ConjuntoElectricoRow conjunto);
    /// <summary>Reemplaza la colección completa (upsert + elimina los que ya no están).</summary>
    void SaveAll(IReadOnlyList<ConjuntoElectricoRow> conjuntos);
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

    /// <summary>
    /// Formato de paridad (principal, secundario) para la moneda activa. Regla de producto:
    /// si la moneda es CLP, el principal es el monto en pesos y el secundario queda vacío
    /// (nunca se muestra UF). Para UF/USD/EUR el principal va en esa moneda y el secundario
    /// es el equivalente en CLP, para dar visibilidad del precio.
    /// </summary>
    (string Primary, string Secondary) FormatMoneyPairFromClp(decimal clp, string? currency, decimal valorUfClp, decimal tipoCambioUsdClp);
}

public interface IPurchaseListService
{
    IReadOnlyList<CompraMaterialViewModel> Build(IEnumerable<CubicacionRow> cubicacion, IEnumerable<MaterialRow> materiales, decimal toleranciaPct);
    IReadOnlyList<MaterialUsageBreakdown> GetMaterialUsage(string codigo, IEnumerable<CubicacionRow> cubicacion, IEnumerable<ItemizadoRow> itemizado);
}

public interface IMaterialControlService
{
    IReadOnlyList<MaterialControlRow> Build(
        IEnumerable<CubicacionRow> cubicacion,
        IEnumerable<MaterialRow> materiales,
        IEnumerable<SolicitudItemRow> solicitudes,
        decimal toleranciaPct = 5m);

    MaterialControlSummary GetSummary(IReadOnlyList<MaterialControlRow> rows);
}

public interface IApuService
{
    ApuRow CrearDesdeItem(ItemizadoRow item);
    List<string> Validar(ApuRow apu);
}

public interface IValidationService
{
    BudgetValidationResult ValidateProject(ProjectFileData project);
    BudgetValidationResult ValidateBudget(ProjectInfoRow proyecto, IEnumerable<ItemizadoRow> itemizado, IEnumerable<MaterialRow> materiales, IEnumerable<CubicacionRow> cubicacion, IEnumerable<TemplateRow> plantillas);
}
