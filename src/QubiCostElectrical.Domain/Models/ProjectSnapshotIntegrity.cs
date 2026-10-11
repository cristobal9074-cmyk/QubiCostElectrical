namespace QubiCostElectrical;

public static class ProjectSnapshotIntegrity
{
    public static void Ensure(ProjectFileData value)
    {
        value.Proyecto ??= new ProjectInfoRow();
        value.Itemizado ??= new List<ItemizadoRow>();
        value.Materiales ??= new List<MaterialRow>();
        value.Cubicacion ??= new List<CubicacionRow>();
        value.Plantillas ??= new List<TemplateRow>();
        value.ConjuntosElectricos ??= new List<ConjuntoElectricoRow>();
        value.CubicacionRecursos ??= new List<CubicacionRecursoRow>();
        value.CubicacionZonaCantidades ??= new List<CubicacionZonaCantidadRow>();
        value.CubicacionValorizaciones ??= new List<CubicacionValorizacionRow>();
        value.PartidaApuVinculos ??= new List<PartidaApuVinculoRow>();
        value.QtyHeadersByItem ??= new Dictionary<string, string[]>();
        value.Zonas ??= new List<ZonaProyecto>();
        value.Mediciones ??= new List<MeasurementLineRow>();
        value.Versiones ??= new List<BudgetVersionRow>();
        value.Apus ??= new List<ApuRow>();
        value.OrdenesCambio ??= new List<ChangeOrderRow>();
        value.CubicacionSC ??= new List<CuadroCargaSCRow>();
        value.CubicacionScConfig ??= CubicacionScConfig.Default();
        EnsureScConfigSistemas(value.CubicacionScConfig);
        value.HojaCubicacion ??= new List<HojaCubicacionItem>();
        value.Solicitudes ??= new List<SolicitudItemRow>();
        value.MoDirecta ??= new List<MoDirectaRow>();
        value.MoIndirecta ??= new List<MoIndirectaRow>();
        value.GastosOperacionales ??= new List<GastoOperacionalRow>();
        value.Epp ??= EppBudgetData.Default();
        value.Epp.MoDirecta ??= new List<EppSeccionRow>();
        value.Epp.MoIndirecta ??= new List<EppSeccionRow>();
        value.Epp.AccesoriosOpcionales ??= new List<EppSeccionRow>();
        value.EstadoPagoMeses ??= new List<EstadoPagoMesRow>();
        value.EstadoPagoConfig ??= EstadoPagoConfig.Default();
        value.ActiveVersionId ??= "";

        EnsureProjectInfo(value.Proyecto);
        EnsureApus(value.Apus);
        EnsureConjuntos(value.ConjuntosElectricos);
        EnsureCubicacionSC(value.CubicacionSC);
        EnsureMaterialesClp(value.Materiales, value.Proyecto.ValorUfClp);
        EnsureCubicacionClp(value);
    }

    private static void EnsureCubicacionClp(ProjectFileData data)
    {
        decimal uf = data.Proyecto.ValorUfClp > 0m ? data.Proyecto.ValorUfClp : 39000m;

        foreach (var v in data.CubicacionValorizaciones)
        {
            // Sembrar CLP por campo (MAT y MO independientes). Nunca MoUf=1 → 1×ValorUF.
            if (v.MatClp == 0m && v.MatUf > 0m)
                v.MatClp = PriceConversion.LegacyUfFieldToClp(v.MatUf, uf, isMo: false);
            if (v.MoClp == 0m && v.MoUf > 0m)
                v.MoClp = PriceConversion.LegacyUfFieldToClp(v.MoUf, uf, isMo: true);

            if ((v.MatClp > 0m || v.MoClp > 0m) && uf > 0m)
            {
                v.MatUf = PriceConversion.ClpToUf(v.MatClp, uf);
                v.MoUf = PriceConversion.ClpToUf(v.MoClp, uf);
            }
        }

        foreach (var r in data.Cubicacion)
        {
            if (r.MoClp == 0m && r.MoUf > 15m)
            {
                r.MoClp = r.MoUf;
                r.RecalcularUf(uf);
            }

            if (r.MatClp == 0m && r.MatUf > 0m)
                r.MatClp = PriceConversion.LegacyUfFieldToClp(r.MatUf, uf, isMo: false);
            if (r.MoClp == 0m && r.MoUf > 0m)
                r.MoClp = PriceConversion.LegacyUfFieldToClp(r.MoUf, uf, isMo: true);

            if (r.MatClp > 0m || r.MoClp > 0m)
                r.RecalcularUf(uf);
        }

        // Migración C3 (motor v3): antes, cualquier GgUtFactor != 1 en una fila de cubicación
        // activaba la venta línea a línea por factores de presentación. Ahora esa ruta es un
        // opt-in explícito (UsarFactoresPresentacionFamilia). Para no cambiar el total de
        // proyectos existentes al reabrirlos, si ya traen factores por familia se marca el flag.
        if (!data.Proyecto.UsarFactoresPresentacionFamilia
            && !data.Proyecto.PoliticaCostosExcel
            && data.Cubicacion.Any(r => r.GgUtFactor != 1m))
        {
            data.Proyecto.UsarFactoresPresentacionFamilia = true;
        }
    }

