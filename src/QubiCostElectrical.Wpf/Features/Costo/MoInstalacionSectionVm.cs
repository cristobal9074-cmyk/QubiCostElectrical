using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Costo;

/// <summary>
/// Sección "M.O. instalación" de la pantalla Costo: modo por rendimiento (Tarifa D/H ÷
/// rendimiento del material) vs M.O. directa del catálogo, tarifa editable y derivación
/// desde el plan de M.O. Vive fuera de CostoViewModel por el gate de MaxLineCount.
/// </summary>
public sealed class MoInstalacionSectionVm : ViewModelBase
{
    private readonly BudgetProjectSession session;
    private readonly Func<bool> canEdit;
    private readonly Func<IReadOnlyList<MoDirectaRowVm>> moDirectaRows;
    private readonly Action<Action> runSuppressed;
    private readonly Action refreshCost;
    private readonly IPriceBankStore? priceBank;
    private bool modoMoRendimiento;
    private string tarifaDhClpText = "";
    private bool tarifaDhConLeyes;
    private string indiceMoFaText = "";
    private string hint = "";
    private string faAviso = "";
    private string faResultado = "";
    private string faDerivadoTexto = "";
    private bool faAutomatico = true;

    public MoInstalacionSectionVm(
        BudgetProjectSession session,
        Func<bool> canEdit,
        Func<IReadOnlyList<MoDirectaRowVm>> moDirectaRows,
        Action<Action> runSuppressed,
        Action refreshCost,
        IPriceBankStore? priceBank = null)
    {
        this.session = session;
        this.canEdit = canEdit;
        this.moDirectaRows = moDirectaRows;
        this.runSuppressed = runSuppressed;
        this.refreshCost = refreshCost;
        this.priceBank = priceBank;
        DerivarTarifaDhCommand = new RelayCommand(DerivarTarifaDh, () => canEdit());
        CalcularFaCommand = new RelayCommand(CalcularFa, () => canEdit());
        faAutomatico = UserPreferencesStore.Load().FaAutomatico;
    }

    public RelayCommand DerivarTarifaDhCommand { get; }

    /// <summary>Deriva el FA del proyecto desde la M.O. real y el itemizado, en vez de un índice sin calibrar.</summary>
    public RelayCommand CalcularFaCommand { get; }

    /// <summary>M.O. de instalación por rendimiento (true) o directa del catálogo (false).</summary>
    public bool ModoMoRendimiento
    {
        get => modoMoRendimiento;
        set
        {
            if (!SetProperty(ref modoMoRendimiento, value) || !canEdit())
                return;
            Persist(() => session.ProjectInfo.ModoMoInstalacion = value
                ? MaterialMoResolver.ModoRendimiento
                : MaterialMoResolver.ModoDirecto);
        }
    }

    /// <summary>Tarifa por día-hombre en CLP (texto localizado, se formatea al cargar).</summary>
    public string TarifaDhClpText
    {
        get => tarifaDhClpText;
        set
        {
            if (!SetProperty(ref tarifaDhClpText, value ?? ""))
                return;
            if (!canEdit() || !TryParseClp(value, out var clp))
                return;
            Persist(() => session.ProjectInfo.TarifaDhClp = clp);
        }
    }

    /// <summary>Al derivar la tarifa desde el plan MO, incluir leyes sociales sobre el sueldo.</summary>
    public bool TarifaDhConLeyes
    {
        get => tarifaDhConLeyes;
        set
        {
            if (!SetProperty(ref tarifaDhConLeyes, value) || !canEdit())
                return;
            Persist(() => session.ProjectInfo.TarifaDhConLeyes = value);
        }
    }

