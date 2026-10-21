namespace QubiCostElectrical;

public enum ApuTipoRecurso
{
    MAT,
    MO,
    EQ,
    SC,
    HH,
    HERR,
    TR
}

public sealed class ApuRecursoRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ApuTipoRecurso Tipo { get; set; } = ApuTipoRecurso.MAT;
    public string CodigoMaterial { get; set; } = "";
    public string Codigo { get => CodigoMaterial; set => CodigoMaterial = value ?? ""; }
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = "";
    public decimal Rendimiento { get; set; } = 1m;
    public decimal PrecioUf { get; set; }
    public string Cuadrilla { get; set; } = "";
    public decimal RendHH { get; set; }
    public decimal ValorHHUf { get; set; }
    public decimal MermaPct { get; set; }
    public decimal ImprevistosPct { get; set; }
    public string FuentePrecio { get; set; } = "Lista de precios";
    public DateTime FechaPrecio { get; set; } = DateTime.Today;
    public string Proveedor { get; set; } = "";
    public string Estado { get; set; } = EstadoRecurso.OK;
    public string Nota { get; set; } = "";

    public decimal ConsumoUnitario => Rendimiento;
    public decimal PrecioBaseUf => (Tipo == ApuTipoRecurso.MO || Tipo == ApuTipoRecurso.HH) && ValorHHUf > 0m ? ValorHHUf : PrecioUf;
    public decimal CostoBaseUf => Rendimiento * PrecioBaseUf;
    public decimal MermaUf => CostoBaseUf * MermaPct / 100m;
    public decimal ImprevistosUf => (CostoBaseUf + MermaUf) * ImprevistosPct / 100m;
    public decimal CostoUnitUf => CostoBaseUf + MermaUf + ImprevistosUf;
    public decimal HHUnitarias => Tipo == ApuTipoRecurso.MO || Tipo == ApuTipoRecurso.HH
        ? (RendHH > 0m ? RendHH : Rendimiento)
        : 0m;
}

public sealed class ApuRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ItemId { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = "";
    public decimal CantidadPartida { get; set; } = 1m;
    public decimal Rendimiento { get; set; } = 1m;
    public string CuadrillaBase { get; set; } = "";
    public decimal HHUnitarias { get; set; }
    public decimal ValorHHUf { get; set; }
    public decimal HerramientasPct { get; set; } = 3m;
    public decimal MermaPct { get; set; } = 0m;
    public decimal TransportePct { get; set; } = 0m;
    public decimal ImprevistosAlzaPct { get; set; } = 2m;
    public string FuentePrecio { get; set; } = "APU del proyecto";
    public DateTime FechaPrecio { get; set; } = DateTime.Today;
    public string Proveedor { get; set; } = "";
    public string Estado { get; set; } = EstadoRecurso.OK;
    public string NotaRevision { get; set; } = "";
    public ApuCondicionesLaboralesRow CondicionesLaborales { get; set; } = new();
    public List<ApuRecursoRow> Recursos { get; set; } = new();

    public decimal CostoMaterialUf => Recursos.Where(r => r.Tipo == ApuTipoRecurso.MAT).Sum(r => r.CostoUnitUf);

    /// <summary>
    /// C2: base para la merma de nivel APU. La merma es UNA sola capa: si un recurso ya declara
    /// su propia <see cref="ApuRecursoRow.MermaPct"/>, la merma del APU NO se vuelve a aplicar
    /// sobre él. Así se evita la merma-sobre-merma. La merma del APU cubre solo los materiales
    /// que no traen desperdicio propio (patrón estándar: % general de desperdicio de obra).
    /// </summary>
    public decimal MermaBaseUf => Recursos
        .Where(r => r.Tipo == ApuTipoRecurso.MAT && r.MermaPct <= 0m)
        .Sum(r => r.CostoBaseUf);

    /// <summary>true si algún recurso MAT declara merma propia y además el APU tiene MermaPct.</summary>
    public bool TieneMermaEnDosNiveles =>
        MermaPct > 0m && Recursos.Any(r => r.Tipo == ApuTipoRecurso.MAT && r.MermaPct > 0m);
    public decimal CostoMoUf => Recursos.Where(r => r.Tipo == ApuTipoRecurso.MO).Sum(r => ApuLaborEngine.CostoUnitLabor(r, CondicionesLaborales));
    public decimal CostoEquipoUf => Recursos.Where(r => r.Tipo == ApuTipoRecurso.EQ).Sum(r => r.CostoUnitUf);
    public decimal CostoScUf => Recursos.Where(r => r.Tipo == ApuTipoRecurso.SC).Sum(r => ApuLaborEngine.CostoUnitLabor(r, CondicionesLaborales));
    public decimal CostoHhUf => Recursos.Where(r => r.Tipo == ApuTipoRecurso.HH).Sum(r => ApuLaborEngine.CostoUnitLabor(r, CondicionesLaborales));
    public decimal CostoHerramientasRecursoUf => Recursos.Where(r => r.Tipo == ApuTipoRecurso.HERR).Sum(r => r.CostoUnitUf);
    public decimal CostoTransporteRecursoUf => Recursos.Where(r => r.Tipo == ApuTipoRecurso.TR).Sum(r => r.CostoUnitUf);
    public decimal CostoDirectoBaseUf => CostoMaterialUf + CostoMoUf + CostoEquipoUf + CostoScUf + CostoHhUf + CostoHerramientasRecursoUf + CostoTransporteRecursoUf;
    public decimal HerramientasCalculadoUf => CostoHerramientasRecursoUf > 0m ? CostoHerramientasRecursoUf : (CostoMoUf + CostoHhUf) * HerramientasPct / 100m;
    public decimal MermaUf => MermaBaseUf * MermaPct / 100m;
    public decimal TransporteUf => CostoTransporteRecursoUf > 0m ? CostoTransporteRecursoUf : (CostoMaterialUf + CostoEquipoUf) * TransportePct / 100m;
    public decimal CostoDirectoUf => CostoDirectoBaseUf + (CostoHerramientasRecursoUf > 0m ? 0m : HerramientasCalculadoUf) + MermaUf + (CostoTransporteRecursoUf > 0m ? 0m : TransporteUf);
    public decimal ImprevistosUf => CostoDirectoUf * ImprevistosAlzaPct / 100m;
    public decimal PrecioUnitUf => CostoDirectoUf + ImprevistosUf;
    public decimal TotalPartidaUf => PrecioUnitUf * CantidadPartida;
}
