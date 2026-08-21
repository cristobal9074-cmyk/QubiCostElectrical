using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class TemplateRulesTests
{
    // ── IsTemplateReference ───────────────────────────────────────────────────

    [Fact]
    public void IsTemplateReference_ClasePLANTILLA_RetornaTrue()
    {
        var item = new TemplateItemRow { Clase = "PLANTILLA" };
        Assert.True(TemplateRules.IsTemplateReference(item));
    }

    [Fact]
    public void IsTemplateReference_TipoTPL_RetornaTrue()
    {
        var item = new TemplateItemRow { Tipo = "TPL" };
        Assert.True(TemplateRules.IsTemplateReference(item));
    }

    [Fact]
    public void IsTemplateReference_ClaseMATERIAL_RetornaFalse()
    {
        var item = new TemplateItemRow { Clase = "MATERIAL", Tipo = "MAT" };
        Assert.False(TemplateRules.IsTemplateReference(item));
    }

    [Fact]
    public void IsTemplateReference_Null_RetornaFalse()
    {
        Assert.False(TemplateRules.IsTemplateReference(null!));
    }

    // ── TemplateReferenceName ─────────────────────────────────────────────────

    [Fact]
    public void TemplateReferenceName_SubPlantillaPresente_RetornaSubPlantilla()
    {
        var item = new TemplateItemRow { SubPlantilla = "MiPlantilla", Codigo = "COD-001" };
        Assert.Equal("MiPlantilla", TemplateRules.TemplateReferenceName(item));
    }

    [Fact]
    public void TemplateReferenceName_SubPlantillaVacia_RetornaCodigo()
    {
        var item = new TemplateItemRow { SubPlantilla = "", Codigo = "COD-001" };
        Assert.Equal("COD-001", TemplateRules.TemplateReferenceName(item));
    }

    // ── WouldCreateCycle ──────────────────────────────────────────────────────

    [Fact]
    public void WouldCreateCycle_MismoNombre_RetornaTrue()
    {
        Assert.True(TemplateRules.WouldCreateCycle("A", "A", new List<TemplateRow>()));
    }

    [Fact]
    public void WouldCreateCycle_SinRelacion_RetornaFalse()
    {
        var plantillas = new List<TemplateRow>
        {
            new() { Nombre = "B", Items = new() }
        };
        Assert.False(TemplateRules.WouldCreateCycle("A", "B", plantillas));
    }

    [Fact]
    public void WouldCreateCycle_HijoReferenciaPadre_RetornaTrue()
    {
        // B references A via subplantilla
        var itemRef = new TemplateItemRow { Clase = "PLANTILLA", SubPlantilla = "A" };
        var plantillas = new List<TemplateRow>
        {
            new() { Nombre = "B", Items = new() { itemRef } }
        };
        Assert.True(TemplateRules.WouldCreateCycle("A", "B", plantillas));
    }

    [Fact]
    public void WouldCreateCycle_CicloIndirecto_RetornaTrue()
    {
        // A→B→C→A
        var itemAB = new TemplateItemRow { Clase = "PLANTILLA", SubPlantilla = "B" };
        var itemBC = new TemplateItemRow { Clase = "PLANTILLA", SubPlantilla = "C" };
        var itemCA = new TemplateItemRow { Clase = "PLANTILLA", SubPlantilla = "A" };
        var plantillas = new List<TemplateRow>
        {
            new() { Nombre = "B", Items = new() { itemBC } },
            new() { Nombre = "C", Items = new() { itemCA } }
        };
        Assert.True(TemplateRules.WouldCreateCycle("A", "B", plantillas));
    }

    // ── UniqueTemplateName ────────────────────────────────────────────────────

    [Fact]
    public void UniqueTemplateName_NombreLibre_RetornaNombreBase()
    {
        var result = TemplateRules.UniqueTemplateName("Nueva", new List<TemplateRow>());
        Assert.Equal("Nueva", result);
    }

    [Fact]
    public void UniqueTemplateName_NombreOcupado_AgregaSufijo()
    {
        var plantillas = new List<TemplateRow> { new() { Nombre = "Nueva" } };
        var result = TemplateRules.UniqueTemplateName("Nueva", plantillas);
        Assert.Equal("Nueva 2", result);
    }

    [Fact]
    public void UniqueTemplateName_VariasOcupadas_IncrementaSufijo()
    {
        var plantillas = new List<TemplateRow>
        {
            new() { Nombre = "Nueva" },
            new() { Nombre = "Nueva 2" }
        };
        var result = TemplateRules.UniqueTemplateName("Nueva", plantillas);
        Assert.Equal("Nueva 3", result);
    }

    // ── MergeRows ─────────────────────────────────────────────────────────────

    [Fact]
    public void MergeRows_FilasDistintas_NoMerge()
    {
        var rows = new List<CubicacionRow>
        {
            new() { Codigo = "A", Tipo = "MAT", Unidad = "ML", Area = "Z1", MatUf = 1m, MoUf = 0m, Cantidad = 5m },
            new() { Codigo = "B", Tipo = "MAT", Unidad = "ML", Area = "Z1", MatUf = 2m, MoUf = 0m, Cantidad = 3m }
        };

        var result = TemplateRules.MergeRows(rows);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void MergeRows_FilasIguales_SumaCantidad()
    {
        var rows = new List<CubicacionRow>
        {
            new() { Codigo = "A", Tipo = "MAT", Unidad = "ML", Area = "Z1", MatUf = 1m, MoUf = 0m, Cantidad = 5m, ItemId = "I1", Descripcion = "Tubo" },
            new() { Codigo = "A", Tipo = "MAT", Unidad = "ML", Area = "Z1", MatUf = 1m, MoUf = 0m, Cantidad = 3m, ItemId = "I1", Descripcion = "Tubo" }
        };

        var result = TemplateRules.MergeRows(rows);

        Assert.Single(result);
        Assert.Equal(8m, result[0].Cantidad);
    }

    [Fact]
    public void MergeRows_ListaVacia_RetornaVacia()
    {
        var result = TemplateRules.MergeRows(new List<CubicacionRow>());
        Assert.Empty(result);
    }
}