    /// <summary>
    /// Índice global de M.O. "FA" (paridad COSTOS!E192 del Excel). Solo multiplica dentro de la
    /// fórmula "M.O. por %" (Material × % × FA); las M.O. fijas y el modo rendimiento no cambian.
    /// 0 = sin calibrar: la M.O. por % se calcula con factor 1 (Material × %) hasta que el índice
    /// se derive del plan de obra.
    /// Editable a mano: al confirmarlo se recalibra la M.O. del catálogo y del itemizado con él.
    /// </summary>
    public string IndiceMoFaText
    {
        get => indiceMoFaText;
        set
        {
            if (!SetProperty(ref indiceMoFaText, value ?? ""))
                return;
            if (!canEdit() || !TryParseClp(value, out var fa))
                return;
            if (fa < 0m)
                return;

            // Un FA escrito a mano manda: el modo automático se apaga para no pisarlo en el próximo
            // refresco. El usuario puede reactivarlo cuando quiera volver a cuadrar con el plan.
            bool eraAutomatico = faAutomatico;
            if (eraAutomatico)
            {
                faAutomatico = false;
                OnPropertyChanged(nameof(FaAutomatico));
                OnPropertyChanged(nameof(FaModoHint));
                GuardarPreferenciaFaAutomatico(false);
            }

            IndiceMoFaAplicado aplicado = AplicarFa(fa);
            if (eraAutomatico)
            {
                faResultado = fa <= 0m
                    ? "FA 0,00: sin calibrar, la M.O. por % se calcula como Material × % (factor 1). " + ResumenPropagacion(aplicado)
                    : "FA " + aplicado.IndiceMoFa.ToString("N2", CultureInfo.CurrentCulture)
                      + " fijado a mano." + ResumenPropagacion(aplicado)
                      + " El modo Automático quedó desactivado: reactívalo para que el FA se recalcule solo.";
                OnPropertyChanged(nameof(FaResultado));
            }
        }
    }

    public string MoInstalacionHint => hint;

    /// <summary>
    /// Modo automático del FA: mientras está activo, el índice se recalcula solo para que la M.O.
    /// del itemizado ("EN PPTO") cuadre con el costo real del plan de obra ("COSTO"), que es la
    /// brecha que el Excel cerraba tecleando 62,80 a mano. Se guarda como preferencia del usuario y
    /// se desactiva al escribir un FA a mano, para no pisar el valor que el estimador acaba de fijar.
    /// </summary>
    public bool FaAutomatico
    {
        get => faAutomatico;
        set
        {
            if (!SetProperty(ref faAutomatico, value))
                return;

            OnPropertyChanged(nameof(FaModoHint));
            GuardarPreferenciaFaAutomatico(value);
            if (!value)
            {
                UpdateFaDerivado();
                return;
            }

            // Al activarlo, deja la fila FACTORES cuadrada de inmediato.
            SincronizarFaAutomatico();
            UpdateFaDerivado();
        }
    }

    /// <summary>Etiqueta de estado del modo, para que el usuario vea de un vistazo quién manda.</summary>
    public string FaModoHint => faAutomatico
        ? "Automático: el FA se recalcula solo para que EN PPTO cuadre con el costo real."
        : "Manual: el FA queda como lo dejaste; pulsa Calcular FA para cuadrarlo con el costo real.";

    /// <summary>
    /// Aviso cuando el FA no puede derivarse: el proyecto no tiene plan de M.O. cargado, así que
    /// el índice queda en 0 (sin calibrar) y la M.O. por % se calcula con factor 1 (Material × %).
    /// </summary>
    public string FaAviso => faAviso;

    public bool FaAvisoVisible => !string.IsNullOrEmpty(faAviso);

    /// <summary>Detalle en pesos del último cálculo de FA (M.O. real, catálogo, base e índice).</summary>
    public string FaResultado => faResultado;

    /// <summary>
    /// FA que corresponde al plan de mano de obra del proyecto, calculado siempre (sin pulsar nada)
    /// y comparado con el vigente, para que el usuario vea de inmediato si su índice está calibrado.
    /// </summary>
    public string FaDerivadoTexto => faDerivadoTexto;

    public bool FaDerivadoVisible => !string.IsNullOrEmpty(faDerivadoTexto);

    // ─── Bloque FACTORES (paridad Excel COSTOS!D191:I192) ─────────────────────
    // El Excel compara, en la misma fila del factor de M/O, la M.O. que quedó "EN PPTO" con el
    // "COSTO" real del plan de obra y los "TOTAL DD/HH". El FA existe para cerrar esa brecha; por
    // eso la tabla muestra las tres cifras juntas en vez de solo el índice.

    private string moEnPptoLabel = "—";
    private string moCostoRealLabel = "—";
    private string moDiasHombreLabel = "—";
    private string moDeltaLabel = "—";
    private bool moDeltaEsAviso;
    private string moTarifaLabel = "";

    /// <summary>M.O. del itemizado con el FA vigente (Excel: EN PPTO · COSTOS!F192).</summary>
    public string MoEnPptoLabel => moEnPptoLabel;

