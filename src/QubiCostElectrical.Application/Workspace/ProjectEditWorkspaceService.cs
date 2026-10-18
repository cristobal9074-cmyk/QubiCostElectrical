namespace QubiCostElectrical;

public sealed class ProjectEditSnapshot
{
    public string Nombre { get; set; } = "";
    public string Cliente { get; set; } = "";
    public string Ubicacion { get; set; } = "";
    public string TipoProyecto { get; set; } = "";
    public string UsuarioNombre { get; set; } = "";
    public string UsuarioRol { get; set; } = "";
    public string MonedaPresupuesto { get; set; } = "CLP";
    public string EstadoPresupuesto { get; set; } = "Borrador";
    public decimal ValorUfClp { get; set; }
    public decimal TipoCambioUsdClp { get; set; }
    public decimal TipoCambioEurClp { get; set; } = 1050m;
    public decimal GastosGeneralesPct { get; set; }
    public decimal UtilidadPct { get; set; }
    public decimal RiesgoPct { get; set; }
    public decimal DescuentoPct { get; set; }
    public decimal IvaPct { get; set; }
    public CommercialCostingModel CommercialModel { get; set; } = CommercialCostingModel.ApuDirectCostWithCommercialFactors;
    public CommercialProfile CommercialProfile { get; set; } = CommercialProfile.StandardConstructor;
    public bool AllowPartidaCommercialOverrides { get; set; } = true;
    public string ApuCostingMode { get; set; } = "CubicacionMasApuComplementarios";
    public decimal MetrosCuadrados { get; set; }
    public string ScopeText { get; set; } = "";
    public string ExclusionsText { get; set; } = "";
    public int ValidityDays { get; set; } = 30;
    public bool OcultarGgYUtilidadEnResumen { get; set; } = true;

    // Markups por familia
    public decimal MarkupConductores { get; set; } = 1.50m;
    public decimal MarkupCanalizacion { get; set; } = 1.44m;
    public decimal MarkupTableros { get; set; } = 1.10m;
    public decimal MarkupProtecciones { get; set; } = 1.12m;
    public decimal MarkupIluminacion { get; set; } = 1.17m;
    public decimal MarkupEquipos { get; set; } = 1.12m;
    public decimal MarkupEnchufes { get; set; } = 1.15m;
    public decimal MarkupPuestaTierra { get; set; } = 1.20m;
    public decimal MarkupAccesorios { get; set; } = 1.14m;

    public ProjectEditSnapshot Clone() => new()
    {
        Nombre = Nombre,
        Cliente = Cliente,
        Ubicacion = Ubicacion,
        TipoProyecto = TipoProyecto,
        UsuarioNombre = UsuarioNombre,
        UsuarioRol = UsuarioRol,
        MonedaPresupuesto = MonedaPresupuesto,
        EstadoPresupuesto = EstadoPresupuesto,
        ValorUfClp = ValorUfClp,
        TipoCambioUsdClp = TipoCambioUsdClp,
        TipoCambioEurClp = TipoCambioEurClp,
        GastosGeneralesPct = GastosGeneralesPct,
        UtilidadPct = UtilidadPct,
        RiesgoPct = RiesgoPct,
        DescuentoPct = DescuentoPct,
        IvaPct = IvaPct,
        CommercialModel = CommercialModel,
        CommercialProfile = CommercialProfile,
        AllowPartidaCommercialOverrides = AllowPartidaCommercialOverrides,
        ApuCostingMode = ApuCostingMode,
        MetrosCuadrados = MetrosCuadrados,
        ScopeText = ScopeText,
        ExclusionsText = ExclusionsText,
        ValidityDays = ValidityDays,
        OcultarGgYUtilidadEnResumen = OcultarGgYUtilidadEnResumen,

        MarkupConductores = MarkupConductores,
        MarkupCanalizacion = MarkupCanalizacion,
        MarkupTableros = MarkupTableros,
        MarkupProtecciones = MarkupProtecciones,
        MarkupIluminacion = MarkupIluminacion,
        MarkupEquipos = MarkupEquipos,
        MarkupEnchufes = MarkupEnchufes,
        MarkupPuestaTierra = MarkupPuestaTierra,
        MarkupAccesorios = MarkupAccesorios
    };

