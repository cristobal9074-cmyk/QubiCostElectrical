using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace QubiCostElectrical;

public sealed partial class BudgetProjectSession
{
    public int GetScopeVersion(string scope)
    {
        string normalized = NormalizeScope(scope);
        return scopeVersions.TryGetValue(normalized, out int version) ? version : 0;
    }

    public IReadOnlyCollection<string> GetDirtyScopes() => dirtyScopes.ToArray();

    public bool HasDirtyScope(string scope)
    {
        string normalized = NormalizeScope(scope);
        return dirtyScopes.Contains(normalized);
    }

    public void ClearDirtyScopes(IEnumerable<string>? scopes = null)
    {
        if (scopes == null)
        {
            dirtyScopes.Clear();
            return;
        }

        foreach (string scope in scopes)
            dirtyScopes.Remove(NormalizeScope(scope));
    }

    public ProjectInfoRow ProjectInfo
    {
        get
        {
            data.Proyecto ??= new ProjectInfoRow();
            EnsureProjectInfoIntegrity(data.Proyecto);
            return data.Proyecto;
        }
    }

    public List<ItemizadoRow> Itemizado => Data.Itemizado;
    public List<MaterialRow> Materiales => Data.Materiales;
    public List<CubicacionRow> Cubicacion => Data.Cubicacion;
    public List<TemplateRow> Plantillas => Data.Plantillas;
    public List<BudgetVersionRow> Versiones => Data.Versiones;
    public List<ApuRow> Apus => Data.Apus;
    public List<ChangeOrderRow> OrdenesCambio => Data.OrdenesCambio;
    public Dictionary<string, string[]> QtyHeadersByItem => Data.QtyHeadersByItem;
    public List<ZonaProyecto> ZonasProyecto => Data.Zonas;
    public List<MeasurementLineRow> Mediciones => Data.Mediciones;
    public List<HojaCubicacionItem> HojaCubicacion => Data.HojaCubicacion;
    public List<SolicitudItemRow> Solicitudes => Data.Solicitudes;
    public List<BudgetStatusHistoryRow> BudgetStatusHistory => ProjectInfo.HistorialEstados;
    public List<CubicacionCellCommentRow> CubicacionCellComments => ProjectInfo.CubicacionCellComments;
    public List<TechnicalValidationRuleRow> TechnicalValidationRules => ProjectInfo.TechnicalValidationRules;
    public List<RealCostRow> RealCosts => ProjectInfo.RealCosts;
    public List<PartidaWorkflowRow> PartidaWorkflow => ProjectInfo.PartidaWorkflow;
    public List<PartidaChangeTimelineRow> PartidaTimeline => ProjectInfo.PartidaTimeline;
    public List<ProactiveAlertRuleRow> ProactiveAlertRules => ProjectInfo.ProactiveAlertRules;
    public List<ConjuntoElectricoRow> ConjuntosElectricos => Data.ConjuntosElectricos;
    public List<CubicacionRecursoRow> CubicacionRecursos
    {
        get
        {
            EnsureIntegrity(data);
            return Data.CubicacionRecursos;
        }
    }
    public List<CubicacionZonaCantidadRow> CubicacionZonaCantidades
    {
        get
        {
            EnsureIntegrity(data);
            return Data.CubicacionZonaCantidades;
        }
    }
    public List<CubicacionValorizacionRow> CubicacionValorizaciones
    {
        get
        {
            EnsureIntegrity(data);
            return Data.CubicacionValorizaciones;
        }
    }
    public List<PartidaApuVinculoRow> PartidaApuVinculos => Data.PartidaApuVinculos;
    public List<CuadroCargaSCRow> CubicacionSC => Data.CubicacionSC;
    public CubicacionScConfig CubicacionScConfig
    {
        get => Data.CubicacionScConfig ??= CubicacionScConfig.Default();
        set => Data.CubicacionScConfig = value ?? CubicacionScConfig.Default();
    }
    public List<AntecedenteTecnicoRow> AntecedentesTecnicos => ProjectInfo.AntecedentesTecnicos;
    public LaborDaySettingsRow LaborDaySettings => ProjectInfo.LaborDaySettings;
    public Dictionary<string, decimal> ProjectVariables => ProjectInfo.Variables;

