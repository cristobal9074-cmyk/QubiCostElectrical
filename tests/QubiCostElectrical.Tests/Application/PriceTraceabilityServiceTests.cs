using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

/// <summary>
/// PriceTraceabilityService no tenía tests propios. Antes, "precio vencido" usaba un umbral
/// fijo de 6 meses (~183 días) hardcodeado por separado del que usa
/// Features/Cubicacion/CubicacionRightPanelVm.cs (180 días) y ProactiveAlertDefaults.cs (config
/// del proyecto) — ahora los tres pasan por ProjectInfoRow.IsPrecioVencido.
/// </summary>
public sealed class PriceTraceabilityServiceTests
{
    private static readonly IPriceTraceabilityService Service = new PriceTraceabilityService();

    [Fact]
    public void Build_MarksMaterial_AsPrecioVencido_UsingProjectConfiguredThreshold()
    {
        var proyecto = new ProjectInfoRow { PriceExpiryCriticalDays = 30 };
        var now = AppClock.Now.Date;

        var materiales = new List<MaterialRow>
        {
            new() { Codigo = "MAT-1", MatUf = 1m, MoUf = 0m, FechaPrecio = now.AddDays(-61) } // vencido con umbral 30
        };
        var cubicacion = new List<CubicacionRow>
        {
            new() { Codigo = "MAT-1", MatUf = 1m, MoUf = 0m }
        };

        var rows = Service.Build(materiales, cubicacion, apus: Array.Empty<ApuRow>(), proyecto);

        var row = Assert.Single(rows);
        Assert.Equal(PriceTraceStatus.PrecioVencido, row.Estado);
    }

    [Fact]
    public void Build_DoesNotMarkMaterial_AsPrecioVencido_WhenWithinConfiguredThreshold()
    {
        var proyecto = new ProjectInfoRow { PriceExpiryCriticalDays = 90 };
        var now = AppClock.Now.Date;

        var materiales = new List<MaterialRow>
        {
            new() { Codigo = "MAT-1", MatUf = 1m, MoUf = 0m, FechaPrecio = now.AddDays(-61) } // no vencido con umbral 90
        };
        var cubicacion = new List<CubicacionRow>
        {
            new() { Codigo = "MAT-1", MatUf = 1m, MoUf = 0m }
        };

        var rows = Service.Build(materiales, cubicacion, apus: Array.Empty<ApuRow>(), proyecto);

        var row = Assert.Single(rows);
        Assert.Equal(PriceTraceStatus.OK, row.Estado);
    }

    [Fact]
    public void Build_MarksApuResource_AsPrecioVencido_UsingProjectConfiguredThreshold()
    {
        var proyecto = new ProjectInfoRow { PriceExpiryCriticalDays = 30 };
        var now = AppClock.Now.Date;

        var apus = new List<ApuRow>
        {
            new()
            {
                ItemId = "1.1",
                Recursos = new List<ApuRecursoRow>
                {
                    new()
                    {
                        Tipo = ApuTipoRecurso.MAT,
                        CodigoMaterial = "MAT-2",
                        PrecioUf = 5m,
                        Rendimiento = 1m,
                        Proveedor = "Proveedor X",
                        FechaPrecio = now.AddDays(-61) // vencido con umbral 30
                    }
                }
            }
        };

        var rows = Service.Build(materiales: Array.Empty<MaterialRow>(), cubicacion: Array.Empty<CubicacionRow>(), apus, proyecto);

        var row = Assert.Single(rows);
        Assert.Equal(PriceTraceStatus.PrecioVencido, row.Estado);
    }

    [Fact]
    public void Build_FlagsFaltaPrecio_WhenPriceIsZero()
    {
        var cubicacion = new List<CubicacionRow> { new() { Codigo = "SIN-PRECIO", MatUf = 0m, MoUf = 0m } };

        var rows = Service.Build(Array.Empty<MaterialRow>(), cubicacion, Array.Empty<ApuRow>());

        var row = Assert.Single(rows);
        Assert.Equal(PriceTraceStatus.FaltaPrecio, row.Estado);
    }

    [Fact]
    public void Build_FlagsImportadoSinValidar_WhenCodeUsedInCubicacionHasNoMaterialMatch()
    {
        var cubicacion = new List<CubicacionRow> { new() { Codigo = "NO-EXISTE", MatUf = 2m, MoUf = 0m } };

        var rows = Service.Build(Array.Empty<MaterialRow>(), cubicacion, Array.Empty<ApuRow>());

        var row = Assert.Single(rows);
        Assert.Equal(PriceTraceStatus.ImportadoSinValidar, row.Estado);
    }

    [Fact]
    public void Build_UsesDefaultProject_WhenNoneProvided()
    {
        // proyecto es opcional (?? new ProjectInfoRow()) — no debe lanzar si se omite.
        var cubicacion = new List<CubicacionRow> { new() { Codigo = "MAT-1", MatUf = 1m, MoUf = 0m } };

        var rows = Service.Build(Array.Empty<MaterialRow>(), cubicacion, Array.Empty<ApuRow>());

        Assert.Single(rows);
    }
}