    /// <summary>Costo real del plan de mano de obra (Excel: COSTO · MANO DE OBRA!T23).</summary>
    public string MoCostoRealLabel => moCostoRealLabel;

    /// <summary>Días-hombre del plan (Excel: TOTAL DD/HH · MANO DE OBRA!U23).</summary>
    public string MoDiasHombreLabel => moDiasHombreLabel;

    /// <summary>Tarifa D/H implícita del plan, si se puede derivar del costo y los DD/HH.</summary>
    public string MoTarifaLabel => moTarifaLabel;

    /// <summary>Desviación de la M.O. del itemizado respecto del costo real (Excel: la brecha a cerrar).</summary>
    public string MoDeltaLabel => moDeltaLabel;

    /// <summary>True cuando la brecha amerita atención (> 0,5 %): el FA está sin cuadrar.</summary>
    public bool MoDeltaEsAviso => moDeltaEsAviso;

    /// <summary>Hay un plan de M.O. cargado: sin él la comparación del FACTORES no tiene contra qué.</summary>
    public bool FactoresTieneCostoReal => moCostoRealLabel != "—";

    /// <summary>Carga el estado desde el proyecto (inicializa el modo desde la preferencia global si está vacío).</summary>
    public void Load(ProjectInfoRow project)
    {
        if (string.IsNullOrEmpty(project.ModoMoInstalacion))
        {
            var prefs = UserPreferencesStore.Load();
            project.ModoMoInstalacion = prefs.MoInstalacionPorRendimientoDefault
                ? MaterialMoResolver.ModoRendimiento
                : MaterialMoResolver.ModoDirecto;
        }

        modoMoRendimiento = string.Equals(
            project.ModoMoInstalacion, MaterialMoResolver.ModoRendimiento, StringComparison.OrdinalIgnoreCase);
        OnPropertyChanged(nameof(ModoMoRendimiento));
        tarifaDhClpText = project.TarifaDhClp > 0m
            ? project.TarifaDhClp.ToString("N0", CultureInfo.CurrentCulture)
            : "";
        OnPropertyChanged(nameof(TarifaDhClpText));
        tarifaDhConLeyes = project.TarifaDhConLeyes;
        OnPropertyChanged(nameof(TarifaDhConLeyes));
        indiceMoFaText = project.IndiceMoFa.ToString("N2", CultureInfo.CurrentCulture);
        OnPropertyChanged(nameof(IndiceMoFaText));
        faResultado = "";
        OnPropertyChanged(nameof(FaResultado));
        UpdateHint();

        // Modo automático: al abrir/refrescar el proyecto el FA queda cuadrado con el plan de obra
        // (EN PPTO = COSTO) sin que el usuario pulse nada. Sin plan de M.O. o sin base de % no hay
        // nada que cuadrar y el índice se respeta tal cual.
        SincronizarFaAutomatico();
        UpdateFaDerivado();
    }

    /// <summary>
    /// Deriva el FA del proyecto: FA = (M.O. real − M.O. ya cubierta por el catálogo/rendimiento)
    /// ÷ Σ(Material × %) del itemizado. Es lo que en la planilla original era un número tecleado
    /// a mano (COSTOS!E192 = 62,80) hasta que la M.O. del itemizado cuadraba con el plan de obra.
    /// </summary>
    private void CalcularFa()
    {
        if (!canEdit())
            return;

        var resultado = FactorAjusteMoService.CalcularParaProyecto(session.Data, session.ProjectInfo);
        if (!resultado.PuedeCalcular)
        {
            faResultado = resultado.TienePlanMo
                ? "No hay materiales con M.O. por % en el itemizado: el FA no cambia la M.O. del proyecto."
                : "Este proyecto no tiene plan de mano de obra: carga dotación y sueldos en Mano de obra para derivar el FA.";
            OnPropertyChanged(nameof(FaResultado));
            return;
        }

        IndiceMoFaAplicado aplicado = AplicarFa(resultado.Fa!.Value);

        faResultado = "M.O. real " + CurrencyFormat.Clp(resultado.MoRealClp)
            + " − ya cubierta " + CurrencyFormat.Clp(resultado.MoCubiertaClp)
            + " = " + CurrencyFormat.Clp(resultado.RemanenteClp)
            + " ÷ base % " + CurrencyFormat.Clp(resultado.BasePorcentajeClp)
            + " → FA " + resultado.Fa.Value.ToString("N2", CultureInfo.CurrentCulture)
            + " (" + resultado.LineasPorcentaje + " líneas con M.O. por %)."
            + ResumenPropagacion(aplicado);
        OnPropertyChanged(nameof(FaResultado));
    }