    // Migra la lista de precios de proyectos anteriores al modelo "CLP es la fuente": si un
    // material ya tenía MatUf/MoUf cargado (en UF, como funcionaba antes) pero todavía no tiene
    // MatClp/MoClp, se siembra MatClp = MatUf × ValorUfClp para no perder el precio ya cargado.
    // Solo corre una vez por material (mientras MatClp/MoClp sigan en 0); de ahí en adelante
    // MatClp/MoClp son la fuente y MatUf/MoUf se recalculan desde ellos (ver
    // MaterialRow.RecalcularUf), así que esta siembra no vuelve a pisar ediciones del usuario.
    private static void EnsureMaterialesClp(IEnumerable<MaterialRow> materiales, decimal valorUfClp)
    {
        decimal uf = valorUfClp > 0m ? valorUfClp : 39000m;
        foreach (var m in materiales)
        {
            if (m.MoClp == 0m && m.MoUf > 15m)
            {
                m.MoClp = m.MoUf;
                m.RecalcularUf(uf);
            }

            if (m.MatClp == 0m && m.MatUf > 0m)
                m.MatClp = PriceConversion.LegacyUfFieldToClp(m.MatUf, uf, isMo: false);
            if (m.MoClp == 0m && m.MoUf > 0m)
                m.MoClp = PriceConversion.LegacyUfFieldToClp(m.MoUf, uf, isMo: true);

            if (m.MatClp > 0m || m.MoClp > 0m)
                m.RecalcularUf(uf);

            if (m.PrecioCongelado != null)
                EnsurePrecioCongeladoClp(m.PrecioCongelado, uf);
        }
    }

    private static void EnsurePrecioCongeladoClp(PrecioCongeladoRow snapshot, decimal valorUfClp)
    {
        if (snapshot.MatClp == 0m && snapshot.MoClp == 0m && (snapshot.MatUf > 0m || snapshot.MoUf > 0m))
        {
            snapshot.MatClp = snapshot.ResolveMatClp();
            snapshot.MoClp = snapshot.ResolveMoClp();
            if (snapshot.MatClp == 0m && snapshot.MatUf > 0m && valorUfClp > 0m)
                snapshot.MatClp = PriceConversion.UfToClp(snapshot.MatUf, valorUfClp);
            if (snapshot.MoClp == 0m && snapshot.MoUf > 0m && valorUfClp > 0m)
                snapshot.MoClp = PriceConversion.UfToClp(snapshot.MoUf, valorUfClp);
        }

        if ((snapshot.MatClp > 0m || snapshot.MoClp > 0m) && valorUfClp > 0m)
        {
            snapshot.MatUf = PriceConversion.ClpToUf(snapshot.MatClp, valorUfClp);
            snapshot.MoUf = PriceConversion.ClpToUf(snapshot.MoClp, valorUfClp);
            snapshot.MonedaPrecio = Moneda.CLP;
            if (snapshot.UfUsadaClp <= 0m)
                snapshot.UfUsadaClp = valorUfClp;
        }
    }

