using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public enum BudgetValidationSeverity
{
    Info,
    Warning,
    Error
}

public sealed record BudgetValidationIssue(
    BudgetValidationSeverity Severity,
    string Scope,
    string Code,
    string Message,
    string Reference = "");

public sealed class BudgetValidationResult
{
    public List<BudgetValidationIssue> Issues { get; } = new();

    public bool IsValid => ErrorCount == 0;
    public bool HasErrors => ErrorCount > 0;
    public bool HasWarnings => WarningCount > 0;
    public int ErrorCount => Issues.Count(x => x.Severity == BudgetValidationSeverity.Error);
    public int WarningCount => Issues.Count(x => x.Severity == BudgetValidationSeverity.Warning);
    public int InfoCount => Issues.Count(x => x.Severity == BudgetValidationSeverity.Info);

    public string Summary => $"{ErrorCount} errores · {WarningCount} advertencias · {InfoCount} info";

    public void Add(BudgetValidationSeverity severity, string scope, string code, string message, string reference = "")
    {
        Issues.Add(new BudgetValidationIssue(severity, scope, code, message, reference));
    }

    public void Merge(BudgetValidationResult other)
    {
        if (other == null) return;
        Issues.AddRange(other.Issues);
    }
}

public sealed class ValidationService : IValidationService
{
    public BudgetValidationResult ValidateProject(ProjectFileData project)
    {
        if (project == null)
        {
            var nullResult = new BudgetValidationResult();
            nullResult.Add(BudgetValidationSeverity.Error, "Proyecto", "VAL-PRJ-000", "El proyecto no puede ser nulo.");
            return nullResult;
        }

        var result = ValidateBudget(
            project.Proyecto,
            project.Itemizado,
            project.Materiales,
            project.Cubicacion,
            project.Plantillas);

        ValidateVersions(project.Versiones, result);
        ValidateApus(project.Apus, project.Itemizado, result);
        return result;
    }

    public BudgetValidationResult ValidateBudget(
        ProjectInfoRow proyecto,
        IEnumerable<ItemizadoRow> itemizado,
        IEnumerable<MaterialRow> materiales,
        IEnumerable<CubicacionRow> cubicacion,
        IEnumerable<TemplateRow> plantillas)
    {
        var result = new BudgetValidationResult();
        var itemList = (itemizado ?? Enumerable.Empty<ItemizadoRow>()).ToList();
        var materialList = (materiales ?? Enumerable.Empty<MaterialRow>()).ToList();
        var cubList = (cubicacion ?? Enumerable.Empty<CubicacionRow>()).ToList();
        var templateList = (plantillas ?? Enumerable.Empty<TemplateRow>()).ToList();

        ValidateProjectInfo(proyecto ?? new ProjectInfoRow(), result);
        ValidateItemizado(itemList, result);
        ValidateMateriales(materialList, result);
        ValidateCubicacion(cubList, itemList, materialList, result);
        ValidatePlantillas(templateList, materialList, result);

        if (itemList.Count == 0 && materialList.Count == 0 && cubList.Count == 0)
        {
            result.Add(
                BudgetValidationSeverity.Warning,
                "Proyecto",
                "VAL-PRJ-EMPTY",
                "El proyecto está vacío. No hay itemizado, materiales ni cubicación.");
        }

        return result;
    }