    /// <summary>Deja constancia de cuánto se movió al aplicar el FA (catálogo e itemizado).</summary>
    private static string ResumenPropagacion(IndiceMoFaAplicado aplicado) =>
        aplicado.MaterialesRecalibrados == 0 && aplicado.CubicacionSincronizada == 0
            ? ""
            : " Recalibrado: " + aplicado.MaterialesRecalibrados + " materiales · "
              + aplicado.CubicacionSincronizada + " filas de cubicación.";

    /// <summary>
    /// Cierra la fila FACTORES sin intervención del usuario: recalcula el FA para que la M.O. del
    /// itemizado (EN PPTO) iguale el costo real del plan de obra (COSTO) y lo aplica al catálogo,
    /// al banco de precios y al itemizado. Devuelve true solo si movió el índice.
    /// </summary>
    private bool SincronizarFaAutomatico()
    {
        if (!faAutomatico || !canEdit())
            return false;

        var resultado = FactorAjusteMoService.CalcularParaProyecto(session.Data, session.ProjectInfo);
        if (!resultado.PuedeCalcular)
            return false;

        // Convergencia exacta: el FA derivado se redondea a 2 decimales y no depende del vigente
        // (la base es Material × %, no la M.O. ya guardada), así que no hay oscilación entre refrescos.
        decimal fa = resultado.Fa!.Value;
        if (fa == MaterialMoResolver.IndiceFa(session.ProjectInfo))
            return false;

        IndiceMoFaAplicado aplicado = AplicarFa(fa);
        faResultado = "FA automático " + fa.ToString("N2", CultureInfo.CurrentCulture)
            + ": EN PPTO " + CurrencyFormat.Clp(resultado.MoItemizadoClp(fa))
            + " = COSTO " + CurrencyFormat.Clp(resultado.MoRealClp)
            + " (M.O. real − ya cubierta " + CurrencyFormat.Clp(resultado.MoCubiertaClp)
            + " ÷ base % " + CurrencyFormat.Clp(resultado.BasePorcentajeClp) + ")."
            + ResumenPropagacion(aplicado);
        OnPropertyChanged(nameof(FaResultado));
        return true;
    }

    /// <summary>Guarda el modo automático como preferencia global (nunca lanza).</summary>
    private static void GuardarPreferenciaFaAutomatico(bool valor)
    {
        try
        {
            var prefs = UserPreferencesStore.Load();
            if (prefs.FaAutomatico == valor)
                return;
            prefs.FaAutomatico = valor;
            UserPreferencesStore.Save(prefs);
        }
        catch (Exception ex)
        {
            AppLog.Warning("Costo", "No se pudo guardar el modo automático del FA", ex);
        }
    }

    /// <summary>Aplica el FA al proyecto y refresca la M.O. de materiales, banco de precios y costo.</summary>
    private IndiceMoFaAplicado AplicarFa(decimal fa)
    {
        IndiceMoFaAplicado aplicado = session.AplicarIndiceMoFa(fa, priceBank);
        indiceMoFaText = aplicado.IndiceMoFa.ToString("N2", CultureInfo.CurrentCulture);
        OnPropertyChanged(nameof(IndiceMoFaText));

        if (aplicado.MaterialesRecalibrados > 0 || aplicado.CubicacionSincronizada > 0)
        {
            faResultado = "FA " + aplicado.IndiceMoFa.ToString("N2", CultureInfo.CurrentCulture)
                + " aplicado." + ResumenPropagacion(aplicado);
            OnPropertyChanged(nameof(FaResultado));
        }

        runSuppressed(refreshCost);
        UpdateHint();
        return aplicado;
    }

    /// <summary>
    /// Deriva la tarifa D/H desde el plan de M.O. directa: promedio de los cargos maestros
    /// (o el mayor sueldo si no hay maestros) ÷ 22 jornadas, con leyes sociales opcionales.
    /// </summary>
    private void DerivarTarifaDh()
    {
        if (!canEdit())
            return;
        var conSueldo = moDirectaRows().Where(r => r.SueldoMensualClp > 0m).ToList();
        if (conSueldo.Count == 0)
            return;
        var maestros = conSueldo
            .Where(r => r.Cargo.Contains("MAESTR", StringComparison.OrdinalIgnoreCase))
            .ToList();
        decimal sueldo = maestros.Count > 0
            ? maestros.Average(r => r.SueldoMensualClp)
            : conSueldo.Max(r => r.SueldoMensualClp);
        decimal tarifa = MaterialMoResolver.DerivarTarifaDh(
            sueldo, session.ProjectInfo.LeyesSocialesMoPct, session.ProjectInfo.TarifaDhConLeyes);
        if (tarifa <= 0m)
            return;
        TarifaDhClpText = tarifa.ToString("N0", CultureInfo.CurrentCulture);
    }

