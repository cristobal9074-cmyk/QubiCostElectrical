namespace QubiCostElectrical;

public sealed class TasaCambioHistorialRow
{
    public DateTime Fecha { get; set; } = AppClock.Now;
    public decimal UfClp { get; set; }
    public decimal UsdClp { get; set; }
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

public sealed class BudgetVersionStatusHistoryRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string EstadoAnterior { get; set; } = "";
    public string EstadoNuevo { get; set; } = "";
    public DateTime Fecha { get; set; } = AppClock.Now;
    public string Usuario { get; set; } = "";
    public string Comentario { get; set; } = "";
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
    public DateTime FechaTipoCambio { get; set; } = DateTime.MinValue;
    public List<TasaCambioHistorialRow> HistorialTasas { get; set; } = new();
    public bool AutoActualizarIndicadores { get; set; } = true;
    public string EstadoPresupuesto { get; set; } = "Borrador";
    public List<BudgetStatusHistoryRow> HistorialEstados { get; set; } = new();
    public string MarkupMethod { get; set; } = "SobreVenta";
    public decimal GastosGeneralesPct { get; set; } = 14m;
    public decimal UtilidadPct { get; set; } = 18m;
    /// <summary>Valor agregado sobre equipos (Excel COSTOS). Persistido en JSON de project_info.</summary>
    public decimal ValorAgregadoEquiposPct { get; set; } = 0m;
    /// <summary>Si true, motor usa bases GG/UT estilo Excel -=COSTOS=-.</summary>
    public bool PoliticaCostosExcel { get; set; }
    /// <summary>Duración de obra en meses para MO de dotación (puente Excel).</summary>
    public int MoDuracionMeses { get; set; } = 12;
    /// <summary>Leyes sociales % sobre sueldos base de MO (puente Excel / módulo M. de obra).</summary>
    public decimal LeyesSocialesMoPct { get; set; } = 35m;
    /// <summary>
    /// Total MO CLP del módulo Mano de obra (incluye leyes y curva mensual).
    /// Si &gt; 0, el puente Excel lo usa en lugar de recomputar desde filas.
    /// </summary>
    public decimal MoTotalClp { get; set; }
    public decimal HerramientasPct { get; set; } = 0m;
    public decimal MermasPct { get; set; } = 0m;
    public string ApuCostingMode { get; set; } = "CubicacionMasApuComplementarios";
    public decimal RiesgoPct { get; set; } = 0m;
    public decimal DescuentoPct { get; set; } = 0m;
    public decimal IvaPct { get; set; } = 19m;
    public bool IncluirIvaEnTotal { get; set; } = true;
    public decimal MetrosCuadrados { get; set; }
    public Dictionary<string, decimal> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public OfferPdfSettings OfertaPdf { get; set; } = new();
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

    /// <summary>Si es true, el banco y el cat�logo no actualizan precios del proyecto sin confirmaci�n expl�cita.</summary>
    public bool PreciosCongelados { get; set; }

    public DateTime? FechaCongelamientoPrecios { get; set; }
    public string UsuarioCongelamientoPrecios { get; set; } = "";
    public decimal ValorUfClpCongelado { get; set; }
    public decimal TipoCambioUsdClpCongelado { get; set; }
    public DateTime FechaTipoCambioCongelado { get; set; } = DateTime.MinValue;

    /// <summary>Fecha de corte de antecedentes t�cnicos considerados para cubicar y ofertar.</summary>
    public DateTime? FechaCorteAntecedentes { get; set; }

    public List<AntecedenteTecnicoRow> AntecedentesTecnicos { get; set; } = new();

    /// <summary>Último preset de coeficientes guardado en este proyecto (opcional).</summary>
    public CostosCoeficientesPresetRow? CostosPreset { get; set; }
}