    // Sana el catálogo de fases/alim: garantiza que existan las fases estándar (M/T/EM) con su
    // N° de conductores y merma. Proyectos guardados con un catálogo viejo o incompleto (p. ej.
    // con las fases con nombre largo "Monofásico"/"Trifásico", que ApplySistema ya no reconoce)
    // dejaban de multiplicar por el alim y caían al valor por defecto (3 conductores). Solo AGREGA
    // las fases faltantes; no toca las que el usuario ya configuró.
    private static void EnsureScConfigSistemas(CubicacionScConfig config)
    {
        config.Sistemas ??= new List<SistemaSC>();
        foreach (var def in CubicacionScConfig.Default().Sistemas)
        {
            if (!config.Sistemas.Any(s => string.Equals(s.Nombre, def.Nombre, StringComparison.OrdinalIgnoreCase)))
                config.Sistemas.Add(new SistemaSC { Nombre = def.Nombre, NumConductores = def.NumConductores, MermaPct = def.MermaPct });
        }

        config.SistemasCircuito ??= new List<SistemaCircuitoSC>();
        if (config.SistemasCircuito.Count == 0)
        {
            foreach (var def in CubicacionScConfig.Default().SistemasCircuito)
            {
                config.SistemasCircuito.Add(new SistemaCircuitoSC { Codigo = def.Codigo, Nombre = def.Nombre });
            }
        }
    }

    // Migra circuitos SC de proyectos anteriores a los dos tramos independientes: si tienen el
    // largo único legacy (LargoCircuito) pero aún no los metrajes por tramo, se siembra el tramo
    // que corresponda según cómo se canalizaba el circuito. LargoCircuito se conserva (el
    // ViewModel lo mantiene = suma de ambos tramos), así que la siembra ocurre una sola vez.
    private static void EnsureCubicacionSC(IEnumerable<CuadroCargaSCRow> cuadros)
    {
        foreach (var cuadro in cuadros)
        {
            cuadro.Circuitos ??= new List<CircuitoSCRow>();
            foreach (var c in cuadro.Circuitos)
            {
                c.Artefactos ??= new List<ArtefactoSCRow>();
                c.PisosTramos ??= new List<CircuitoPisoTramo>();
                if (c.MetrosBandeja == 0m && c.MetrosTuberia == 0m && c.LargoCircuito > 0m)
                {
                    if (c.EsPorBandeja) c.MetrosBandeja = c.LargoCircuito;
                    else c.MetrosTuberia = c.LargoCircuito;
                }
            }
        }
    }

    private static void EnsureProjectInfo(ProjectInfoRow project)
    {
        if (project.ValorUfClp <= 0m)
            project.ValorUfClp = PriceConversion.DefaultValorUfClp;

        project.AntecedentesTecnicos ??= new List<AntecedenteTecnicoRow>();
        project.LaborDaySettings ??= new LaborDaySettingsRow();
        project.Variables ??= new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        project.FamiliasPrecioAsignadas ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        project.OfertaPdf ??= new OfferPdfSettings();
        project.HistorialEstados ??= new List<BudgetStatusHistoryRow>();
        project.CubicacionCellComments ??= new List<CubicacionCellCommentRow>();
        project.TechnicalValidationRules ??= new List<TechnicalValidationRuleRow>();
        project.RealCosts ??= new List<RealCostRow>();
        project.PartidaWorkflow ??= new List<PartidaWorkflowRow>();
        project.PartidaTimeline ??= new List<PartidaChangeTimelineRow>();
        project.AuditLog ??= new List<ProjectAuditEntryRow>();
        project.ProactiveAlertRules ??= new List<ProactiveAlertRuleRow>();
    }

    private static void EnsureApus(IEnumerable<ApuRow> apus)
    {
        foreach (var apu in apus)
        {
            apu.CondicionesLaborales ??= new ApuCondicionesLaboralesRow();
            apu.Recursos ??= new List<ApuRecursoRow>();
        }
    }

    private static void EnsureConjuntos(IEnumerable<ConjuntoElectricoRow> conjuntos)
    {
        foreach (var conjunto in conjuntos)
        {
            conjunto.Recursos ??= new List<ConjuntoRecursoRow>();
            // Ids unicos y no vacios: las referencias MaestroId (esclavo -> maestro) dependen de ellos.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var recurso in conjunto.Recursos)
            {
                recurso.MaestroId ??= "";
                if (string.IsNullOrWhiteSpace(recurso.Id) || !seen.Add(recurso.Id))
                {
                    recurso.Id = Guid.NewGuid().ToString("N");
                    seen.Add(recurso.Id);
                }
            }
        }
    }
}