    /// <summary>Escribe el campo en ProjectInfo, marca dirty, reempuja MO a cubicación y refresca costo.</summary>
    private void Persist(Action write)
    {
        runSuppressed(() =>
        {
            write();
            session.MarkDirty(ProjectDirtyScope.ProjectInfo);
            int synced = session.SyncCubicacionPricesFromMateriales();
            if (synced > 0)
                session.MarkDirty(ProjectDirtyScope.Cubicacion);
            refreshCost();
        });
        UpdateHint();
    }

    private void UpdateHint()
    {
        int conPorcentaje = session.Materiales.Count(m => MaterialMoResolver.DebeUsarPorcentaje(m));
        string porcentajeSuffix = conPorcentaje > 0
            ? " · " + conPorcentaje + " materiales con M.O. por % (Precio × % × FA)"
            : "";
        if (!modoMoRendimiento)
        {
            hint = "M.O. directa del catálogo (comportamiento histórico)." + porcentajeSuffix;
        }
        else
        {
            int conRend = session.Materiales.Count(m => m.RendimientoDh > 0m);
            hint = (conRend == 0
                ? "Sin rendimientos en el catálogo: se usa M.O. directa."
                : conRend + " materiales con rendimiento · " + (session.ProjectInfo.TarifaDhClp > 0m
                    ? session.ProjectInfo.TarifaDhClp.ToString("N0", CultureInfo.CurrentCulture) + " CLP/D-H"
                    : "tarifa sin definir (fallback a directo)")) + porcentajeSuffix;
        }
        OnPropertyChanged(nameof(MoInstalacionHint));
        UpdateFaAviso(conPorcentaje);
        UpdateFaDerivado();
        UpdateFactores();
    }

    /// <summary>
    /// Reproyecta el bloque FACTORES: M.O. del itemizado con el FA vigente contra el costo real
    /// del plan de obra, más los días-hombre y la tarifa implícita. Es la misma lectura que hace
    /// el Excel en COSTOS!E192:G192.
    /// </summary>
    private void UpdateFactores()
    {
        var resultado = FactorAjusteMoService.CalcularParaProyecto(session.Data, session.ProjectInfo);
        decimal faVigente = MaterialMoResolver.IndiceFa(session.ProjectInfo);
        decimal enPpto = resultado.MoItemizadoClp(faVigente);
        decimal costoReal = resultado.MoRealClp;
        decimal diasHombre = FactorAjusteMoService.ResolverDiasHombre(session.Data, session.ProjectInfo);

        string nuevoEnPpto = CurrencyFormat.Clp(enPpto);
        string nuevoCosto = costoReal > 0m ? CurrencyFormat.Clp(costoReal) : "—";
        string nuevosDias = diasHombre > 0m ? diasHombre.ToString("N0", CultureInfo.CurrentCulture) : "—";
        string nuevaTarifa = costoReal > 0m && diasHombre > 0m
            ? CurrencyFormat.Clp(Math.Round(costoReal / diasHombre, 0, MidpointRounding.AwayFromZero)) + " /D-H"
            : "";
        decimal desvio = FactorAjusteMoResultado.DesviacionPct(enPpto, costoReal);
        string nuevaDelta = costoReal > 0m
            ? desvio.ToString("N2", CultureInfo.CurrentCulture) + " %"
            : "—";
        bool nuevoAviso = costoReal > 0m && Math.Abs(desvio) >= 0.5m;

        bool cambio = nuevoEnPpto != moEnPptoLabel || nuevoCosto != moCostoRealLabel
            || nuevosDias != moDiasHombreLabel || nuevaTarifa != moTarifaLabel
            || nuevaDelta != moDeltaLabel || nuevoAviso != moDeltaEsAviso;
        if (!cambio)
            return;

        moEnPptoLabel = nuevoEnPpto;
        moCostoRealLabel = nuevoCosto;
        moDiasHombreLabel = nuevosDias;
        moTarifaLabel = nuevaTarifa;
        moDeltaLabel = nuevaDelta;
        moDeltaEsAviso = nuevoAviso;

        OnPropertyChanged(nameof(MoEnPptoLabel));
        OnPropertyChanged(nameof(MoCostoRealLabel));
        OnPropertyChanged(nameof(MoDiasHombreLabel));
        OnPropertyChanged(nameof(MoTarifaLabel));
        OnPropertyChanged(nameof(MoDeltaLabel));
        OnPropertyChanged(nameof(MoDeltaEsAviso));
        OnPropertyChanged(nameof(FactoresTieneCostoReal));
    }