    public bool ContentEquals(ProjectEditSnapshot other) =>
        other != null
        && Nombre == other.Nombre && Cliente == other.Cliente && Ubicacion == other.Ubicacion
        && TipoProyecto == other.TipoProyecto && UsuarioNombre == other.UsuarioNombre && UsuarioRol == other.UsuarioRol
        && MonedaPresupuesto == other.MonedaPresupuesto && EstadoPresupuesto == other.EstadoPresupuesto
        && ValorUfClp == other.ValorUfClp && TipoCambioUsdClp == other.TipoCambioUsdClp && TipoCambioEurClp == other.TipoCambioEurClp
        && GastosGeneralesPct == other.GastosGeneralesPct && UtilidadPct == other.UtilidadPct
        && RiesgoPct == other.RiesgoPct && DescuentoPct == other.DescuentoPct && IvaPct == other.IvaPct
        && CommercialModel == other.CommercialModel && CommercialProfile == other.CommercialProfile
        && AllowPartidaCommercialOverrides == other.AllowPartidaCommercialOverrides
        && ApuCostingMode == other.ApuCostingMode
        && MetrosCuadrados == other.MetrosCuadrados && ScopeText == other.ScopeText
        && ExclusionsText == other.ExclusionsText && ValidityDays == other.ValidityDays
        && OcultarGgYUtilidadEnResumen == other.OcultarGgYUtilidadEnResumen
        && MarkupConductores == other.MarkupConductores && MarkupCanalizacion == other.MarkupCanalizacion
        && MarkupTableros == other.MarkupTableros && MarkupProtecciones == other.MarkupProtecciones
        && MarkupIluminacion == other.MarkupIluminacion && MarkupEquipos == other.MarkupEquipos
        && MarkupEnchufes == other.MarkupEnchufes && MarkupPuestaTierra == other.MarkupPuestaTierra
        && MarkupAccesorios == other.MarkupAccesorios;
}

public interface IProjectEditWorkspaceService
{
    ProjectEditSnapshot Load();
    IReadOnlyList<string> Validate(ProjectEditSnapshot snapshot);
    void Apply(ProjectEditSnapshot snapshot);
    void Discard();
    bool HasWorkingCopy { get; }
}

public sealed class ProjectEditWorkspaceService : IProjectEditWorkspaceService
{
    private readonly BudgetProjectSession session;
    private ProjectEditSnapshot? workingCopy;

    public ProjectEditWorkspaceService(BudgetProjectSession session)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public bool HasWorkingCopy => workingCopy != null;

    public ProjectEditSnapshot Load()
    {
        workingCopy ??= FromProject(session.ProjectInfo);
        return workingCopy.Clone();
    }

    public IReadOnlyList<string> Validate(ProjectEditSnapshot snapshot)
    {
        var errors = new List<string>();
        ValidateField(errors, ProjectTextValidator.ValidateText(snapshot.Nombre, "Nombre del proyecto", "Proyecto sin nombre", 120, required: true, mustBeFileNameSafe: true));
        ValidateField(errors, ProjectTextValidator.ValidateText(snapshot.Cliente, "Cliente", "Cliente sin nombre", 120, required: true, mustBeFileNameSafe: false));
        ValidateField(errors, ProjectTextValidator.ValidateText(snapshot.Ubicacion, "Ubicación", "Sin ubicación", 200, required: false, mustBeFileNameSafe: false));
        ValidateField(errors, ProjectTextValidator.ValidateText(snapshot.TipoProyecto, "Tipo de proyecto", "Edificio habitacional", 80, required: false, mustBeFileNameSafe: false));
        ValidateField(errors, ProjectTextValidator.ValidateText(snapshot.UsuarioNombre, "Responsable", Environment.UserName, 80, required: false, mustBeFileNameSafe: false));
        ValidateField(errors, ProjectTextValidator.ValidateText(snapshot.UsuarioRol, "Rol", "Usuario", 40, required: false, mustBeFileNameSafe: false));

        if (snapshot.ValorUfClp < 0)
            errors.Add("Valor UF (CLP): no puede ser negativo.");
        if (snapshot.TipoCambioUsdClp < 0)
            errors.Add("Tipo de cambio USD: no puede ser negativo.");
        if (snapshot.TipoCambioEurClp < 0)
            errors.Add("Tipo de cambio EUR: no puede ser negativo.");
        if (snapshot.GastosGeneralesPct < 0 || snapshot.GastosGeneralesPct > 100)
            errors.Add("Gastos generales %: debe estar entre 0 y 100.");
        if (snapshot.UtilidadPct < 0 || snapshot.UtilidadPct > 100)
            errors.Add("Utilidad %: debe estar entre 0 y 100.");
        if (snapshot.RiesgoPct < 0 || snapshot.RiesgoPct > 100)
            errors.Add("Riesgo %: debe estar entre 0 y 100.");
        if (snapshot.DescuentoPct < 0 || snapshot.DescuentoPct > 100)
            errors.Add("Descuento %: debe estar entre 0 y 100.");
        if (snapshot.IvaPct < 0 || snapshot.IvaPct > 100)
            errors.Add("IVA %: debe estar entre 0 y 100.");
        if (snapshot.MetrosCuadrados < 0)
            errors.Add("Metros cuadrados: no puede ser negativo.");
        if (snapshot.ValidityDays < 1 || snapshot.ValidityDays > 365)
            errors.Add("Validez de oferta (días): debe estar entre 1 y 365.");

        // Validaciones de markups de familia
        ValidateMarkup(errors, snapshot.MarkupConductores, "Conductores");
        ValidateMarkup(errors, snapshot.MarkupCanalizacion, "Canalización");
        ValidateMarkup(errors, snapshot.MarkupTableros, "Tableros");
        ValidateMarkup(errors, snapshot.MarkupProtecciones, "Protecciones");
        ValidateMarkup(errors, snapshot.MarkupIluminacion, "Iluminación");
        ValidateMarkup(errors, snapshot.MarkupEquipos, "Equipos");
        ValidateMarkup(errors, snapshot.MarkupEnchufes, "Enchufes y tomacorrientes");
        ValidateMarkup(errors, snapshot.MarkupPuestaTierra, "Puesta a tierra");
        ValidateMarkup(errors, snapshot.MarkupAccesorios, "Accesorios");

        return errors;
    }

