#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.CubicacionSc;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed class PrecioListaResumenVm : ViewModelBase
{
    private readonly BudgetProjectSession session;

    private readonly IPriceBankStore priceBankStore;

    private readonly ObservableCollection<PrecioListaMaterialVm> allItems = new ObservableCollection<PrecioListaMaterialVm>();

    private readonly ICollectionView filteredView;

    private readonly List<PriceBankEntry> allBankEntries;

    private string searchText = "";

    private string activeFilter = "Todos";

    private bool autocompleteVisible;

    public ObservableCollection<FilterChipVm> Filters { get; } = new ObservableCollection<FilterChipVm>();

    public string ActiveFilter
    {
        get => activeFilter;
        set
        {
            if (SetProperty(ref activeFilter, value, nameof(ActiveFilter)))
            {
                filteredView?.Refresh();
                RefreshKpis();
            }
        }
    }

    public int TotalCount => allItems.Count;
    public int ConPrecioCount => allItems.Count(x => !x.SinPrecio);
    public int SinPrecioCount => allItems.Count(x => x.SinPrecio);

    public ObservableCollection<PriceBankSuggestionVm> AutocompleteResults { get; } = new ObservableCollection<PriceBankSuggestionVm>();

    public bool AutocompleteVisible
    {
        get
        {
            return autocompleteVisible;
        }
        set
        {
            SetProperty(ref autocompleteVisible, value, "AutocompleteVisible");
        }
    }

    public ICollectionView Items => filteredView;

    public string SearchText
    {
        get
        {
            return searchText;
        }
        set
        {
            if (SetProperty(ref searchText, value, "SearchText"))
            {
                filteredView?.Refresh();
                OnPropertyChanged(nameof(FilteredCount));
            }
        }
    }

    public int FilteredCount => ((IEnumerable)filteredView)?.Cast<object>().Count() ?? 0;

    public void SearchBank(string query)
    {
        AutocompleteResults.Clear();
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            return;
        }
        string value = query.Trim().ToLowerInvariant();
        // Se busca sobre lo que muestra la lista (banco + materiales del proyecto), no solo el banco:
        // así un material creado solo en este proyecto también se sugiere al escribir su código.
        foreach (PrecioListaMaterialVm item in allItems)
        {
            string text = (item.Codigo ?? "").ToLowerInvariant();
            string text2 = (item.Descripcion ?? "").ToLowerInvariant();
            if (text.Contains(value) || text2.Contains(value))
            {
                AutocompleteResults.Add(new PriceBankSuggestionVm(item.Codigo ?? "", item.Descripcion ?? "", item.Unidad ?? "", item.MatClp, item.MoClp));
            }
        }
        AutocompleteVisible = AutocompleteResults.Count > 0;
    }

    public void ApplySuggestion(PrecioListaMaterialVm target, PriceBankSuggestionVm suggestion)
    {
        target.Codigo = suggestion.Codigo;
        target.Descripcion = suggestion.Descripcion;
        target.Unidad = suggestion.Unidad;
        target.MatClp = suggestion.MatClp;
        target.MoClp = suggestion.MoClp;
        target.FechaPrecio = DateTime.Today;
        ActualizarPrecioEnSesion(target);
    }

    public PrecioListaResumenVm(BudgetProjectSession session, IPriceBankStore priceBankStore, string autoFilterCodigo = "")
    {
        this.session = session;
        this.priceBankStore = priceBankStore;
        allBankEntries = priceBankStore.GetAll().ToList();
        foreach (PriceBankEntry item in allBankEntries.OrderBy((PriceBankEntry x) => x.Codigo))
        {
            allItems.Add(new PrecioListaMaterialVm(item, session));
        }

        // Materiales que existen SOLO en el proyecto (los que crea esta pantalla): no están en el
        // banco de precios global y sin esto desaparecerían de la lista al reabrirla.
        var codigosMostrados = new HashSet<string>(
            allBankEntries.Select((PriceBankEntry e) => CodeHelper.Normalize(e.Codigo)), StringComparer.Ordinal);
        foreach (MaterialRow material in session.Materiales
                     .OrderBy((MaterialRow m) => m.Codigo, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(material.Codigo))
                continue;
            if (codigosMostrados.Add(CodeHelper.Normalize(material.Codigo)))
                allItems.Add(new PrecioListaMaterialVm(material, session));
        }

        filteredView = CollectionViewSource.GetDefaultView(allItems);
        filteredView.Filter = FilterPredicate;
        CalcularFaCommand = new RelayCommand(CalcularFa);
        RefrescarFa();
        RefreshFilterChips();
        if (!string.IsNullOrWhiteSpace(autoFilterCodigo))
        {
            searchText = autoFilterCodigo;
            OnPropertyChanged("SearchText");
            filteredView.Refresh();
        }
    }

    private bool FilterPredicate(object obj)
    {
        if (obj is not PrecioListaMaterialVm item)
        {
            return false;
        }

        if (activeFilter == "Con precio" && item.SinPrecio)
        {
            return false;
        }

        if (activeFilter == "Sin precio" && !item.SinPrecio)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            string value = searchText.Trim().ToLowerInvariant();
            if (!item.Codigo.ToLowerInvariant().Contains(value) &&
                !item.Descripcion.ToLowerInvariant().Contains(value) &&
                !(item.Proveedor ?? "").ToLowerInvariant().Contains(value))
            {
                return false;
            }
        }
        return true;
    }

    public void ActualizarPrecioEnSesion(PrecioListaMaterialVm item)
    {
        item.Guardar(priceBankStore, session.ProjectInfo.ValorUfClp);
        session.SyncCubicacionPricesFromMateriales();
        session.MarkDirty(ProjectDirtyScope.Materiales, ProjectDirtyScope.Cubicacion);
        RefreshKpis();
    }

    // ── Alta rápida ───────────────────────────────────────────────────────────
    // El alta rápida no obliga a escribir un código: lo sugiere la familia, y la M.O. sale de la
    // fórmula Material × % × FA en vez de teclearse. Estas tres piezas son las que consumen el
    // diálogo de alta rápida; el cálculo de la M.O. vive en el motor (MaterialMoResolver) para que
    // la vista previa y el resultado del presupuesto sean siempre la misma cifra.

    /// <summary>Familias del proyecto: lista de precios / banco actual + familias personalizadas y estándar internas sin duplicar.</summary>
    public IReadOnlyList<string> FamiliasDisponibles =>
        ElectricalFamilyCatalog.CombinarFamiliasConOcultas(
            session.ProjectInfo.FamiliasOcultas,
            session.ProjectInfo.FamiliasPersonalizadas,
            allBankEntries.Select(e => e.Categoria));

    /// <summary>FA vigente del proyecto (índice de M.O.): el que se usará al guardar.</summary>
    public decimal IndiceMoFa => MaterialMoResolver.IndiceFa(session.ProjectInfo);

    private string indiceMoFaTexto = "";

    /// <summary>
    /// FA editable desde el propio alta rápida: al cambiarlo se recalibra la M.O. del catálogo y
    /// del itemizado con él, igual que al reescribir COSTOS!E192 en la planilla original.
    /// 0 = sin calibrar: la M.O. por % se calcula con factor neutro (Material × %).
    /// </summary>
    public string IndiceMoFaTexto
    {
        get => indiceMoFaTexto;
        set
        {
            if (!SetProperty(ref indiceMoFaTexto, value ?? ""))
                return;
            if (!TryParseFa(value, out decimal fa) || fa < 0m)
            {
                NotificarExplicacionFa();
                return;
            }
            AplicarFa(fa);
        }
    }

    /// <summary>Deriva el FA desde el plan de mano de obra del proyecto y lo deja aplicado.</summary>
    public RelayCommand CalcularFaCommand { get; }

    /// <summary>FA que corresponde al plan de M.O., siempre calculado, frente al índice vigente.</summary>
    public string FaDerivadoTexto => faDerivadoTexto;

    public bool FaDerivadoVisible => !string.IsNullOrEmpty(faDerivadoTexto);

    private string faDerivadoTexto = "";

    /// <summary>El FA está sin calibrar: la M.O. por % sale de Material × % (factor neutro) hasta que se fije un índice.</summary>
    public bool FaSinCalibrar => IndiceMoFa <= 0m;

    /// <summary>
    /// Por qué el FA está (o no) en cero, en lenguaje llano. El alta rápida lo muestra al lado de
    /// la fórmula: un proyecto recién creado o sin plan de mano de obra deja el FA en 0 y hay que
    /// explicar de dónde sale entonces la cifra de mano de obra.
    /// </summary>
    public string ExplicacionFa
    {
        get
        {
            if (IndiceMoFa > 0m)
                return $"FA {IndiceMoFa:N2} calibrado: la M.O. de este material se calcula sola con Material × % × FA.";

            var resultado = FactorAjusteMoService.CalcularParaProyecto(session.Data, session.ProjectInfo);
            if (resultado.PuedeCalcular)
                return $"El FA está en 0 porque este proyecto todavía no se ha calibrado: la M.O. se calcula por ahora como "
                     + $"Material × % (factor 1). Le corresponde {resultado.Fa!.Value:N2} según su plan de mano de obra: "
                     + "pulsa «Calcular FA» para aplicarlo.";

            if (resultado.TienePlanMo)
                return "El FA está en 0 porque ningún material del itemizado usa M.O. por % (Precio × % × FA), "
                     + "así que no hay con qué calibrarlo: fija el FA a mano o guarda el material sin fórmula.";

            return "El FA está en 0 porque el proyecto aún no tiene plan de mano de obra (dotación y sueldos). "
                 + "Mientras tanto la M.O. se calcula como Material × % (factor neutro) y se recalcula sola al "
                 + "mover el FA desde Costos: complétalo en el módulo Mano de obra, o escribe el FA a mano aquí.";
        }
    }

    /// <summary>Refresca los textos del FA (vigente, derivado y aviso) desde el proyecto.</summary>
    public void RefrescarFa()
    {
        indiceMoFaTexto = IndiceMoFa.ToString("N2", System.Globalization.CultureInfo.CurrentCulture);
        faDerivadoTexto = CalcularFaDerivadoTexto();

        OnPropertyChanged(nameof(IndiceMoFa));
        OnPropertyChanged(nameof(IndiceMoFaTexto));
        OnPropertyChanged(nameof(FaDerivadoTexto));
        OnPropertyChanged(nameof(FaDerivadoVisible));
        NotificarExplicacionFa();
    }

    private void NotificarExplicacionFa()
    {
        OnPropertyChanged(nameof(FaSinCalibrar));
        OnPropertyChanged(nameof(ExplicacionFa));
    }

    private string CalcularFaDerivadoTexto()
    {
        var resultado = FactorAjusteMoService.CalcularParaProyecto(session.Data, session.ProjectInfo);
        if (!resultado.PuedeCalcular)
            return "";

        string derivado = resultado.Fa!.Value.ToString("N2", System.Globalization.CultureInfo.CurrentCulture);
        decimal vigente = IndiceMoFa;
        return Math.Abs(vigente - resultado.Fa.Value) < 0.005m
            ? "FA calibrado con la M.O. real del proyecto (" + derivado + ")."
            : "FA automático del proyecto: " + derivado + " (vigente "
              + vigente.ToString("N2", System.Globalization.CultureInfo.CurrentCulture) + "). Pulsa Calcular FA para aplicarlo.";
    }

    /// <summary>Aplica el FA al proyecto y deja la lista de precios con la M.O. ya recalculada.</summary>
    private void AplicarFa(decimal fa)
    {
        IndiceMoFaAplicado aplicado = session.AplicarIndiceMoFa(fa, priceBankStore);
        foreach (var cambio in aplicado.Materiales)
        {
            var fila = allItems.FirstOrDefault(i => string.Equals(i.Codigo, cambio.Codigo, StringComparison.OrdinalIgnoreCase));
            if (fila != null)
                fila.MoClp = cambio.NuevoClp;
        }

        indiceMoFaTexto = aplicado.IndiceMoFa.ToString("N2", System.Globalization.CultureInfo.CurrentCulture);
        faDerivadoTexto = CalcularFaDerivadoTexto();

        OnPropertyChanged(nameof(IndiceMoFa));
        OnPropertyChanged(nameof(IndiceMoFaTexto));
        OnPropertyChanged(nameof(FaDerivadoTexto));
        OnPropertyChanged(nameof(FaDerivadoVisible));
        NotificarExplicacionFa();
        RefreshKpis();
    }

    private void CalcularFa()
    {
        var resultado = FactorAjusteMoService.CalcularParaProyecto(session.Data, session.ProjectInfo);
        if (!resultado.PuedeCalcular)
        {
            // Sin plan de M.O. no hay con qué derivarlo; se explica en el propio panel en vez de
            // abrir un modal, porque el aviso del FA genérico ya está visible al lado del campo.
            faDerivadoTexto = resultado.TienePlanMo
                ? "Ningún material del itemizado usa M.O. por % (Precio × % × FA): el FA no cambia la mano de obra."
                : "Sin plan de mano de obra: carga dotación y sueldos en el módulo Mano de obra para poder derivar el FA.";
            OnPropertyChanged(nameof(FaDerivadoTexto));
            OnPropertyChanged(nameof(FaDerivadoVisible));
            return;
        }

        IndiceMoFaTexto = resultado.Fa!.Value.ToString("N2", System.Globalization.CultureInfo.CurrentCulture);
    }

    private static bool TryParseFa(string? texto, out decimal fa)
    {
        fa = 0m;
        if (string.IsNullOrWhiteSpace(texto))
            return false;
        string limpio = texto.Trim().Replace(" ", "");
        return decimal.TryParse(limpio, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out fa)
            || decimal.TryParse(limpio.Replace(',', '.'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out fa);
    }

    /// <summary>Código libre sugerido para la familia/descripción; el usuario no necesita escribirlo.</summary>
    public string SugerirCodigo(string? familia, string? descripcion)
    {
        var existentes = allBankEntries.Select(e => e.Codigo)
            .Concat(session.Materiales.Select(m => m.Codigo))
            .Concat(allItems.Select(i => i.Codigo));
        return MaterialCodeGenerator.Siguiente(familia, descripcion, existentes);
    }

    /// <summary>M.O. por fórmula Material × % × FA con el FA del proyecto, para la vista previa.</summary>
    public decimal CalcularMoPorcentaje(decimal matClp, decimal porcentajeMaterial) =>
        MaterialMoResolver.MoClpPorPorcentaje(matClp, porcentajeMaterial, IndiceMoFa);

    /// <summary>
    /// Precio de referencia de un código: primero el banco de precios y, si no está, el catálogo del
    /// proyecto (materiales que solo existen aquí). Se consulta el catálogo y no la lista en pantalla
    /// para no devolver la propia fila que se está editando.
    /// </summary>
    public PriceBankEntry? LookupByCodigo(string codigo)
    {
        PriceBankEntry? delBanco = priceBankStore.GetByCodigo(codigo.Trim());
        if (delBanco != null)
            return delBanco;

        MaterialRow? fila = session.Materiales.FirstOrDefault((MaterialRow m) => CodeHelper.Match(m.Codigo, codigo));
        if (fila == null)
            return null;

        return new PriceBankEntry
        {
            Codigo = fila.Codigo,
            Descripcion = fila.Descripcion,
            Unidad = fila.Unidad,
            Categoria = fila.Categoria,
            MatClp = fila.MatClp,
            MoClp = fila.MoClp,
            Proveedor = fila.Proveedor,
            FechaPrecio = fila.FechaPrecio
        };
    }

    public (decimal MatClp, decimal MoClp)? LookupPricesByDescripcion(string descripcion)
    {
        string buscada = descripcion.Trim();
        PriceBankEntry? priceBankEntry = priceBankStore.GetAll().FirstOrDefault((PriceBankEntry e) => string.Equals(e.Descripcion, buscada, StringComparison.OrdinalIgnoreCase));
        if (priceBankEntry != null)
            return (priceBankEntry.MatClp, priceBankEntry.MoClp);

        MaterialRow? fila = session.Materiales.FirstOrDefault((MaterialRow m) => string.Equals(m.Descripcion, buscada, StringComparison.OrdinalIgnoreCase));
        return fila == null ? null : (fila.MatClp, fila.MoClp);
    }

    /// <summary>
    /// Crea el material en el catálogo del PROYECTO (nunca en el banco global) y lo agrega a la lista.
    /// Devuelve false si el código ya existía, en cuyo caso no se crea nada.
    /// </summary>
    public bool AgregarMaterial(string codigo, string descripcion, string unidad, string categoria, decimal matClp, decimal moClp, string proveedor, DateTime fechaPrecio,
        string moFormulaTipo = "", decimal moPorcentajeMaterial = 2.5m)
    {
        // El material nace en el PROYECTO, no en el banco de precios global: el banco es compartido
        // entre todos los proyectos y estos materiales (tableros, luminarias puntuales) son propios
        // de este presupuesto. Cientos de variantes distintas por proyecto no deben convertirse en
        // catálogo reutilizable de los demás.
        if (ExisteCodigo(codigo))
        {
            WpfDialogService.Info("Código duplicado", "Ya existe un material con el código '" + codigo + "' en la lista de precios.");
            return false;
        }

        decimal valorUfClp = session.ProjectInfo.ValorUfClp;
        // Con fórmula de porcentaje la M.O. se deriva (Material × % × FA) en vez de teclearse; el
        // MoClp guardado queda como valor efectivo para que listas y exportaciones muestren la cifra
        // que el motor ya aplica, y la fórmula queda en el material para que siga viva si cambia el FA.
        bool porPorcentaje = string.Equals(moFormulaTipo, MaterialMoResolver.MoFormulaPorcentaje, StringComparison.OrdinalIgnoreCase)
            && moPorcentajeMaterial > 0m;
        bool porFactor = string.Equals(moFormulaTipo, MaterialMoResolver.MoFormulaFactor, StringComparison.OrdinalIgnoreCase);

        decimal moEfectiva = porPorcentaje
            ? MaterialMoResolver.MoClpPorPorcentaje(matClp, moPorcentajeMaterial, MaterialMoResolver.IndiceFa(session.ProjectInfo))
            : (porFactor
                ? MaterialMoResolver.MoClpPorFactor(moClp, MaterialMoResolver.IndiceFa(session.ProjectInfo))
                : moClp);

        var materialRow = new MaterialRow
        {
            Codigo = codigo,
            Descripcion = descripcion,
            Unidad = (string.IsNullOrWhiteSpace(unidad) ? "un" : unidad),
            Categoria = (categoria ?? ""),
            MatClp = matClp,
            MoClp = moEfectiva,
            MoFactorBase = porFactor ? moClp : 0m,
            MoFormulaTipo = porPorcentaje ? MaterialMoResolver.MoFormulaPorcentaje : (porFactor ? MaterialMoResolver.MoFormulaFactor : (moFormulaTipo ?? "")),
            MoPorcentajeMaterial = moPorcentajeMaterial,
            Proveedor = (proveedor ?? ""),
            FechaPrecio = ((fechaPrecio == default(DateTime)) ? DateTime.Today : fechaPrecio),
            OrigenPrecio = "Lista de precios",
            UltimaActualizacionPrecio = AppClock.Now
        };
        materialRow.RecalcularUf(valorUfClp);

        session.Materiales.Add(materialRow);
        session.MarkDirty(ProjectDirtyScope.Materiales);

        // Si la familia ingresada no es estándar ni del banco de precios, registrarla como personalizada
        if (!string.IsNullOrWhiteSpace(categoria))
        {
            string claveCat = ElectricalFamilyCatalog.ClaveCanonica(categoria);
            bool esEstandar = ElectricalFamilyCatalog.FamiliasEstandar.Any(e =>
                string.Equals(ElectricalFamilyCatalog.ClaveCanonica(e), claveCat, StringComparison.OrdinalIgnoreCase));
            bool esBanco = allBankEntries.Any(b =>
                string.Equals(ElectricalFamilyCatalog.ClaveCanonica(b.Categoria), claveCat, StringComparison.OrdinalIgnoreCase));

            if (!esEstandar && !esBanco)
            {
                session.ProjectInfo.FamiliasPersonalizadas ??= new List<string>();
                if (!session.ProjectInfo.FamiliasPersonalizadas.Any(p =>
                    string.Equals(ElectricalFamilyCatalog.ClaveCanonica(p), claveCat, StringComparison.OrdinalIgnoreCase)))
                {
                    session.ProjectInfo.FamiliasPersonalizadas.Add(categoria.Trim());
                    session.MarkDirty(ProjectDirtyScope.ProjectInfo);
                }
            }
        }

        allItems.Add(new PrecioListaMaterialVm(materialRow, session));
        OnPropertyChanged(nameof(FamiliasDisponibles));
        RefreshKpis();
        filteredView?.Refresh();
        return true;
    }

    /// <summary>El código ya existe en el banco de precios global o en el catálogo del proyecto.</summary>
    private bool ExisteCodigo(string codigo) =>
        !string.IsNullOrWhiteSpace(codigo)
        && (priceBankStore.GetByCodigo(codigo.Trim()) != null
            || session.Materiales.Any((MaterialRow m) => CodeHelper.Match(m.Codigo, codigo)));

    /// <summary>Cuenta cuántos recursos de cubicación en el proyecto activo hacen referencia a este código.</summary>
    public int ContarUsosEnCubicacion(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return 0;
        return session.Cubicacion.Count(c => CodeHelper.Match(c.Codigo, codigo));
    }

    public void EliminarMaterial(PrecioListaMaterialVm vm, bool deleteFromGlobalBank = false)
    {
        // Solo elimina del banco de precios central si se solicitó explícitamente (evita destruir el catálogo maestro)
        if (deleteFromGlobalBank && !vm.EsSoloProyecto)
            priceBankStore.Delete(vm.Codigo);

        MaterialRow? materialRow = session.Materiales.FirstOrDefault((MaterialRow x) => CodeHelper.Match(x.Codigo, vm.Codigo));
        if (materialRow != null)
        {
            session.Materiales.Remove(materialRow);
            session.MarkDirty(ProjectDirtyScope.Materiales);
        }
        allItems.Remove(vm);
        RefreshKpis();
        ICollectionView? obj = filteredView;
        if (obj != null)
        {
            obj.Refresh();
        }
    }

    private void RefreshKpis()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(ConPrecioCount));
        OnPropertyChanged(nameof(SinPrecioCount));
        OnPropertyChanged(nameof(FilteredCount));
        RefreshFilterChips();
    }

    private void RefreshFilterChips()
    {
        Filters.Clear();
        Filters.Add(new FilterChipVm { Nombre = "Todos", Count = TotalCount, IsActive = activeFilter == "Todos" });
        Filters.Add(new FilterChipVm { Nombre = "Con precio", Count = ConPrecioCount, IsActive = activeFilter == "Con precio" });
        Filters.Add(new FilterChipVm { Nombre = "Sin precio", Count = SinPrecioCount, IsActive = activeFilter == "Sin precio" });
    }
}
