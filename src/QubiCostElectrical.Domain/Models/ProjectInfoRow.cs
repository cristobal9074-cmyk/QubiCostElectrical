namespace QubiCostElectrical;

public sealed class TasaCambioHistorialRow
{
    public DateTime Fecha { get; set; } = AppClock.Now;
    public decimal UfClp { get; set; }
    public decimal UsdClp { get; set; }
    public decimal EurClp { get; set; }
    public bool Activa { get; set; }
    public string Nota { get; set; } = "";
}

public sealed class BudgetStatusHistoryRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string EstadoAnterior { get; set; } = "";
    public string EstadoNuevo { get; set; } = EstadoPartida.Borrador;
    public DateTime Fecha { get; set; } = AppClock.Now;
    public string Usuario { get; set; } = "";
    public string Nota { get; set; } = "";
    public string ApprovedBy { get; set; } = "";
    public DateTime? ApprovalDate { get; set; }
    public string DigitalSignatureHash { get; set; } = "";
    public string TransitionHash { get; set; } = "";
}

public sealed class ProjectInfoRow
{
    public string Nombre { get; set; } = "Proyecto sin nombre";
    public string Cliente { get; set; } = "Cliente sin nombre";
    public string Ubicacion { get; set; } = "Sin ubicación";
    public string TipoProyecto { get; set; } = "Edificio habitacional";
    public string UsuarioNombre { get; set; } = "";
    public string UsuarioRol { get; set; } = "Usuario";
    public DateTime FechaCreacion { get; set; } = AppClock.Now;
    public DateTime FechaModificacion { get; set; } = AppClock.Now;
    public decimal ValorUfClp { get; set; }
    public string MonedaPresupuesto { get; set; } = "CLP";
    public decimal TipoCambioUsdClp { get; set; }
    public decimal TipoCambioEurClp { get; set; } = 1050m;
    public DateTime FechaTipoCambio { get; set; } = DateTime.MinValue;
    public List<TasaCambioHistorialRow> HistorialTasas { get; set; } = new();
    public bool AutoActualizarIndicadores { get; set; } = true;
    public string EstadoPresupuesto { get; set; } = "Borrador";
    public List<BudgetStatusHistoryRow> HistorialEstados { get; set; } = new();
    public string MarkupMethod { get; set; } = "SobreVenta";
    public decimal GastosGeneralesPct { get; set; } = 14m;
    public decimal UtilidadPct { get; set; } = 18m;
    /// <summary>Valor agregado sobre equipos (paridad estándar). Persistido en JSON de project_info.</summary>
    public decimal ValorAgregadoEquiposPct { get; set; } = 0m;
    /// <summary>Si true, motor usa bases GG/UT de paridad estándar.</summary>
    public bool PoliticaCostosExcel { get; set; }
    /// <summary>
    /// Fuerza la venta línea a línea con el factor de presentación por familia
    /// (markup × (1 − descuento%)) en vez del markup global GG/Utilidad. Independiente de
    /// <see cref="PoliticaCostosExcel"/>, con la que se combina por OR: la política Excel COSTOS
    /// ya implica factores por familia. Sin ninguna de las dos, un GgUtFactor ≠ 1 suelto NO
    /// cambia el método de markup del presupuesto (C3).
    /// </summary>
    public bool UsarFactoresPresentacionFamilia { get; set; }
    /// <summary>
    /// Factor de presentación estandar del proyecto para las familias que el usuario no
    /// configura. Los proyectos nuevos usan 1,00 (precio igual a costo, sin sobreventa) hasta
    /// que se cargue una configuracion guardada. 0 = usar la tabla historica del producto,
    /// que conservan los presupuestos ya presupuestados.
    /// </summary>
    public decimal MarkupFactorPorDefecto { get; set; }
    /// <summary>Duración de obra en meses para MO de dotación (puente Excel).</summary>
    public int MoDuracionMeses { get; set; } = 12;
    /// <summary>Leyes sociales % sobre sueldos base de MO (puente Excel / módulo M. de obra).</summary>
    public decimal LeyesSocialesMoPct { get; set; } = 35m;
    /// <summary>
    /// Total MO CLP del módulo Mano de obra (incluye leyes y curva mensual).
    /// Si &gt; 0, el puente Excel lo usa en lugar de recomputar desde filas.
    /// </summary>
    public decimal MoTotalClp { get; set; }
    /// <summary>
    /// Total MO Directa CLP del módulo Mano de obra (incluye leyes y curva mensual).
    /// Usado para la calibración del FA y la comparación en FACTORES (COSTOS!G192 del Excel).
    /// </summary>
    public decimal MoDirectaClp { get; set; }
    /// <summary>
    /// Modo del precio de M.O. de instalación por material. <see cref="MaterialMoResolver.ModoDirecto"/>
    /// usa el M.O. directo del catálogo/banco; <see cref="MaterialMoResolver.ModoRendimiento"/> deriva
    /// el precio como TarifaDhClp ÷ RendimientoDh (con fallback automático al directo cuando falta
    /// rendimiento o tarifa). Vacío = Directo (proyectos antiguos sin el campo).
    /// </summary>
    public string ModoMoInstalacion { get; set; } = "";
    /// <summary>Tarifa por día-hombre en CLP usada en ModoRendimiento. 0 = sin tarifa (fallback al directo).</summary>
    public decimal TarifaDhClp { get; set; }
    /// <summary>Si true, la tarifa derivada desde el plan de M.O. multiplica (1 + LeyesSocialesMoPct/100).</summary>
    public bool TarifaDhConLeyes { get; set; }
    /// <summary>
    /// Índice global de M.O. "FA" (COSTOS!E192 del Excel). Solo multiplica dentro de la fórmula de
    /// material "Porcentaje" (Material × % × FA); las M.O. fijas y el modo rendimiento no se afectan.
    /// 0 = sin calibrar: se deriva del plan de obra (Automático o Calcular FA) y queda guardado aquí.
    /// </summary>
    public decimal IndiceMoFa { get; set; } = MaterialMoResolver.IndiceMoFaDefault;
    public decimal HerramientasPct { get; set; } = 0m;
    public decimal MermasPct { get; set; } = 0m;
    /// <summary>
    /// Estimación de ferretería de paridad estándar: porcentaje sobre el costo de materiales
    /// (G30 × E34 / 100). Activa por defecto al crear un nuevo presupuesto (5% estándar).
    /// </summary>
    public bool FerreteriaActiva { get; set; } = true;
    /// <summary>Porcentaje editable de la estimación de ferretería. En el Excel de referencia es 5.</summary>
    public decimal FerreteriaPct { get; set; } = 5m;
    public string ApuCostingMode { get; set; } = "CubicacionMasApuComplementarios";
    public decimal RiesgoPct { get; set; } = 0m;
    public decimal DescuentoPct { get; set; } = 0m;
    public decimal IvaPct { get; set; } = 19m;
    public bool IncluirIvaEnTotal { get; set; } = true;
    /// <summary>Estándar comercial para partidas nuevas. El valor predeterminado conserva el cálculo histórico.</summary>
    public CommercialCostingModel CommercialModel { get; set; } = CommercialCostingModel.ApuDirectCostWithCommercialFactors;
    public CommercialProfile CommercialProfile { get; set; } = CommercialProfile.StandardConstructor;
    public bool AllowPartidaCommercialOverrides { get; set; } = true;
    /// <summary>Excepciones explícitas por PartidaId. No altera filas de cubicación ni APU existentes.</summary>
    public List<CommercialPartidaRule> CommercialPartidaRules { get; set; } = new();
    public decimal MetrosCuadrados { get; set; }
    public Dictionary<string, decimal> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Familia de la lista de precios (clave normalizada) → fila de factor (Conductores, Canalización…).
    /// Vacío = queda sin asignar. Se guarda en el JSON del proyecto, sin cambio de esquema.
    /// </summary>
    public Dictionary<string, string> FamiliasPrecioAsignadas { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Familias creadas o administradas explícitamente por el usuario en este proyecto.
    /// Se guarda en el JSON del proyecto, sin cambio de esquema SQLite.
    /// </summary>
    public List<string> FamiliasPersonalizadas { get; set; } = new();

    /// <summary>
    /// Familias estándar que el usuario decidió ocultar o dar de baja en este proyecto.
    /// Se guarda en el JSON del proyecto, sin cambio de esquema SQLite.
    /// </summary>
    public List<string> FamiliasOcultas { get; set; } = new();
    public OfferPdfSettings OfertaPdf { get; set; } = new();

    /// <summary>
    /// Si true, el resumen de la oferta comercial (PDF y Excel) muestra precios enteros (con GG y UT prorrateados)
    /// y oculta las líneas de GG y Utilidad al final.
    /// </summary>
    public bool OcultarGgYUtilidadEnResumen
    {
        get => OfertaPdf?.OcultarGgYUtilidadEnResumen ?? true;
        set
        {
            OfertaPdf ??= new();
            OfertaPdf.OcultarGgYUtilidadEnResumen = value;
        }
    }
    public List<CubicacionCellCommentRow> CubicacionCellComments { get; set; } = new();
    public DateTime LastSQLiteSavedAt { get; set; } = DateTime.MinValue;
    public List<TechnicalValidationRuleRow> TechnicalValidationRules { get; set; } = new();
    public List<RealCostRow> RealCosts { get; set; } = new();
    public List<PartidaWorkflowRow> PartidaWorkflow { get; set; } = new();
    public List<PartidaChangeTimelineRow> PartidaTimeline { get; set; } = new();
    public List<ProjectAuditEntryRow> AuditLog { get; set; } = new();
    public List<ProactiveAlertRuleRow> ProactiveAlertRules { get; set; } = new();
    public LaborDaySettingsRow LaborDaySettings { get; set; } = new();
    public int PriceExpiryWarningDays { get; set; } = 150;
    public int PriceExpiryCriticalDays { get; set; } = 180;
    public int SolicitudCorrelativo { get; set; } = 1;

    /// <summary>Umbral de vencimiento efectivo (nunca 0 o negativo, aunque el proyecto lo tenga mal configurado).</summary>
    public int EffectivePriceExpiryCriticalDays => PriceExpiryCriticalDays <= 0 ? 180 : PriceExpiryCriticalDays;

    /// <summary>Regla única de "precio vencido" para todo el proyecto, en vez de repetir el umbral hardcodeado en cada pantalla.</summary>
    public bool IsPrecioVencido(DateTime fechaPrecio) =>
        fechaPrecio < AppClock.Now.Date.AddDays(-EffectivePriceExpiryCriticalDays);

    /// <summary>Umbral de alza de precio de material que dispara alerta en el proyecto.</summary>
    public decimal PriceIncreaseAlertThresholdPct { get; set; } = 8m;

    /// <summary>Si es true, el banco y el catálogo no actualizan precios del proyecto sin confirmación explícita.</summary>
    public bool PreciosCongelados { get; set; }

    public DateTime? FechaCongelamientoPrecios { get; set; }
    public string UsuarioCongelamientoPrecios { get; set; } = "";
    public decimal ValorUfClpCongelado { get; set; }
    public decimal TipoCambioUsdClpCongelado { get; set; }
    public DateTime FechaTipoCambioCongelado { get; set; } = DateTime.MinValue;

    /// <summary>Fecha de corte de antecedentes técnicos considerados para cubicar y ofertar.</summary>
    public DateTime? FechaCorteAntecedentes { get; set; }

    public List<AntecedenteTecnicoRow> AntecedentesTecnicos { get; set; } = new();

    /// <summary>Último preset de coeficientes guardado en este proyecto (opcional).</summary>
    public CostosCoeficientesPresetRow? CostosPreset { get; set; }
}