    private static void ValidateMarkup(List<string> errors, decimal value, string name)
    {
        if (value < 1.0m || value > 5.0m)
            errors.Add($"Markup {name}: debe ser un factor entre 1.00 y 5.00 (ej: 1.50).");
    }

    public void Apply(ProjectEditSnapshot snapshot)
    {
        session.EnsureWritable("aplicar cambios al proyecto");
        var errors = Validate(snapshot);
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));

        var project = session.ProjectInfo;
        project.Nombre = ProjectTextValidator.NormalizeText(snapshot.Nombre, "Proyecto sin nombre", 120);
        project.Cliente = ProjectTextValidator.NormalizeText(snapshot.Cliente, "Cliente sin nombre", 120);
        project.Ubicacion = ProjectTextValidator.NormalizeText(snapshot.Ubicacion, "Sin ubicación", 200);
        project.TipoProyecto = ProjectTextValidator.NormalizeText(snapshot.TipoProyecto, "Edificio habitacional", 80);
        project.UsuarioNombre = ProjectTextValidator.NormalizeText(snapshot.UsuarioNombre, Environment.UserName, 80);
        project.UsuarioRol = ProjectTextValidator.NormalizeText(snapshot.UsuarioRol, "Usuario", 40);
        project.MonedaPresupuesto = string.IsNullOrWhiteSpace(snapshot.MonedaPresupuesto) ? "CLP" : snapshot.MonedaPresupuesto.Trim().ToUpperInvariant();
        project.EstadoPresupuesto = string.IsNullOrWhiteSpace(snapshot.EstadoPresupuesto) ? "Borrador" : snapshot.EstadoPresupuesto.Trim();
        project.ValorUfClp = snapshot.ValorUfClp;
        project.TipoCambioUsdClp = snapshot.TipoCambioUsdClp;
        project.TipoCambioEurClp = snapshot.TipoCambioEurClp;

        if (project.ValorUfClp > 0)
        {
            project.HistorialTasas ??= new();
            if (!project.HistorialTasas.Any(h => h.Activa && h.UfClp == project.ValorUfClp && h.UsdClp == project.TipoCambioUsdClp && h.EurClp == project.TipoCambioEurClp))
            {
                foreach (var h in project.HistorialTasas) h.Activa = false;
                project.HistorialTasas.Add(new TasaCambioHistorialRow
                {
                    Fecha = AppClock.Now,
                    UfClp = project.ValorUfClp,
                    UsdClp = project.TipoCambioUsdClp,
                    EurClp = project.TipoCambioEurClp,
                    Activa = true
                });
                project.FechaTipoCambio = AppClock.Now;
            }

            foreach (var row in session.Cubicacion)
            {
                row.RecalcularUf(project.ValorUfClp);
            }
        }

        project.GastosGeneralesPct = snapshot.GastosGeneralesPct;
        project.UtilidadPct = snapshot.UtilidadPct;
        project.RiesgoPct = snapshot.RiesgoPct;
        project.DescuentoPct = snapshot.DescuentoPct;
        project.IvaPct = snapshot.IvaPct;
        project.CommercialModel = snapshot.CommercialModel;
        project.CommercialProfile = snapshot.CommercialProfile;
        project.AllowPartidaCommercialOverrides = snapshot.AllowPartidaCommercialOverrides;
        project.ApuCostingMode = string.IsNullOrWhiteSpace(snapshot.ApuCostingMode) ? "CubicacionMasApuComplementarios" : snapshot.ApuCostingMode;
        project.MetrosCuadrados = snapshot.MetrosCuadrados;
        project.FechaModificacion = AppClock.Now;
        project.OfertaPdf ??= new OfferPdfSettings();
        project.OfertaPdf.ScopeText = snapshot.ScopeText ?? "";
        project.OfertaPdf.ExclusionsText = snapshot.ExclusionsText ?? "";
        project.OfertaPdf.ValidityDays = snapshot.ValidityDays;
        project.OfertaPdf.OcultarGgYUtilidadEnResumen = snapshot.OcultarGgYUtilidadEnResumen;
        project.OcultarGgYUtilidadEnResumen = snapshot.OcultarGgYUtilidadEnResumen;

        // Guardar markups por familia en variables
        session.SetFamilyMarkup("Conductores", snapshot.MarkupConductores);
        session.SetFamilyMarkup("Canalización", snapshot.MarkupCanalizacion);
        session.SetFamilyMarkup("Tableros", snapshot.MarkupTableros);
        session.SetFamilyMarkup("Protecciones", snapshot.MarkupProtecciones);
        session.SetFamilyMarkup("Iluminación", snapshot.MarkupIluminacion);
        session.SetFamilyMarkup("Equipos", snapshot.MarkupEquipos);
        session.SetFamilyMarkup("Enchufes y tomacorrientes", snapshot.MarkupEnchufes);
        session.SetFamilyMarkup("Puesta a tierra", snapshot.MarkupPuestaTierra);
        session.SetFamilyMarkup("Accesorios", snapshot.MarkupAccesorios);

        workingCopy = snapshot.Clone();
        session.MarkDirty(ProjectDirtyScope.ProjectInfo);
    }

    public void Discard() => workingCopy = null;

    private static void ValidateField(List<string> errors, ProjectTextValidationResult result)
    {
        if (result.IsValid)
            return;
        errors.AddRange(result.Errors);
    }

    private ProjectEditSnapshot FromProject(ProjectInfoRow project)
    {
        project.OfertaPdf ??= new OfferPdfSettings();
        return new ProjectEditSnapshot
        {
            Nombre = project.Nombre,
            Cliente = project.Cliente,
            Ubicacion = project.Ubicacion,
            TipoProyecto = project.TipoProyecto,
            UsuarioNombre = project.UsuarioNombre,
            UsuarioRol = project.UsuarioRol,
            MonedaPresupuesto = project.MonedaPresupuesto,
            EstadoPresupuesto = project.EstadoPresupuesto,
            ValorUfClp = project.ValorUfClp,
            TipoCambioUsdClp = project.TipoCambioUsdClp,
            TipoCambioEurClp = project.TipoCambioEurClp,
            GastosGeneralesPct = project.GastosGeneralesPct,
            UtilidadPct = project.UtilidadPct,
            RiesgoPct = project.RiesgoPct,
            DescuentoPct = project.DescuentoPct,
            IvaPct = project.IvaPct,
            CommercialModel = project.CommercialModel,
            CommercialProfile = project.CommercialProfile,
            AllowPartidaCommercialOverrides = project.AllowPartidaCommercialOverrides,
            ApuCostingMode = string.IsNullOrWhiteSpace(project.ApuCostingMode) ? "CubicacionMasApuComplementarios" : project.ApuCostingMode,
            MetrosCuadrados = project.MetrosCuadrados,
            ScopeText = project.OfertaPdf.ScopeText,
            ExclusionsText = project.OfertaPdf.ExclusionsText,
            ValidityDays = project.OfertaPdf.ValidityDays,
            OcultarGgYUtilidadEnResumen = project.OfertaPdf.OcultarGgYUtilidadEnResumen,

            MarkupConductores = session.GetFamilyMarkup("Conductores"),
            MarkupCanalizacion = session.GetFamilyMarkup("Canalización"),
            MarkupTableros = session.GetFamilyMarkup("Tableros"),
            MarkupProtecciones = session.GetFamilyMarkup("Protecciones"),
            MarkupIluminacion = session.GetFamilyMarkup("Iluminación"),
            MarkupEquipos = session.GetFamilyMarkup("Equipos"),
            MarkupEnchufes = session.GetFamilyMarkup("Enchufes y tomacorrientes"),
            MarkupPuestaTierra = session.GetFamilyMarkup("Puesta a tierra"),
            MarkupAccesorios = session.GetFamilyMarkup("Accesorios")
        };
    }
}