    public DateTime LastSQLiteSavedAt => ProjectInfo.LastSQLiteSavedAt;

    public void SetSQLiteSavedAt(DateTime savedAt)
    {
        EnsureWritable("actualizar el estado de guardado");
        ProjectInfo.LastSQLiteSavedAt = savedAt == default ? AppClock.Now : savedAt;
        StateVersion++;
        TouchScope("saved", markDirty: false);
    }

    public string ActiveVersionId
    {
        get => Data.ActiveVersionId ?? "";
        set => Data.ActiveVersionId = value ?? "";
    }

    public void MarkDirty() => MarkDirty("project_info");

    public void MarkDirty(params string[] scopes)
    {
        EnsureWritable("marcar cambios");
        IsDirty = true;
        StateVersion++;

        if (scopes == null || scopes.Length == 0)
        {
            TouchScope("project_info");
            PublishChanged("project_info", "Cambio de proyecto");
            return;
        }

        foreach (string rawScope in scopes.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            string normalized = TouchScope(rawScope);
            PublishChanged(normalized, normalized);
        }
    }

    public void NotifyChanged(string scope, string reason = "")
    {
        MarkDirty(string.IsNullOrWhiteSpace(scope) ? "project_info" : scope);
    }

    public void MarkSaved()
    {
        IsDirty = false;
        dirtyScopes.Clear();
        StateVersion++;
        TouchScope("saved", markDirty: false);
        var args = new ProjectSessionChangedEventArgs(ProjectSessionChangeKind.Saved, "saved", "Proyecto guardado", IsDirty);
        Saved?.Invoke(this, args);
        Changed?.Invoke(this, args);
    }

    public void ClearCubicacionCompositionLayers()
    {
        data.CubicacionRecursos ??= new List<CubicacionRecursoRow>();
        data.CubicacionZonaCantidades ??= new List<CubicacionZonaCantidadRow>();
        data.CubicacionValorizaciones ??= new List<CubicacionValorizacionRow>();
        data.PartidaApuVinculos ??= new List<PartidaApuVinculoRow>();
        data.CubicacionRecursos.Clear();
        data.CubicacionZonaCantidades.Clear();
        data.CubicacionValorizaciones.Clear();
        data.PartidaApuVinculos.Clear();
    }

    public void RemoveCubicacionRows(IReadOnlyCollection<string> rowIds)
    {
        EnsureWritable("eliminar recursos de cubicación");
        new CubicacionMutationService().RemoveRowsByIds(data, rowIds);
    }

    public void RemoveCubicacionForItemIds(IReadOnlyCollection<string> itemIds)
    {
        EnsureWritable("eliminar cubicación de partidas");
        new CubicacionMutationService().RemoveForItemIds(data, itemIds);
    }

    public void RemoveApusForItemIds(IReadOnlyCollection<string> removedItemIds)
    {
        if (removedItemIds == null || removedItemIds.Count == 0)
            return;

        data.Apus ??= new List<ApuRow>();
        data.Apus.RemoveAll(apu => removedItemIds.Any(id => ItemizadoHierarchyRules.MatchesRemovedId(apu.ItemId, id)));

        data.PartidaApuVinculos ??= new List<PartidaApuVinculoRow>();
        data.PartidaApuVinculos.RemoveAll(v => removedItemIds.Any(id => ItemizadoHierarchyRules.MatchesRemovedId(v.ItemId, id)));
    }

    public void ClearAllApus()
    {
        data.Apus ??= new List<ApuRow>();
        data.Apus.Clear();
    }

