using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class MaterialControlServiceTests
{
    private readonly MaterialControlService _service = new();

    private static CubicacionRow Cub(string codigo, decimal cantidad, decimal matUf = 1m, string tipo = "MAT")
        => new() { Codigo = codigo, Cantidad = cantidad, MatUf = matUf, Tipo = tipo, Descripcion = codigo };

    private static MaterialRow Mat(string codigo, string desc, decimal matUf = 1m)
        => new() { Codigo = codigo, Descripcion = desc, Unidad = "UN", MatUf = matUf, Categoria = "Cables" };

    private static SolicitudItemRow Sol(string codigo, decimal cantidad, string correlativo = "SM 1", string solicitante = "Jefe Obra")
        => new()
        {
            Codigo = codigo,
            Descripcion = codigo,
            Cantidad = cantidad,
            Tipo = "Material",
            SolicitudCodigo = correlativo,
            Fecha = "14/10/2026",
            SolicitadoPor = solicitante
        };

    [Fact]
    public void Build_WithoutSolicitudes_ShowsFullPending()
    {
        var cub = new[] { Cub("PVC20", 100m) };
        var mats = new[] { Mat("PVC20", "PVC 20mm") };
        var solicitudes = Enumerable.Empty<SolicitudItemRow>();

        var rows = _service.Build(cub, mats, solicitudes, toleranciaPct: 0m);

        Assert.Single(rows);
        var row = rows[0];
        Assert.Equal("PVC20", row.Codigo);
        Assert.Equal(100m, row.CantidadCubicada);
        Assert.Equal(0m, row.CantidadSolicitada);
        Assert.Equal(100m, row.CantidadPendiente);
        Assert.Equal(0m, row.CantidadExceso);
        Assert.False(row.TieneSobrepedido);
        Assert.True(row.SinPedir);
        Assert.Equal("Sin pedir", row.EstadoControl);
    }

    [Fact]
    public void Build_PartialRequest_DiscountsFromPending()
    {
        var cub = new[] { Cub("PVC20", 100m) };
        var mats = new[] { Mat("PVC20", "PVC 20mm") };
        var solicitudes = new[] { Sol("PVC20", 40m, "SM 1") };

        var rows = _service.Build(cub, mats, solicitudes, toleranciaPct: 0m);

        Assert.Single(rows);
        var row = rows[0];
        Assert.Equal(100m, row.CantidadCubicada);
        Assert.Equal(40m, row.CantidadSolicitada);
        Assert.Equal(60m, row.CantidadPendiente);
        Assert.Equal(0m, row.CantidadExceso);
        Assert.False(row.TieneSobrepedido);
        Assert.True(row.EstaEnProceso);
        Assert.Equal("En proceso", row.EstadoControl);
        Assert.Single(row.Solicitudes);
        Assert.False(row.Solicitudes[0].ProvocoExceso);
    }

    [Fact]
    public void Build_OverRequest_TriggersSobrepedidoAlertAndIdentifiesOffendingSolicitud()
    {
        var cub = new[] { Cub("PVC20", 100m) };
        var mats = new[] { Mat("PVC20", "PVC 20mm") };
        var solicitudes = new[]
        {
            Sol("PVC20", 70m, "SM 1"),
            Sol("PVC20", 50m, "SM 2") // Este supera los 100m
        };

        var rows = _service.Build(cub, mats, solicitudes, toleranciaPct: 0m);

        Assert.Single(rows);
        var row = rows[0];
        Assert.Equal(100m, row.CantidadCubicada);
        Assert.Equal(120m, row.CantidadSolicitada);
        Assert.Equal(0m, row.CantidadPendiente);
        Assert.Equal(20m, row.CantidadExceso);
        Assert.True(row.TieneSobrepedido);
        Assert.Contains("Sobrepedido", row.EstadoControl);

        Assert.Equal(2, row.Solicitudes.Count);
        Assert.False(row.Solicitudes[0].ProvocoExceso);
        Assert.True(row.Solicitudes[1].ProvocoExceso); // SM 2 caused the excess
    }

    [Fact]
    public void Build_UnbudgetedMaterialInSolicitud_AddedWithZeroBudgetAndAlerted()
    {
        var cub = new[] { Cub("PVC20", 100m) };
        var mats = new[] { Mat("PVC20", "PVC 20mm") };
        var solicitudes = new[]
        {
            Sol("EXTRA99", 15m, "SM 1")
        };

        var rows = _service.Build(cub, mats, solicitudes, toleranciaPct: 0m);

        Assert.Equal(2, rows.Count);
        var extra = rows.First(r => r.Codigo == "EXTRA99");
        Assert.Equal(0m, extra.CantidadCubicada);
        Assert.Equal(15m, extra.CantidadSolicitada);
        Assert.Equal(15m, extra.CantidadExceso);
        Assert.True(extra.TieneSobrepedido);
        Assert.Contains("No presupuestado", extra.EstadoControl);
        Assert.True(extra.Solicitudes[0].ProvocoExceso);
    }

    [Fact]
    public void GetSummary_CalculatesTotalsAndAlertCounts()
    {
        var cub = new[]
        {
            Cub("PVC20", 100m),
            Cub("THHN10", 50m)
        };
        var mats = new[]
        {
            Mat("PVC20", "PVC 20mm"),
            Mat("THHN10", "THHN 10")
        };
        var solicitudes = new[]
        {
            Sol("PVC20", 120m), // Sobrepedido (+20)
            Sol("THHN10", 50m)  // Completo
        };

        var rows = _service.Build(cub, mats, solicitudes, toleranciaPct: 0m);
        var summary = _service.GetSummary(rows);

        Assert.Equal(150m, summary.TotalCubicado);
        Assert.Equal(170m, summary.TotalSolicitado);
        Assert.Equal(0m, summary.TotalPendiente);
        Assert.Equal(20m, summary.TotalExceso);
        Assert.Equal(1, summary.CantidadSobrepedidos);
        Assert.Equal(1, summary.CantidadCompletos);
    }
}
