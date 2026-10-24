using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Threading;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Costo;

/// <summary>
/// Coordina los coeficientes de sobreventa por familia, presets de factores y presentación de márgenes.
/// </summary>
internal sealed class CostoCoeficientesCoordinator
{
    private readonly CostoViewModel _vm;

    public CostoCoeficientesCoordinator(CostoViewModel vm)
    {
        _vm = vm;
    }

    /// <summary>Filas que la pantalla muestra siempre, aunque el proyecto no las use.</summary>
    private static readonly string[] FamiliasBase =
    {
        "Conductores",
        "Equipos",
        "Tableros",
        "Iluminación",
        "Canalización",
        "Accesorios",
        "Enchufes y tomacorrientes",
        "Movimientos",
        "Comisionamiento"
    };

    /// <summary>
    /// Arma la lista de coeficientes con las filas del proyecto: las canónicas de siempre más
    /// las que el proyecto usa o configura (por ejemplo Puesta a tierra o Protecciones, que antes
    /// no tenían fila y quedaban con el factor por defecto sin que nadie lo notara).
    /// </summary>
    public void SyncCoefFactors()
    {
        var informe = PresentacionFamiliasInforme.Construir(_vm.Session.ProjectInfo, _vm.Session.Cubicacion);
        _vm.InformeFamilias = informe;

        var deseados = informe.Buckets
            .Where(b => EsFilaVisible(b, _vm.Session.ProjectInfo))
            .ToList();

        foreach (var row in _vm.CoefFactors.ToList())
        {
            if (!deseados.Any(d => string.Equals(d.Clave, FamilyPresentationDefaults.Normalize(row.FamilyKey), StringComparison.OrdinalIgnoreCase)))
                _vm.CoefFactors.Remove(row);
        }

        foreach (var d in deseados)
        {
            var row = _vm.CoefFactors.FirstOrDefault(f =>
                string.Equals(FamilyPresentationDefaults.Normalize(f.FamilyKey), d.Clave, StringComparison.OrdinalIgnoreCase));
            if (row == null)
            {
                row = new CoefFactorVm(d.Nombre, d.Etiqueta, d.Nombre, d.Markup, CommitFamilyMarkup, CommitFamilyDiscount);
                _vm.CoefFactors.Add(row);
            }
            else
            {
                row.SetValueSilent(d.Markup);
                row.SetDiscountSilent(d.DescuentoPct);
            }

            var sinConfigurar = d.Familias.Count(f => !f.Configurada);
            row.SetEstado(d.Origen, d.FactorAplicado, sinConfigurar, d.Familias.Count);
        }

        var orden = deseados.Select(d => d.Clave).ToList();
        var actuales = _vm.CoefFactors
            .Select(f => FamilyPresentationDefaults.Normalize(f.FamilyKey))
            .ToList();
        if (!orden.SequenceEqual(actuales, StringComparer.OrdinalIgnoreCase))
        {
            var ordenados = orden
                .Select(clave => _vm.CoefFactors.First(f =>
                    string.Equals(FamilyPresentationDefaults.Normalize(f.FamilyKey), clave, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            _vm.CoefFactors.Clear();
            foreach (var r in ordenados)
                _vm.CoefFactors.Add(r);
        }
    }

    private static bool EsFilaVisible(BucketPresentacionInfo bucket, ProjectInfoRow? project)
    {
        if (bucket.Familias.Count > 0)
            return true;
        if (bucket.Configurada)
            return true;
        if (project?.FamiliasPersonalizadas?.Any(p =>
            string.Equals(FamilyPresentationDefaults.Normalize(p), bucket.Clave, StringComparison.OrdinalIgnoreCase)) == true)
            return true;
        return FamiliasBase.Any(f =>
            string.Equals(FamilyPresentationDefaults.Normalize(f), bucket.Clave, StringComparison.OrdinalIgnoreCase));
    }

    public void RefreshCoefEffectiveLabels()
    {
        foreach (var f in _vm.CoefFactors)
        {
            var m = _vm.Session.GetFamilyMarkup(f.FamilyKey);
            var d = _vm.Session.GetFamilyDiscountPct(f.FamilyKey);
            f.SetEffectiveSilent(m, d);
        }
    }

    public void UpdateCostosPresetHint(ProjectInfoRow project)
    {
        var preset = project.CostosPreset;
        if (preset is not { Factores.Count: > 0 })
        {
            _vm.CostosPresetHint = "Sin preset guardado en este proyecto";
            _vm.NotifyCostosPresetStateChanged();
            return;
        }

        var when = preset.GuardadoEn > DateTime.MinValue
            ? preset.GuardadoEn.ToString("d", CultureInfo.CurrentCulture)
            : "—";
        _vm.CostosPresetHint = $"{preset.Etiqueta} · guardado {when}";
        _vm.NotifyCostosPresetStateChanged();
    }

    public void RefreshCoeficientesSection(ProjectInfoRow project)
    {
        SyncCoefFactors();
        foreach (var f in _vm.CoefFactors)
        {
            f.SetValueSilent(_vm.Session.GetFamilyMarkup(f.FamilyKey));
            f.SetDiscountSilent(_vm.Session.GetFamilyDiscountPct(f.FamilyKey));
        }
        RefreshCoefEffectiveLabels();
        UpdateCostosPresetHint(project);

        _vm.SetGastosGeneralesSilent(project.GastosGeneralesPct);
        _vm.SetUtilidadSilent(project.UtilidadPct);
        _vm.SetValorAgregadoEquiposSilent(project.ValorAgregadoEquiposPct);
        _vm.SetFerreteriaSilent(project.FerreteriaActiva, project.FerreteriaPct);

        _vm.UtilidadPolicyLabel = string.Equals(project.MarkupMethod, "SobreVenta", StringComparison.OrdinalIgnoreCase)
            && !project.PoliticaCostosExcel
            ? "Margen s/ venta"
            : project.PoliticaCostosExcel
                ? "Utilidades (aditiva)"
                : "Utilidades";

        _vm.SetPoliticaCostosExcelSilent(project.PoliticaCostosExcel);
        _vm.SetUsarFactoresPresentacionFamiliaSilent(project.UsarFactoresPresentacionFamilia);

        _vm.MoInstalacion.Load(project);

        _vm.UpdateCoefChips();
        _vm.RebuildPrecioFamiliaChips();
    }

    public void CommitFamilyMarkup(string familyKey, decimal value)
    {
        if (!_vm.CanEdit || _vm.Session.GetFamilyMarkup(familyKey) == value)
            return;
        _vm.SuppressCoefRefresh = true;
        try
        {
            _vm.Session.SetFamilyMarkup(familyKey, value, syncTakeoff: false);
            RefreshCoefEstados();
            RefreshCoefEffectiveLabels();
            _vm.UpdateCoefChips();
            _vm.Session.SyncCubicacionGgUtFactorsFromVariables();
            _vm.RefreshCostOnly();
        }
        finally
        {
            _vm.SuppressCoefRefresh = false;
        }
    }

    public void CommitFamilyDiscount(string familyKey, decimal pct)
    {
        if (!_vm.CanEdit || _vm.Session.GetFamilyDiscountPct(familyKey) == pct)
            return;
        _vm.SuppressCoefRefresh = true;
        try
        {
            _vm.Session.SetFamilyDiscountPct(familyKey, pct, syncTakeoff: false);
            RefreshCoefEstados();
            RefreshCoefEffectiveLabels();
            _vm.UpdateCoefChips();
            _vm.Session.SyncCubicacionGgUtFactorsFromVariables();
            _vm.RefreshCostOnly();
        }
        finally
        {
            _vm.SuppressCoefRefresh = false;
        }
    }

    /// <summary>Recalcula el origen del factor de cada fila sin tocar el valor que se esta editando.</summary>
    public void RefreshCoefEstados()
    {
        var informe = PresentacionFamiliasInforme.Construir(_vm.Session.ProjectInfo, _vm.Session.Cubicacion);
        _vm.InformeFamilias = informe;
        foreach (var row in _vm.CoefFactors)
        {
            var clave = FamilyPresentationDefaults.Normalize(row.FamilyKey);
            var bucket = informe.Buckets.FirstOrDefault(b =>
                string.Equals(b.Clave, clave, StringComparison.OrdinalIgnoreCase));
            if (bucket == null)
                continue;
            row.SetEstado(bucket.Origen, bucket.FactorAplicado, bucket.Familias.Count(f => !f.Configurada), bucket.Familias.Count);
        }
    }

    public void SaveCostosPreset()
    {
        if (!_vm.CanEdit)
            return;
        var project = _vm.Session.ProjectInfo;
        var preset = new CostosCoeficientesPresetRow
        {
            Etiqueta = string.IsNullOrWhiteSpace(project.Nombre) ? "Preset del proyecto" : $"Preset · {project.Nombre}",
            GuardadoEn = AppClock.Now,
            GastosGeneralesPct = project.GastosGeneralesPct,
            UtilidadPct = project.UtilidadPct,
            ValorAgregadoEquiposPct = project.ValorAgregadoEquiposPct,
            PoliticaCostosExcel = project.PoliticaCostosExcel,
        };
        foreach (var f in _vm.CoefFactors)
        {
            preset.Factores[f.FamilyKey] = _vm.Session.GetFamilyMarkup(f.FamilyKey);
            var d = _vm.Session.GetFamilyDiscountPct(f.FamilyKey);
            if (d > 0m)
                preset.DescuentosFamiliaPct[f.FamilyKey] = d;
        }

        project.CostosPreset = preset;
        _vm.Session.MarkDirty(ProjectDirtyScope.ProjectInfo);
        UpdateCostosPresetHint(project);
    }

    public void RestoreCostosPreset()
    {
        if (!_vm.CanEdit)
            return;
        var project = _vm.Session.ProjectInfo;
        var preset = project.CostosPreset;
        if (preset is not { Factores.Count: > 0 })
            return;

        if (!_vm.ConfirmAction("Restaurar preset",
            $"¿Desea restaurar los factores y márgenes guardados ({preset.Etiqueta})?\n\nEsta acción sobrescribirá los valores actuales de las familias."))
            return;

        _vm.Undo?.PushUndoPoint(_vm.Session, $"Restaurar preset de costos «{preset.Etiqueta}»");
        _vm.SuppressCoefRefresh = true;
        try
        {
            foreach (var f in _vm.CoefFactors)
            {
                var markup = preset.Factores.TryGetValue(f.FamilyKey, out var m) ? m : 1m;
                _vm.Session.SetFamilyMarkup(f.FamilyKey, markup);
                var dsct = preset.DescuentosFamiliaPct.TryGetValue(f.FamilyKey, out var d) ? d : 0m;
                _vm.Session.SetFamilyDiscountPct(f.FamilyKey, dsct);
                f.SetValueSilent(markup);
                f.SetDiscountSilent(dsct);
            }

            project.GastosGeneralesPct = preset.GastosGeneralesPct;
            project.UtilidadPct = preset.UtilidadPct;
            project.ValorAgregadoEquiposPct = preset.ValorAgregadoEquiposPct;
            project.PoliticaCostosExcel = preset.PoliticaCostosExcel;
            project.MarkupMethod = preset.PoliticaCostosExcel ? "SobreCosto" : project.MarkupMethod;
            _vm.Session.MarkDirty(ProjectDirtyScope.ProjectInfo);

            _vm.SetGastosGeneralesSilent(preset.GastosGeneralesPct);
            _vm.SetUtilidadSilent(preset.UtilidadPct);
            _vm.SetValorAgregadoEquiposSilent(preset.ValorAgregadoEquiposPct);
            _vm.SetPoliticaCostosExcelSilent(preset.PoliticaCostosExcel);
            _vm.UtilidadPolicyLabel = preset.PoliticaCostosExcel ? "Utilidades (aditiva)" : "Utilidades";

            RefreshCoefEffectiveLabels();
            _vm.UpdateCoefChips();
            _vm.RefreshCostOnly();
        }
        finally
        {
            _vm.SuppressCoefRefresh = false;
        }
    }

    /// <summary>
    /// Deja los factores efectivos de hoy como valores propios del proyecto: los numeros no
    /// cambian, pero el proyecto queda explicito y sus factores se pueden copiar a otro.
    /// </summary>
    public void FreezeCurrentFactors()
    {
        if (!_vm.CanEdit)
            return;

        var informe = PresentacionFamiliasInforme.Construir(_vm.Session.ProjectInfo, _vm.Session.Cubicacion);
        var variables = _vm.Session.ProjectInfo.Variables;
        var plan = new List<(string Familia, decimal Markup)>();
        var omitidas = new List<string>();

        foreach (var bucket in informe.Buckets)
        {
            if (bucket.Familias.Count == 0)
                continue;
            if (variables.ContainsKey($"MarkupFactor_{bucket.Clave}"))
                continue;

            var factores = bucket.Familias.Select(f => f.FactorEfectivo).Distinct().ToList();
            if (factores.Count > 1)
            {
                omitidas.Add(bucket.Nombre);
                continue;
            }

            var factor = factores[0];
            var descuento = bucket.DescuentoPct;
            var markup = descuento > 0m ? factor / (1m - descuento / 100m) : factor;
            plan.Add((bucket.Nombre, Math.Round(markup, 2, MidpointRounding.AwayFromZero)));
        }

        if (plan.Count == 0)
        {
            _vm.ConfirmAction("Fijar factores actuales",
                "No hay familias sin configurar en este proyecto: todos los factores ya son valores propios.");
            return;
        }

        var resumen = string.Join("\n", plan.Select(p => $"· {p.Familia}: {p.Markup:0.00}×"));
        var extra = omitidas.Count > 0
            ? $"\n\nSe dejan sin fijar porque tienen familias con factores distintos: {string.Join(", ", omitidas)}."
            : string.Empty;
        if (!_vm.ConfirmAction("Fijar factores actuales",
            $"Se guardaran como valores propios del proyecto. Los numeros no cambian.\n\n{resumen}{extra}\n\n¿Continuar?"))
            return;

        _vm.Undo?.PushUndoPoint(_vm.Session, "Fijar factores actuales");
        _vm.SuppressCoefRefresh = true;
        try
        {
            foreach (var p in plan)
                _vm.Session.SetFamilyMarkup(p.Familia, p.Markup);
            SyncCoefFactors();
            _vm.Session.SyncCubicacionGgUtFactorsFromVariables();
            _vm.RefreshCostOnly();
        }
        finally
        {
            _vm.SuppressCoefRefresh = false;
        }
    }

    /// <summary>Copia los factores, descuentos y asignaciones de familias de otro presupuesto.</summary>
    public void LoadConfigFromOtherProject()
    {
        if (!_vm.CanEdit)
            return;

        var recientes = new RecentProjectsService(WpfAppHost.Current?.UserPreferencesStore)
            .GetProjects()
            .Where(p => !p.IsMissing)
            .ToList();
        if (recientes.Count == 0)
        {
            _vm.ConfirmAction("Cargar configuración",
                "No hay presupuestos recientes disponibles para cargar.");
            return;
        }

        var owner = System.Windows.Application.Current?.MainWindow;
        var dialog = new CargarConfiguracionDialog(recientes);
        if (owner != null)
            dialog.Owner = owner;
        dialog.ShowDialog();
        if (!dialog.Accepted || string.IsNullOrWhiteSpace(dialog.SelectedPath))
            return;

        ProjectFileData? origen;
        try
        {
            origen = SQLiteProjectStore.LoadSnapshot(dialog.SelectedPath);
        }
        catch (Exception ex)
        {
            _vm.ConfirmAction("Cargar configuración", $"No se pudo leer el presupuesto:\n{ex.Message}");
            return;
        }

        var proyectoOrigen = origen.Proyecto;
        var factores = (proyectoOrigen?.Variables ?? new Dictionary<string, decimal>())
            .Where(k => k.Key.StartsWith("MarkupFactor_", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (factores.Count == 0)
        {
            _vm.ConfirmAction("Cargar configuración",
                "Ese presupuesto no tiene factores de presentación configurados: no hay nada que copiar.");
            return;
        }

        var descuentos = (proyectoOrigen?.Variables ?? new Dictionary<string, decimal>())
            .Where(k => k.Key.StartsWith("MarkupDiscount_", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var asignadas = proyectoOrigen?.FamiliasPrecioAsignadas ?? new Dictionary<string, string>();

        var resumen = string.Join("\n", factores
            .OrderBy(f => f.Key)
            .Select(f => $"· {f.Key["MarkupFactor_".Length..]}: {f.Value:0.00}×"));
        var extra = asignadas.Count > 0
            ? $"\n\nAsignaciones de familias: {string.Join(", ", asignadas.Select(a => $"{a.Key} → {a.Value}"))}."
            : string.Empty;
        if (!_vm.ConfirmAction("Cargar configuración",
            $"Se copiará desde «{System.IO.Path.GetFileNameWithoutExtension(dialog.SelectedPath)}»:\n\n{resumen}{extra}\n\n¿Continuar?"))
            return;

        _vm.Undo?.PushUndoPoint(_vm.Session, "Cargar configuración de otro presupuesto");
        _vm.SuppressCoefRefresh = true;
        try
        {
            foreach (var f in factores)
                _vm.Session.SetFamilyMarkup(f.Key["MarkupFactor_".Length..], f.Value);
            foreach (var d in descuentos)
                _vm.Session.SetFamilyDiscountPct(d.Key["MarkupDiscount_".Length..], d.Value);

            _vm.Session.ProjectInfo.FamiliasPrecioAsignadas = new Dictionary<string, string>(asignadas, StringComparer.OrdinalIgnoreCase);
            if (proyectoOrigen != null && proyectoOrigen.MarkupFactorPorDefecto > 0m)
                _vm.Session.ProjectInfo.MarkupFactorPorDefecto = proyectoOrigen.MarkupFactorPorDefecto;
            _vm.Session.MarkDirty(ProjectDirtyScope.ProjectInfo);

            SyncCoefFactors();
            _vm.Session.SyncCubicacionGgUtFactorsFromVariables();
            _vm.RefreshCostOnly();
        }
        finally
        {
            _vm.SuppressCoefRefresh = false;
        }
    }

    public void AttachCoefPresentation()
    {
        decimal costo = 0m;
        decimal venta = 0m;
        foreach (var factor in _vm.CoefFactors)
        {
            decimal costoUf = 0m;
            double share = 0d;
            foreach (var familia in _vm.Familias)
            {
                if (!FamiliaCaeEnFactor(factor, familia.Familia))
                    continue;
                costoUf += familia.TotalUf;
                share += familia.Share;
            }

            var markup = ParseCoef(factor.ValueText, 1m);
            var discount = ParseCoef(factor.DiscountText, 0m);
            var ventaUf = costoUf * ExcelCostosBridge.PresentationFactor(markup, discount);
            factor.SetPresentation(
                costoUf,
                ventaUf,
                share,
                costoUf > 0m ? _vm.FormatMoneyPrimary(PriceConversion.UfToClp(costoUf, _vm.CurrentValorUf)) : null,
                costoUf > 0m ? _vm.FormatMoneyPrimary(PriceConversion.UfToClp(ventaUf, _vm.CurrentValorUf)) : null);
            costo += costoUf;
            venta += ventaUf;
        }

        var costoClp = PriceConversion.UfToClp(costo, _vm.CurrentValorUf);
        var ventaClp = PriceConversion.UfToClp(venta, _vm.CurrentValorUf);
        _vm.FamCostoLabel = costo > 0m ? _vm.FormatMoneyPrimary(costoClp) : "—";
        _vm.FamVentaLabel = venta > 0m ? _vm.FormatMoneyPrimary(ventaClp) : "—";
        var delta = _vm.FormatMoneyPair(ventaClp - costoClp);
        var deltaBase = ventaClp - costoClp >= 0m ? $"+{delta.Primary}" : delta.Primary;
        _vm.FamDeltaLabel = costo > 0m
            ? (delta.Secondary.Length == 0 ? deltaBase : $"{deltaBase} ({delta.Secondary})")
            : "—";
    }

    /// <summary>
    /// El monto de la fila es la suma de las familias que se ven en ella.
    /// Canalizado y Bandejas no se llaman «Canalización», pero están en PVC/EPC.
    /// </summary>
    private static bool FamiliaCaeEnFactor(CoefFactorVm factor, string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return false;
        if (string.Equals(nombre, factor.FamilyKey, StringComparison.OrdinalIgnoreCase)
            || string.Equals(nombre, factor.Name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(nombre, factor.Map, StringComparison.OrdinalIgnoreCase))
            return true;

        var clave = ElectricalFamilyCatalog.ClaveCanonica(nombre);
        var normalizada = FamilyPresentationDefaults.Normalize(nombre);
        foreach (var chip in factor.FamiliasPrecio)
        {
            if (string.Equals(chip.Nombre, nombre, StringComparison.OrdinalIgnoreCase)
                || string.Equals(chip.Clave, normalizada, StringComparison.OrdinalIgnoreCase)
                || string.Equals(ElectricalFamilyCatalog.ClaveCanonica(chip.Nombre), clave, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static decimal ParseCoef(string? text, decimal fallback)
    {
        var s = (text ?? "").Trim().TrimEnd('%').Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }

    public void ResetCoeficientes()
    {
        if (!_vm.CanEdit)
            return;
        if (!_vm.ConfirmAction("Restablecer factores",
            "¿Desea restablecer todos los factores de familia a 1.00× y eliminar los descuentos aplicados? Se perderán las personalizaciones actuales."))
            return;
        _vm.Undo?.PushUndoPoint(_vm.Session, "Restablecer factores de familia");
        _vm.SuppressCoefRefresh = true;
        try
        {
            foreach (var f in _vm.CoefFactors)
            {
                _vm.Session.SetFamilyMarkup(f.FamilyKey, 1.00m);
                _vm.Session.SetFamilyDiscountPct(f.FamilyKey, 0m);
                f.SetValueSilent(1.00m);
                f.SetDiscountSilent(0m);
            }
            RefreshCoefEffectiveLabels();
            _vm.UpdateCoefChips();
            _vm.RefreshCostOnly();
        }
        finally
        {
            _vm.SuppressCoefRefresh = false;
        }
    }

    public void LoadCoeficientesEstandar() =>
        ApplyCoefPreset(ExcelCostosBridge.PresetFactoresEstandar, fallback: 1.30m, "Factores Estándar");

    public void LoadTipicosCoeficientes() =>
        ApplyCoefPreset(ExcelCostosBridge.PresetFactoresTipicos, fallback: 1.14m, "Factores Típicos");

    public void ApplyCoefPreset(IReadOnlyDictionary<string, decimal> preset, decimal fallback, string presetName)
    {
        if (!_vm.CanEdit)
            return;
        if (!_vm.ConfirmAction("Aplicar factores predefinidos",
            $"¿Desea aplicar los factores y márgenes «{presetName}»?\n\nEsta acción sobrescribirá los factores por familia y los porcentajes de gastos generales y utilidad."))
            return;
        _vm.Undo?.PushUndoPoint(_vm.Session, $"Aplicar preset «{presetName}»");
        _vm.SuppressCoefRefresh = true;
        try
        {
            foreach (var f in _vm.CoefFactors)
            {
                var v = preset.TryGetValue(f.FamilyKey, out var x) ? x : fallback;
                _vm.Session.SetFamilyMarkup(f.FamilyKey, v);
                f.SetValueSilent(v);
            }

            ExcelCostosBridge.ApplyPoliticaEstandar(_vm.Session.ProjectInfo);
            _vm.Session.MarkDirty(ProjectDirtyScope.ProjectInfo);

            _vm.SetGastosGeneralesSilent(5.0m);
            _vm.SetUtilidadSilent(8.0m);
            _vm.SetValorAgregadoEquiposSilent(3.0m);
            _vm.SetPoliticaCostosExcelSilent(true);
            _vm.UtilidadPolicyLabel = "Utilidades (aditiva)";

            RefreshCoefEffectiveLabels();
            _vm.UpdateCoefChips();
            _vm.RefreshCostOnly();
        }
        finally
        {
            _vm.SuppressCoefRefresh = false;
        }
    }

    public void ApplyCoeficientes()
    {
        if (_vm.CanEdit && !_vm.Session.ProjectInfo.PoliticaCostosExcel)
        {
            _vm.Session.ProjectInfo.PoliticaCostosExcel = true;
            _vm.Session.MarkDirty(ProjectDirtyScope.ProjectInfo);
            _vm.SetPoliticaCostosExcelSilent(true);
            _vm.UtilidadPolicyLabel = "Utilidades (aditiva)";
        }

        _vm.RefreshCostOnly();
        _vm.CoefAppliedFlash = true;
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(700)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _vm.CoefAppliedFlash = false;
        };
        timer.Start();
    }
}
