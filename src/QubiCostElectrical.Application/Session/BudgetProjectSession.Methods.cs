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
        return dirtyTracker.GetScopeVersion(normalized);
    }

    public IReadOnlyCollection<string> GetDirtyScopes() => dirtyTracker.GetDirtyScopes();

    public bool HasDirtyScope(string scope)
    {
        string normalized = NormalizeScope(scope);
        return dirtyTracker.HasDirtyScope(normalized);
    }

    public void ClearDirtyScopes(IEnumerable<string>? scopes = null)
    {
        if (scopes == null)
        {
            dirtyTracker.ClearDirtyScopes();
            return;
        }

        dirtyTracker.ClearDirtyScopes(scopes.Select(NormalizeScope));
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
    public List<EstadoPagoMesRow> EstadoPagoMeses => Data.EstadoPagoMeses ??= new List<EstadoPagoMesRow>();
    public EstadoPagoConfig EstadoPagoConfig => Data.EstadoPagoConfig ??= EstadoPagoConfig.Default();
    public TipologiasEdificioData Tipologias => Data.Tipologias ??= TipologiasEdificioData.Default();
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

    public void MarkDirty() => MarkDirty(ProjectDirtyScope.ProjectInfo);

    public void MarkDirty(params ProjectDirtyScope[] scopes)
    {
        if (scopes == null || scopes.Length == 0)
        {
            MarkDirty(ProjectDirtyScope.ProjectInfo.Key);
            return;
        }

        MarkDirty(scopes.Select(s => s.Key).ToArray());
    }

    public void MarkDirty(params string[] scopes)
    {
        EnsureWritable("marcar cambios");
        dirtyTracker.MarkDirtyFlag();
        StateVersion++;

        if (scopes == null || scopes.Length == 0)
        {
            TouchScope(ProjectDirtyScope.ProjectInfo.Key);
            PublishChanged(ProjectDirtyScope.ProjectInfo.Key, "Cambio de proyecto");
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

    /// <summary>
    /// Anuncia que se va a persistir el proyecto. Debe invocarse ANTES de crear el snapshot:
    /// los suscriptores (p.ej. la auditoría) escriben sobre <c>ProjectInfo</c> que aún no se ha serializado.
    /// </summary>
    public void NotifySaving()
    {
        TouchScope("saving", markDirty: false);
        var args = new ProjectSessionChangedEventArgs(ProjectSessionChangeKind.Saving, "saving", "Proyecto por persistir", IsDirty);
        Saving?.Invoke(this, args);
    }

    public void MarkSaved()
    {
        dirtyTracker.MarkSaved();
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

    public void NewProject(
        string userName,
        string userRole = "Usuario",
        decimal? defaultGastosGeneralesPct = null,
        decimal? defaultUtilidadPct = null,
        decimal? defaultMermasPct = null,
        decimal? defaultHerramientasPct = null,
        string? defaultMoneda = null)
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
                MonedaPresupuesto = string.IsNullOrWhiteSpace(defaultMoneda) ? "CLP" : defaultMoneda.Trim().ToUpperInvariant(),
                EstadoPresupuesto = "Borrador",
                MarkupMethod = "SobreCosto",
                ValorAgregadoEquiposPct = 3m,
                PoliticaCostosExcel = true,
                GastosGeneralesPct = defaultGastosGeneralesPct.HasValue && defaultGastosGeneralesPct.Value >= 0m ? defaultGastosGeneralesPct.Value : 14m,
                UtilidadPct = defaultUtilidadPct.HasValue && defaultUtilidadPct.Value >= 0m ? defaultUtilidadPct.Value : 18m,
                MermasPct = defaultMermasPct.HasValue && defaultMermasPct.Value >= 0m ? defaultMermasPct.Value : 0m,
                HerramientasPct = defaultHerramientasPct.HasValue && defaultHerramientasPct.Value >= 0m ? defaultHerramientasPct.Value : 0m,
                FerreteriaActiva = true,
                FerreteriaPct = 5m,
                IvaPct = 19m,
                AutoActualizarIndicadores = true,
                // Estandar del proyecto: las familias nacen sin configurar y con factor 1,00
                // (precio igual a costo) hasta que el usuario cargue una configuracion guardada.
                MarkupFactorPorDefecto = 1.00m,
                UsarFactoresPresentacionFamilia = true,
                OfertaPdf = new OfferPdfSettings()
            }
        };

        CurrentProjectPath = "";
        dirtyTracker.ResetVersionsAndDirty();
        StateVersion++;
        TouchScope("reset", markDirty: false);
        EnsureIntegrity(data);
        SyncCubicacionGgUtFactorsFromVariablesInternal();
        PublishChanged("reset", "Nuevo proyecto");
    }

    public void ApplyCommercialMargins(decimal gastosGeneralesPct, decimal utilidadPct, decimal? mermasPct = null, decimal? herramientasPct = null)
    {
        EnsureWritable("actualizar márgenes comerciales");
        data.Proyecto.GastosGeneralesPct = Math.Max(0m, gastosGeneralesPct);
        data.Proyecto.UtilidadPct = Math.Max(0m, utilidadPct);
        if (mermasPct.HasValue && mermasPct.Value >= 0m) data.Proyecto.MermasPct = mermasPct.Value;
        if (herramientasPct.HasValue && herramientasPct.Value >= 0m) data.Proyecto.HerramientasPct = herramientasPct.Value;
        MarkDirty(ProjectDirtyScope.ProjectInfo);
        SyncCubicacionGgUtFactorsFromVariablesInternal();
        PublishChanged("margins", "Actualización de márgenes comerciales");
    }

    public void InitializeProjectParameters(
        string name,
        string client,
        string location,
        string currency,
        ClosingPolicySelection closingPolicy,
        IEnumerable<ItemizadoRow>? templateRows = null)
    {
        EnsureWritable("inicializar parámetros del proyecto");
        ProjectInfo.Nombre = name;
        ProjectInfo.Cliente = client;
        ProjectInfo.Ubicacion = location;
        ProjectInfo.MonedaPresupuesto = currency;
        Data.ClosingPolicy = closingPolicy;
        MarkDirty(ProjectDirtyScope.ClosingPolicy);
        if (templateRows != null)
        {
            data.Itemizado.Clear();
            data.Itemizado.AddRange(templateRows);
            MarkDirty(ProjectDirtyScope.Itemizado);
        }
        PublishChanged("project_info", "Inicialización de proyecto");
    }

    public void ApplySnapshot(ProjectFileData? snapshot, string? projectPath = null, bool markDirty = false)
    {
        data = snapshot ?? new ProjectFileData();
        EnsureIntegrity(data);
        if (projectPath != null)
            CurrentProjectPath = projectPath;
        dirtyTracker.ResetVersionsAndDirty();
        if (markDirty)
            dirtyTracker.MarkDirtyFlag();
        StateVersion++;
        SyncCubicacionGgUtFactorsFromVariablesInternal();
        TouchScope(markDirty ? "project_info" : "saved", markDirty);
        PublishChanged(markDirty ? "project_info" : "saved", markDirty ? "Snapshot aplicado con cambios" : "Snapshot aplicado");
    }

    public decimal GetFamilyMarkup(string familyName)
    {
        var norm = FamilyPresentationDefaults.FollowAssignment(ProjectInfo.FamiliasPrecioAsignadas, familyName);
        string key = $"MarkupFactor_{norm}";
        if (ProjectInfo.Variables.TryGetValue(key, out decimal val))
            return val;

        // Regla única en Domain (FamilyPresentationDefaults) para no duplicar la tabla.
        return FamilyPresentationDefaults.DefaultMarkup(norm, ProjectInfo.MarkupFactorPorDefecto);
    }

    public void SetFamilyMarkup(string familyName, decimal value, bool syncTakeoff = true)
    {
        EnsureWritable("modificar markup de familia");
        var norm = NormalizeFamilyName(familyName);
        string key = $"MarkupFactor_{norm}";
        ProjectInfo.Variables[key] = value;
        MarkDirty(ProjectDirtyScope.ProjectInfo);
        if (syncTakeoff)
            SyncCubicacionGgUtFactorsFromVariables();
    }

    public decimal GetFamilyDiscountPct(string familyName)
    {
        var norm = FamilyPresentationDefaults.FollowAssignment(ProjectInfo.FamiliasPrecioAsignadas, familyName);
        string key = $"MarkupDiscount_{norm}";
        return ProjectInfo.Variables.TryGetValue(key, out decimal val) ? MarkupPolicy.ClampPct(val) : 0m;
    }

    public void SetFamilyDiscountPct(string familyName, decimal pct, bool syncTakeoff = true)
    {
        EnsureWritable("modificar descuento de familia");
        var norm = NormalizeFamilyName(familyName);
        string key = $"MarkupDiscount_{norm}";
        pct = MarkupPolicy.ClampPct(pct);
        if (pct <= 0m)
            ProjectInfo.Variables.Remove(key);
        else
            ProjectInfo.Variables[key] = pct;
        MarkDirty(ProjectDirtyScope.ProjectInfo);
        if (syncTakeoff)
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
        MarkDirty(ProjectDirtyScope.Cubicacion);
    }

    private void SyncCubicacionGgUtFactorsFromVariablesInternal()
    {
        // C-1: toda línea (MAT y no-MAT) lleva el factor de presentación de su familia cuando el
        // proyecto usa factores; si no, 1.0 como antes.
        bool usaFactores = ProjectInfo.UsarFactoresPresentacionFamilia || ProjectInfo.PoliticaCostosExcel;
        foreach (var row in data.Cubicacion)
        {
            row.GgUtFactor = usaFactores ? GetFamilyPresentationFactor(row.Categoria) : 1.0m;
            row.RecalcularUf(data.Proyecto.ValorUfClp);
        }
    }

    private static string NormalizeFamilyName(string? name) => FamilyPresentationDefaults.Normalize(name);

    public void SetLockReadOnly(bool isReadOnly, string? reason = null)
    {
        accessGate.SetLockReadOnly(isReadOnly, reason);
        PublishChanged("lock_access", isReadOnly ? "Lock de solo lectura activado" : "Lock liberado");
    }

    public void SetRoleReadOnly(bool isReadOnly, string? reason = null)
    {
        accessGate.SetRoleReadOnly(isReadOnly, reason);
        PublishChanged("role_access", isReadOnly ? "Restricción por rol activada" : "Restricción por rol liberada");
    }

    public void EnsureWritable(string operation) => accessGate.EnsureWritable(operation);

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
            CubicacionValorizaciones,
            ProjectInfo);
    }

    /// <summary>
    /// Fija el índice FA del proyecto y propaga la M.O. recalibrada al catálogo de materiales, al
    /// snapshot del banco de precios y a las filas de cubicación. Es el equivalente en la app a
    /// reescribir COSTOS!E192 en la planilla original: las 7.210 líneas del DETALLE se movían con él.
    /// Devuelve qué cambió para que la pantalla que lo pidió refresque solo esas filas.
    /// </summary>
    public IndiceMoFaAplicado AplicarIndiceMoFa(decimal indiceMoFa, IPriceBankStore? bancoPrecios = null)
    {
        EnsureWritable("calibrar el índice de mano de obra");
        decimal anterior = MaterialMoResolver.IndiceFa(ProjectInfo);
        // 0 = "sin calibrar": es un valor válido que deja la M.O. por % en Material × % (factor 1)
        // hasta que el plan de obra permita derivar el índice. Solo los negativos son inválidos.
        if (indiceMoFa < 0m)
            return new IndiceMoFaAplicado(anterior, anterior, Array.Empty<MaterialMoRecalibrado>(), 0);

        ProjectInfo.IndiceMoFa = indiceMoFa;

        var cambios = MaterialMoRecalibrationService.Recalibrar(Materiales, ProjectInfo);
        MarkDirty(ProjectDirtyScope.ProjectInfo);

        if (cambios.Count > 0)
        {
            MaterialMoRecalibrationService.SincronizarBanco(cambios, bancoPrecios, ProjectInfo.ValorUfClp);
            MarkDirty(ProjectDirtyScope.Materiales);
        }

        int sincronizadas = SyncCubicacionPricesFromMateriales();
        if (sincronizadas > 0)
            MarkDirty(ProjectDirtyScope.Cubicacion);

        return new IndiceMoFaAplicado(anterior, indiceMoFa, cambios, sincronizadas);
    }

    public bool ApplyCubicacionMedicionesForPartida(string itemId)
    {
        EnsureWritable("editar mediciones de cubicación");
        return new CubicacionCompositionService().ApplyMedicionesToPartida(data, itemId, data.Mediciones);
    }

    private string TouchScope(string scope, bool markDirty = true)
    {
        string normalized = NormalizeScope(scope);
        return dirtyTracker.Touch(normalized, markDirty);
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

    private static string NormalizeScope(string? scope) => ProjectDirtyScope.Parse(scope).Key;

    private static ProjectSessionChangeKind MapKind(string scope) => ProjectDirtyScope.Parse(scope).ChangeKind;

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