    private static void ValidateApus(IEnumerable<ApuRow> apus, IEnumerable<ItemizadoRow> itemizado, BudgetValidationResult result)
    {
        var apuList = (apus ?? Enumerable.Empty<ApuRow>()).ToList();
        if (apuList.Count == 0) return;

        var itemIds = (itemizado ?? Enumerable.Empty<ItemizadoRow>())
            .Select(x => x.Id)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var apu in apuList)
        {
            string reference = string.IsNullOrWhiteSpace(apu.Id) ? apu.ItemId : apu.Id;
            if (string.IsNullOrWhiteSpace(apu.Id))
                result.Add(BudgetValidationSeverity.Error, "APU", "VAL-APU-ROW-001", "APU sin Id persistente.", reference);
            else if (!ids.Add(apu.Id.Trim()))
                result.Add(BudgetValidationSeverity.Error, "APU", "VAL-APU-ROW-002", $"APU duplicado: {apu.Id}", reference);

            if (string.IsNullOrWhiteSpace(apu.ItemId))
                result.Add(BudgetValidationSeverity.Error, "APU", "VAL-APU-ROW-003", "APU sin partida asociada.", reference);
            else if (itemIds.Count > 0 && !itemIds.Contains(apu.ItemId))
                result.Add(BudgetValidationSeverity.Warning, "APU", "VAL-APU-ROW-004", $"APU asociado a partida inexistente: {apu.ItemId}", reference);

            if (string.IsNullOrWhiteSpace(apu.Descripcion))
                result.Add(BudgetValidationSeverity.Warning, "APU", "VAL-APU-ROW-005", "APU sin descripción.", reference);

            if (string.IsNullOrWhiteSpace(apu.Unidad))
                result.Add(BudgetValidationSeverity.Warning, "APU", "VAL-APU-ROW-006", "APU sin unidad.", reference);

            if (apu.Recursos == null || apu.Recursos.Count == 0)
                result.Add(BudgetValidationSeverity.Error, "APU", "VAL-APU-ROW-007", "APU sin recursos.", reference);

            if (apu.HerramientasPct < 0m || apu.HerramientasPct > 30m)
                result.Add(BudgetValidationSeverity.Warning, "APU", "VAL-APU-ROW-008", "% de herramientas fuera de rango recomendado 0–30%.", reference);

            if (apu.ImprevistosAlzaPct < 0m || apu.ImprevistosAlzaPct > 20m)
                result.Add(BudgetValidationSeverity.Warning, "APU", "VAL-APU-ROW-009", "% de imprevistos fuera de rango recomendado 0–20%.", reference);

            foreach (var recurso in apu.Recursos ?? Enumerable.Empty<ApuRecursoRow>())
            {
                if (string.IsNullOrWhiteSpace(recurso.Descripcion))
                    result.Add(BudgetValidationSeverity.Error, "APU", "VAL-APU-RES-010", "Recurso APU sin descripción.", reference);

                if (string.IsNullOrWhiteSpace(recurso.Unidad))
                    result.Add(BudgetValidationSeverity.Warning, "APU", "VAL-APU-RES-011", $"Recurso {recurso.Descripcion} sin unidad.", reference);

                if (recurso.Rendimiento <= 0m)
                    result.Add(BudgetValidationSeverity.Error, "APU", "VAL-APU-RES-012", $"Recurso {recurso.Descripcion} tiene rendimiento menor o igual a cero.", reference);

                if (recurso.PrecioUf < 0m)
                    result.Add(BudgetValidationSeverity.Error, "APU", "VAL-APU-RES-013", $"Recurso {recurso.Descripcion} tiene precio negativo.", reference);
            }
        }
    }

    private static void ValidateProjectInfo(ProjectInfoRow proyecto, BudgetValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(proyecto.Nombre) || proyecto.Nombre.Trim().Equals("Proyecto sin nombre", StringComparison.OrdinalIgnoreCase))
            result.Add(BudgetValidationSeverity.Warning, "Proyecto", "VAL-PRJ-001", "El proyecto no tiene nombre real.");

        if (string.IsNullOrWhiteSpace(proyecto.Cliente) || proyecto.Cliente.Trim().Equals("Cliente sin nombre", StringComparison.OrdinalIgnoreCase))
            result.Add(BudgetValidationSeverity.Warning, "Proyecto", "VAL-PRJ-002", "El proyecto no tiene cliente definido.");

        if (proyecto.GastosGeneralesPct < 0m)
            result.Add(BudgetValidationSeverity.Error, "Proyecto", "VAL-PRJ-003", "El porcentaje de gastos generales no puede ser negativo.");

        if (proyecto.UtilidadPct < 0m)
            result.Add(BudgetValidationSeverity.Error, "Proyecto", "VAL-PRJ-004", "El porcentaje de utilidad no puede ser negativo.");

        if (proyecto.MetrosCuadrados < 0m)
            result.Add(BudgetValidationSeverity.Error, "Proyecto", "VAL-PRJ-005", "Los m² no pueden ser negativos.");

        string moneda = string.IsNullOrWhiteSpace(proyecto.MonedaPresupuesto) ? "CLP" : proyecto.MonedaPresupuesto.Trim().ToUpperInvariant();
        if (moneda is not ("UF" or "CLP" or "USD"))
            result.Add(BudgetValidationSeverity.Error, "Proyecto", "VAL-PRJ-006", $"Moneda no soportada: {proyecto.MonedaPresupuesto}");

        // CLP no requiere UF; solo USD necesita ambas tasas para convertir correctamente
        if (moneda == "USD" && proyecto.ValorUfClp <= 0m)
            result.Add(BudgetValidationSeverity.Error, "Proyecto", "VAL-PRJ-007", "Para USD debes configurar valor UF en CLP.");

        if (moneda == "USD" && proyecto.TipoCambioUsdClp <= 0m)
            result.Add(BudgetValidationSeverity.Error, "Proyecto", "VAL-PRJ-008", "Para USD debes configurar tipo de cambio USD/CLP.");
    }

    private static void ValidateItemizado(IReadOnlyList<ItemizadoRow> itemizado, BudgetValidationResult result)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in itemizado)
        {
            string reference = string.IsNullOrWhiteSpace(item.Id) ? item.Item : item.Id;

            if (string.IsNullOrWhiteSpace(item.Id))
                result.Add(BudgetValidationSeverity.Error, "Itemizado", "VAL-ITM-001", "Partida sin Id interno.", reference);
            else if (!ids.Add(item.Id.Trim()))
                result.Add(BudgetValidationSeverity.Error, "Itemizado", "VAL-ITM-002", $"Id de partida duplicado: {item.Id}", reference);

            if (!item.EsTitulo)
            {
                if (string.IsNullOrWhiteSpace(item.Descripcion))
                    result.Add(BudgetValidationSeverity.Error, "Itemizado", "VAL-ITM-003", "Partida sin descripción.", reference);

                if (string.IsNullOrWhiteSpace(item.Unidad))
                    result.Add(BudgetValidationSeverity.Warning, "Itemizado", "VAL-ITM-004", "Partida sin unidad.", reference);

                if (item.Cantidad < 0m)
                    result.Add(BudgetValidationSeverity.Warning, "Itemizado", "VAL-ITM-005", "Partida con cantidad negativa.", reference);
            }
        }
    }

    private static void ValidateMateriales(IReadOnlyList<MaterialRow> materiales, BudgetValidationResult result)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var material in materiales)
        {
            string code = material.Codigo?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(code))
            {
                result.Add(BudgetValidationSeverity.Error, "Materiales", "VAL-MAT-001", "Material sin código.");
                continue;
            }

            if (!codes.Add(code))
                result.Add(BudgetValidationSeverity.Error, "Materiales", "VAL-MAT-002", $"Código de material duplicado: {code}", code);

            if (string.IsNullOrWhiteSpace(material.Descripcion))
                result.Add(BudgetValidationSeverity.Warning, "Materiales", "VAL-MAT-003", "Material sin descripción.", code);

            if (string.IsNullOrWhiteSpace(material.Unidad))
                result.Add(BudgetValidationSeverity.Warning, "Materiales", "VAL-MAT-004", "Material sin unidad.", code);

            if (material.MatUf < 0m || material.MoUf < 0m)
                result.Add(BudgetValidationSeverity.Error, "Materiales", "VAL-MAT-005", "Material con precio negativo.", code);

            if (material.MatUf == 0m && material.MoUf == 0m)
                result.Add(BudgetValidationSeverity.Warning, "Materiales", "VAL-MAT-006", "Material sin precio de material ni mano de obra.", code);
        }
    }

    private static void ValidateCubicacion(
        IReadOnlyList<CubicacionRow> cubicacion,
        IReadOnlyList<ItemizadoRow> itemizado,
        IReadOnlyList<MaterialRow> materiales,
        BudgetValidationResult result)
    {
        var itemIds = itemizado.Select(x => x.Id).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var materialCodes = materiales.Select(x => x.Codigo).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rowIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in cubicacion)
        {
            string rowRef = string.IsNullOrWhiteSpace(row.Id) ? row.Codigo : row.Id;

            if (string.IsNullOrWhiteSpace(row.Id))
                result.Add(BudgetValidationSeverity.Warning, "Cubicación", "VAL-CUB-001", "Línea de cubicación sin Id persistente.", rowRef);
            else if (!rowIds.Add(row.Id.Trim()))
                result.Add(BudgetValidationSeverity.Error, "Cubicación", "VAL-CUB-002", $"Id de línea duplicado: {row.Id}", rowRef);

            if (string.IsNullOrWhiteSpace(row.ItemId))
                result.Add(BudgetValidationSeverity.Error, "Cubicación", "VAL-CUB-003", "Línea sin partida asociada.", rowRef);
            else if (itemIds.Count > 0 && !itemIds.Contains(row.ItemId))
                result.Add(BudgetValidationSeverity.Error, "Cubicación", "VAL-CUB-004", $"La partida asociada no existe: {row.ItemId}", rowRef);

            if (string.IsNullOrWhiteSpace(row.Codigo))
                result.Add(BudgetValidationSeverity.Error, "Cubicación", "VAL-CUB-005", "Línea sin código de recurso.", rowRef);
            else if (materialCodes.Count > 0 && !materialCodes.Contains(row.Codigo))
                result.Add(BudgetValidationSeverity.Warning, "Cubicación", "VAL-CUB-006", $"Recurso no existe en lista de precios: {row.Codigo}", rowRef);

            if (string.IsNullOrWhiteSpace(row.Unidad))
                result.Add(BudgetValidationSeverity.Warning, "Cubicación", "VAL-CUB-007", "Línea sin unidad.", rowRef);

            if (row.Cantidad == 0m)
                result.Add(BudgetValidationSeverity.Warning, "Cubicación", "VAL-CUB-008", "Línea con cantidad cero.", rowRef);
            else if (row.Cantidad < 0m)
                result.Add(BudgetValidationSeverity.Info, "Cubicación", "VAL-CUB-009", "Línea con cantidad negativa. Se asume descuento o ajuste.", rowRef);

            if (row.MatUf < 0m || row.MoUf < 0m)
                result.Add(BudgetValidationSeverity.Error, "Cubicación", "VAL-CUB-010", "Línea con costo unitario negativo.", rowRef);

            if (row.MatUf == 0m && row.MoUf == 0m)
                result.Add(BudgetValidationSeverity.Warning, "Cubicación", "VAL-CUB-011", "Línea sin valorización material ni mano de obra.", rowRef);

            if (string.IsNullOrWhiteSpace(row.Area))
                result.Add(BudgetValidationSeverity.Info, "Cubicación", "VAL-CUB-012", "Línea sin área. Se recomienda usar 'General' o área real.", rowRef);
        }
    }

    private static void ValidatePlantillas(IReadOnlyList<TemplateRow> plantillas, IReadOnlyList<MaterialRow> materiales, BudgetValidationResult result)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var materialCodes = materiales.Select(x => x.Codigo).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var templateNames = plantillas.Select(x => x.Nombre).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var template in plantillas)
        {
            string name = template.Nombre?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name))
            {
                result.Add(BudgetValidationSeverity.Error, "Plantillas", "VAL-TPL-001", "Plantilla sin nombre.");
                continue;
            }

            if (!names.Add(name))
                result.Add(BudgetValidationSeverity.Error, "Plantillas", "VAL-TPL-002", $"Nombre de plantilla duplicado: {name}", name);

            var templateItems = template.Items ?? new List<TemplateItemRow>();
            if (templateItems.Count == 0)
                result.Add(BudgetValidationSeverity.Warning, "Plantillas", "VAL-TPL-003", "Plantilla sin recursos.", name);

            foreach (var item in templateItems)
            {
                string clase = string.IsNullOrWhiteSpace(item.Clase) ? "MATERIAL" : item.Clase.Trim().ToUpperInvariant();

                if (item.Factor < 0m)
                    result.Add(BudgetValidationSeverity.Error, "Plantillas", "VAL-TPL-004", "Plantilla con factor negativo.", name);

                if (item.Minimo < 0m)
                    result.Add(BudgetValidationSeverity.Error, "Plantillas", "VAL-TPL-005", "Plantilla con mínimo negativo.", name);

                if (clase == "PLANTILLA")
                {
                    if (string.IsNullOrWhiteSpace(item.SubPlantilla))
                        result.Add(BudgetValidationSeverity.Error, "Plantillas", "VAL-TPL-006", "Sub-plantilla sin nombre destino.", name);
                    else if (!templateNames.Contains(item.SubPlantilla))
                        result.Add(BudgetValidationSeverity.Warning, "Plantillas", "VAL-TPL-007", $"Sub-plantilla no existe: {item.SubPlantilla}", name);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(item.Codigo))
                        result.Add(BudgetValidationSeverity.Error, "Plantillas", "VAL-TPL-008", "Recurso de plantilla sin código.", name);
                    else if (materialCodes.Count > 0 && !materialCodes.Contains(item.Codigo))
                        result.Add(BudgetValidationSeverity.Warning, "Plantillas", "VAL-TPL-009", $"Material de plantilla no existe en lista de precios: {item.Codigo}", name);
                }
            }
        }
    }

    private static void ValidateVersions(IEnumerable<BudgetVersionRow> versiones, BudgetValidationResult result)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var version in versiones ?? Enumerable.Empty<BudgetVersionRow>())
        {
            string code = version.Codigo?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(code))
                result.Add(BudgetValidationSeverity.Warning, "Versiones", "VAL-VER-001", "Versión sin código.", version.Id);
            else if (!codes.Add(code))
                result.Add(BudgetValidationSeverity.Warning, "Versiones", "VAL-VER-002", $"Código de versión duplicado: {code}", version.Id);

            if (version.Snapshot == null)
                result.Add(BudgetValidationSeverity.Warning, "Versiones", "VAL-VER-003", "Versión sin snapshot guardado.", version.Id);
        }
    }
}