    /// <summary>
    /// Muestra siempre el FA que corresponde al plan de M.O. del proyecto frente al vigente, sin
    /// esperar a que el usuario pulse nada: si están desalineados, la M.O. del itemizado no cuadra
    /// con el costo real de la obra (que es justo lo que el FA existe para cerrar).
    /// </summary>
    private void UpdateFaDerivado()
    {
        var resultado = FactorAjusteMoService.CalcularParaProyecto(session.Data, session.ProjectInfo);
        string texto = "";
        if (resultado.PuedeCalcular)
        {
            decimal vigente = MaterialMoResolver.IndiceFa(session.ProjectInfo);
            string derivado = resultado.Fa!.Value.ToString("N2", CultureInfo.CurrentCulture);
            texto = Math.Abs(vigente - resultado.Fa.Value) < 0.005m
                ? "FA calibrado: el índice vigente (" + derivado + ") cuadra la M.O. del itemizado (EN PPTO) con el costo real."
                : faAutomatico
                    ? "FA automático: se recalcula a " + derivado + " para cuadrar EN PPTO con COSTO (vigente "
                      + vigente.ToString("N2", CultureInfo.CurrentCulture) + ")."
                    : "FA del proyecto: " + derivado + " (vigente "
                      + vigente.ToString("N2", CultureInfo.CurrentCulture) + "). Pulsa Calcular FA para cuadrar la M.O. del itemizado con el costo real.";
        }

        if (faDerivadoTexto == texto)
            return;
        faDerivadoTexto = texto;
        OnPropertyChanged(nameof(FaDerivadoTexto));
        OnPropertyChanged(nameof(FaDerivadoVisible));
    }

    /// <summary>
    /// El FA ya no es una constante: se deriva de la M.O. real del proyecto. Sin plan de M.O. no
    /// hay con qué derivarlo, así que se avisa en pantalla en vez de dejar el genérico en silencio.
    /// </summary>
    private void UpdateFaAviso(int materialesPorcentaje)
    {
        decimal moReal = FactorAjusteMoService.ResolverMoRealClp(session.Data, session.ProjectInfo);
        string aviso = moReal <= 0m
            ? "Sin plan de mano de obra en este proyecto: el FA queda en 0 (sin calibrar) y la M.O. por % se calcula con factor 1 (Material × %). "
              + "Carga dotación y sueldos en Mano de obra: con Automático activo el índice se calibra solo."
            : materialesPorcentaje == 0
                ? "Ningún material usa M.O. por % (Precio × % × FA): el FA no cambia la M.O. del itemizado."
                : "";

        if (faAviso != aviso)
        {
            faAviso = aviso;
            OnPropertyChanged(nameof(FaAviso));
            OnPropertyChanged(nameof(FaAvisoVisible));
        }

        if (string.IsNullOrEmpty(faResultado) && moReal > 0m && materialesPorcentaje > 0)
        {
            faResultado = faAutomatico
                ? "M.O. real del proyecto " + CurrencyFormat.Clp(moReal)
                  + ". El FA automático mantiene EN PPTO cuadrado con COSTO."
                : "M.O. real del proyecto " + CurrencyFormat.Clp(moReal)
                  + ". Pulsa Calcular FA para derivar el índice.";
            OnPropertyChanged(nameof(FaResultado));
        }
    }

    private static bool TryParseClp(string? text, out decimal clp)
    {
        clp = 0m;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var limpio = text.Trim().Replace(" ", "").Replace("$", "");
        return decimal.TryParse(limpio, NumberStyles.Number, CultureInfo.CurrentCulture, out clp)
            || decimal.TryParse(limpio, NumberStyles.Number, CultureInfo.InvariantCulture, out clp);
    }
}