    public void NewProject(string userName, string userRole = "Usuario")
    {
        EnsureWritable("crear un nuevo proyecto");
        data = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow
            {
                Nombre = "Proyecto sin nombre",
                Cliente = "Cliente sin nombre",
                Ubicacion = "Sin ubicación",
                UsuarioNombre = string.IsNullOrWhiteSpace(userName) ? Environment.UserName : userName,
                UsuarioRol = string.IsNullOrWhiteSpace(userRole) ? "Usuario" : userRole,
                FechaCreacion = AppClock.Now,
                FechaModificacion = AppClock.Now,
                MonedaPresupuesto = "CLP",
                EstadoPresupuesto = "Borrador",
                GastosGeneralesPct = 14m,
                UtilidadPct = 18m,
                IvaPct = 19m,
                AutoActualizarIndicadores = true,
                OfertaPdf = new OfferPdfSettings()
            }
        };

        CurrentProjectPath = "";
        IsDirty = false;
        StateVersion++;
        scopeVersions.Clear();
        dirtyScopes.Clear();
        TouchScope("reset", markDirty: false);
        EnsureIntegrity(data);
        SyncCubicacionGgUtFactorsFromVariablesInternal();
        PublishChanged("reset", "Nuevo proyecto");
    }

    public void ApplySnapshot(ProjectFileData? snapshot, string? projectPath = null, bool markDirty = false)
    {
        data = snapshot ?? new ProjectFileData();
        EnsureIntegrity(data);
        if (projectPath != null)
            CurrentProjectPath = projectPath;
        IsDirty = markDirty;
        StateVersion++;
        scopeVersions.Clear();
        dirtyScopes.Clear();
        SyncCubicacionGgUtFactorsFromVariablesInternal();
        TouchScope(markDirty ? "project_info" : "saved", markDirty);
        PublishChanged(markDirty ? "project_info" : "saved", markDirty ? "Snapshot aplicado con cambios" : "Snapshot aplicado");
    }

    public decimal GetFamilyMarkup(string familyName)
    {
        var norm = NormalizeFamilyName(familyName);
        string key = $"MarkupFactor_{norm}";
        if (ProjectInfo.Variables.TryGetValue(key, out decimal val))
            return val;

        return norm switch
        {
            "CONDUCTORES" => 1.50m,
            "CANALIZACION" => 1.44m,
            "TABLEROS" => 1.10m,
            "PROTECCIONES" => 1.12m,
            "ILUMINACION" => 1.17m,
            "EQUIPOS" => 1.12m,
            "ENCHUFES Y TOMACORRIENTES" or "ENCHUFES" => 1.15m,
            "PUESTA A TIERRA" => 1.20m,
            _ => 1.14m
        };
    }

    public void SetFamilyMarkup(string familyName, decimal value)
    {
        EnsureWritable("modificar markup de familia");
        var norm = NormalizeFamilyName(familyName);
        string key = $"MarkupFactor_{norm}";
        ProjectInfo.Variables[key] = value;
        MarkDirty("project_info");
        SyncCubicacionGgUtFactorsFromVariables();
    }

    public decimal GetFamilyDiscountPct(string familyName)
    {
        var norm = NormalizeFamilyName(familyName);
        string key = $"MarkupDiscount_{norm}";
        return ProjectInfo.Variables.TryGetValue(key, out decimal val) ? MarkupPolicy.ClampPct(val) : 0m;
    }

    public void SetFamilyDiscountPct(string familyName, decimal pct)
    {
        EnsureWritable("modificar descuento de familia");
        var norm = NormalizeFamilyName(familyName);
        string key = $"MarkupDiscount_{norm}";
        pct = MarkupPolicy.ClampPct(pct);
        if (pct <= 0m)
            ProjectInfo.Variables.Remove(key);
        else
            ProjectInfo.Variables[key] = pct;
        MarkDirty("project_info");
        SyncCubicacionGgUtFactorsFromVariables();
    }

    /// <summary>Factor × efectivo en cubicación: markup × (1 − descuento%).</summary>
    public static decimal ComputePresentationFactor(decimal markup, decimal discountPct) =>
        ExcelCostosBridge.PresentationFactor(markup, discountPct);

    public decimal GetFamilyPresentationFactor(string familyName) =>
        ComputePresentationFactor(GetFamilyMarkup(familyName), GetFamilyDiscountPct(familyName));

    public void SyncCubicacionGgUtFactorsFromVariables()
    {
        EnsureWritable("sincronizar markups de cubicación");
        SyncCubicacionGgUtFactorsFromVariablesInternal();
        MarkDirty("cubicacion");
    }

    private void SyncCubicacionGgUtFactorsFromVariablesInternal()
    {
        foreach (var row in data.Cubicacion)
        {
            if (row.Tipo == TipoRecurso.MAT)
            {
                row.GgUtFactor = GetFamilyPresentationFactor(row.Categoria);
                row.RecalcularUf(data.Proyecto.ValorUfClp);
            }
            else
            {
                row.GgUtFactor = 1.0m;
                row.RecalcularUf(data.Proyecto.ValorUfClp);
            }
        }
    }

    private static string NormalizeFamilyName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "ACCESORIOS";
        return name.Trim().ToUpperInvariant()
            .Replace("Á", "A")
            .Replace("É", "E")
            .Replace("Í", "I")
            .Replace("Ó", "O")
            .Replace("Ú", "U");
    }

    public void SetReadOnly(bool isReadOnly, string? reason = null) =>
        SetLockReadOnly(isReadOnly, reason);

    public void SetLockReadOnly(bool isReadOnly, string? reason = null)
    {
        lockReadOnly = isReadOnly;
        lockReadOnlyReason = isReadOnly
            ? (string.IsNullOrWhiteSpace(reason) ? "Proyecto abierto en modo solo lectura." : reason.Trim())
            : "";
        PublishChanged("lock_access", isReadOnly ? "Lock de solo lectura activado" : "Lock liberado");
    }

    public void SetRoleReadOnly(bool isReadOnly, string? reason = null)
    {
        roleReadOnly = isReadOnly;
        roleReadOnlyReason = isReadOnly
            ? (string.IsNullOrWhiteSpace(reason) ? "Tu rol no permite editar." : reason.Trim())
            : "";
        PublishChanged("role_access", isReadOnly ? "Restricción por rol activada" : "Restricción por rol liberada");
    }

    public void EnsureWritable(string operation)
    {
        if (!IsReadOnly)
            return;

        throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(operation)
                ? "El proyecto est� en modo solo lectura."
                : $"El proyecto est� en modo solo lectura. No se puede {operation}.");
    }

    public ProjectFileData CreateMutableSnapshot()
    {
        EnsureIntegrity(data);
        data.Proyecto.FechaModificacion = AppClock.Now;
        return data;
    }

    public ProjectFileData CreateSnapshot()
    {
        EnsureIntegrity(data);
        data.Proyecto.FechaModificacion = AppClock.Now;
        // Contexto generado en compilación en vez de JsonSerializer reflexivo: mismo
        // deep-clone vía JSON, pero sin el costo de resolver metadata por reflexión en
        // cada Undo/Redo — crítico en proyectos grandes (miles de recursos). El otro costo
        // dominante (EnsureIntegrity con escaneos O(N²) en CubicacionCompositionService)
        // se arregló aparte con índices por RecursoId.
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, ProjectFileDataJsonContext.Default.ProjectFileData);
        var snapshot = JsonSerializer.Deserialize(bytes, ProjectFileDataJsonContext.Default.ProjectFileData) ?? new ProjectFileData();
        EnsureIntegrity(snapshot);
        return snapshot;
    }

    public void SyncCubicacionRowComposition(CubicacionRow row)
    {
        EnsureWritable("editar cubicación");
        new CubicacionCompositionService().SyncNormalizedFromLegacyRow(data, row);
    }

    /// <summary>
    /// Empuja MatClp/MoClp del catálogo de materiales a todas las filas de cubicación (y
    /// valorizaciones) con el mismo código. Independiente de si la pantalla Cubicación está
    /// abierta: el Banco de Precios y Cotizaciones lo llaman al cambiar un precio.
    /// </summary>
    public int SyncCubicacionPricesFromMateriales()
    {
        EnsureWritable("sincronizar precios de cubicación");
        return new CubicacionService().SyncPricesFromCatalog(
            Cubicacion,
            Materiales,
            ProjectInfo.ValorUfClp,
            CubicacionValorizaciones);
    }

    public void ApplyCubicacionMedicionesForPartida(string itemId)
    {
        EnsureWritable("editar mediciones de cubicación");
        var composition = new CubicacionCompositionService();
        composition.ApplyMedicionesToPartida(data, itemId, data.Mediciones);
        composition.SyncLegacyFromNormalized(data);
    }

    private string TouchScope(string scope, bool markDirty = true)
    {
        string normalized = NormalizeScope(scope);
        scopeVersions[normalized] = scopeVersions.TryGetValue(normalized, out int current) ? current + 1 : 1;
        if (markDirty && normalized != "saved" && normalized != "reset")
            dirtyScopes.Add(normalized);
        return normalized;
    }

    private void PublishChanged(string scope, string reason)
    {
        string normalizedScope = NormalizeScope(scope);
        var kind = MapKind(normalizedScope);
        var args = new ProjectSessionChangedEventArgs(kind, normalizedScope, reason, IsDirty);

        Changed?.Invoke(this, args);

        switch (kind)
        {
            case ProjectSessionChangeKind.ProjectInfo:
            case ProjectSessionChangeKind.Reset:
                ProjectChanged?.Invoke(this, args);
                break;
            case ProjectSessionChangeKind.Itemizado:
                ItemizadoChanged?.Invoke(this, args);
                break;
            case ProjectSessionChangeKind.Materiales:
                MaterialsChanged?.Invoke(this, args);
                break;
            case ProjectSessionChangeKind.Cubicacion:
            case ProjectSessionChangeKind.QtyHeaders:
            case ProjectSessionChangeKind.Zonas:
                CubicacionChanged?.Invoke(this, args);
                break;
            case ProjectSessionChangeKind.Apu:
                ApuChanged?.Invoke(this, args);
                break;
            case ProjectSessionChangeKind.Versiones:
                VersionsChanged?.Invoke(this, args);
                break;
        }
    }

    private static string NormalizeScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope)) return "project_info";
        return scope.Trim().ToLowerInvariant() switch
        {
            "plantillas" or "template" or "templates" => "plantillas",
            "conjuntos" or "conjuntos_electricos" => "conjuntos_electricos",
            "apu" or "apus" or "hoja_costo" => "apu",
            "ordenes" or "ordenes_cambio" or "change_orders" => "ordenes_cambio",
            "version" or "versions" or "versiones" => "versiones",
            "qty" or "qty_headers" or "cantidad_headers" => "qty_headers",
            "calc" or "calculadora" => "calculadora",
            "material" or "materiales" => "materiales",
            "item" or "itemizado" => "itemizado",
            "cubicacion" or "cubicación" => "cubicacion",
            "reset" => "reset",
            "saved" => "saved",
            "project" or "proyecto" or "project_info" => "project_info",
            var value => value
        };
    }

    private static ProjectSessionChangeKind MapKind(string scope) => scope switch
    {
        "project_info" => ProjectSessionChangeKind.ProjectInfo,
        "itemizado" => ProjectSessionChangeKind.Itemizado,
        "materiales" => ProjectSessionChangeKind.Materiales,
        "cubicacion" => ProjectSessionChangeKind.Cubicacion,
        "plantillas" => ProjectSessionChangeKind.Plantillas,
        "apu" => ProjectSessionChangeKind.Apu,
        "ordenes_cambio" => ProjectSessionChangeKind.OrdenesCambio,
        "versiones" => ProjectSessionChangeKind.Versiones,
        "qty_headers" => ProjectSessionChangeKind.QtyHeaders,
        "zonas" => ProjectSessionChangeKind.Zonas,
        "saved" => ProjectSessionChangeKind.Saved,
        "reset" => ProjectSessionChangeKind.Reset,
        _ => ProjectSessionChangeKind.Unknown
    };

    private static void EnsureIntegrity(ProjectFileData value)
    {
        ProjectSnapshotIntegrity.Ensure(value);
        EnsureProjectInfoIntegrity(value.Proyecto);
        new CubicacionCompositionService().EnsureProject(value);
    }

    private static void EnsureProjectInfoIntegrity(ProjectInfoRow project)
    {
        project.Nombre = string.IsNullOrWhiteSpace(project.Nombre) ? "Proyecto sin nombre" : project.Nombre;
        project.Cliente = string.IsNullOrWhiteSpace(project.Cliente) ? "Cliente sin nombre" : project.Cliente;
        project.Ubicacion = string.IsNullOrWhiteSpace(project.Ubicacion) ? "Sin ubicación" : project.Ubicacion;
        project.UsuarioNombre = string.IsNullOrWhiteSpace(project.UsuarioNombre) ? Environment.UserName : project.UsuarioNombre;
        project.UsuarioRol = string.IsNullOrWhiteSpace(project.UsuarioRol) ? "Usuario" : project.UsuarioRol;
        project.MonedaPresupuesto = string.IsNullOrWhiteSpace(project.MonedaPresupuesto) ? "CLP" : project.MonedaPresupuesto;
        project.EstadoPresupuesto = string.IsNullOrWhiteSpace(project.EstadoPresupuesto) ? "Borrador" : project.EstadoPresupuesto;
        project.FechaCreacion = project.FechaCreacion == default ? AppClock.Now : project.FechaCreacion;
        project.FechaModificacion = project.FechaModificacion == default ? AppClock.Now : project.FechaModificacion;
        project.GastosGeneralesPct = project.GastosGeneralesPct < 0 ? 0m : project.GastosGeneralesPct;
        project.UtilidadPct = project.UtilidadPct < 0 ? 0m : project.UtilidadPct;
        project.IvaPct = project.IvaPct < 0 ? 0m : project.IvaPct;
        project.HistorialEstados ??= new List<BudgetStatusHistoryRow>();
        project.Variables ??= new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        project.OfertaPdf ??= new OfferPdfSettings();
        project.CubicacionCellComments ??= new List<CubicacionCellCommentRow>();
        project.TechnicalValidationRules ??= new List<TechnicalValidationRuleRow>();
        project.RealCosts ??= new List<RealCostRow>();
        project.PartidaWorkflow ??= new List<PartidaWorkflowRow>();
        project.PartidaTimeline ??= new List<PartidaChangeTimelineRow>();
        project.AuditLog ??= new List<ProjectAuditEntryRow>();
        project.ProactiveAlertRules ??= new List<ProactiveAlertRuleRow>();
        project.LaborDaySettings ??= new LaborDaySettingsRow();
        project.TipoProyecto = string.IsNullOrWhiteSpace(project.TipoProyecto) ? "Edificio habitacional" : project.TipoProyecto;
        project.PriceExpiryWarningDays = project.PriceExpiryWarningDays < 0 ? 150 : project.PriceExpiryWarningDays;
        project.PriceExpiryCriticalDays = project.PriceExpiryCriticalDays < 0 ? 180 : project.PriceExpiryCriticalDays;
    }
}
