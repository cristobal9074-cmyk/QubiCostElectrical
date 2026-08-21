using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class MaterialImportServiceTests
{
    // ── NextNewMaterialCode ───────────────────────────────────────────────────

    [Fact]
    public void NextNewMaterialCode_ListaVacia_RetornaNUEVO001()
    {
        var code = MaterialImportService.NextNewMaterialCode(new List<MaterialRow>());
        Assert.Equal("NUEVO-001", code);
    }

    [Fact]
    public void NextNewMaterialCode_NUEVO001Existe_RetornaNUEVO002()
    {
        var mats = new List<MaterialRow> { new() { Codigo = "NUEVO-001" } };
        var code = MaterialImportService.NextNewMaterialCode(mats);
        Assert.Equal("NUEVO-002", code);
    }

    [Fact]
    public void NextNewMaterialCode_EsInsensibleAMayusculas()
    {
        var mats = new List<MaterialRow> { new() { Codigo = "nuevo-001" } };
        var code = MaterialImportService.NextNewMaterialCode(mats);
        Assert.Equal("NUEVO-002", code);
    }

    // ── MergeSpreadsheetRows ──────────────────────────────────────────────────

    [Fact]
    public void MergeSpreadsheetRows_MaterialNuevo_SeAgrega()
    {
        var rows = new List<MaterialSpreadsheetRow>
        {
            new("TUB-001", "Tubería PVC 20mm", "ML", 1m, 0.5m)
        };
        var mats = new List<MaterialRow>();

        var (imported, updated) = MaterialImportService.MergeSpreadsheetRows(rows, mats, v => v);

        Assert.Equal(1, imported);
        Assert.Equal(0, updated);
        Assert.Single(mats);
        Assert.Equal("TUB-001", mats[0].Codigo);
        Assert.Equal("Tubería PVC 20mm", mats[0].Descripcion);
    }

    [Fact]
    public void MergeSpreadsheetRows_MaterialExistente_SeActualiza()
    {
        var rows = new List<MaterialSpreadsheetRow>
        {
            new("TUB-001", "Tubería PVC 20mm ACTUALIZADA", "ML", 2m, 1m)
        };
        var mats = new List<MaterialRow>
        {
            new() { Codigo = "TUB-001", Descripcion = "Tubería PVC 20mm", MatUf = 1m, MoUf = 0.5m }
        };

        var (imported, updated) = MaterialImportService.MergeSpreadsheetRows(rows, mats, v => v);

        Assert.Equal(0, imported);
        Assert.Equal(1, updated);
        Assert.Single(mats);
        Assert.Equal("Tubería PVC 20mm ACTUALIZADA", mats[0].Descripcion);
        Assert.Equal(2m, mats[0].MatUf);
    }

    [Fact]
    public void MergeSpreadsheetRows_AplicaConversionAPrecios()
    {
        var rows = new List<MaterialSpreadsheetRow> { new("TUB-001", "Tubería", "ML", 36000m, 18000m) };
        var mats = new List<MaterialRow>();

        MaterialImportService.MergeSpreadsheetRows(rows, mats, v => v / 36000m);

        Assert.Equal(1m, mats[0].MatUf);
        Assert.Equal(0.5m, mats[0].MoUf);
    }

    // ── MergeClipboardText ────────────────────────────────────────────────────

    [Fact]
    public void MergeClipboardText_TextoVacio_RetornaCero()
    {
        int count = MaterialImportService.MergeClipboardText(
            "", new List<MaterialRow>(), () => "NUEVO-001", v => v);
        Assert.Equal(0, count);
    }

    [Fact]
    public void MergeClipboardText_LineaNueva_SeAgrega()
    {
        var mats = new List<MaterialRow>();
        string text = "TUB-001\tTubería PVC 20mm\tML";

        int count = MaterialImportService.MergeClipboardText(
            text, mats, () => "NUEVO-001", v => v);

        Assert.Equal(1, count);
        Assert.Single(mats);
        Assert.Equal("TUB-001", mats[0].Codigo);
    }

    [Fact]
    public void MergeClipboardText_LineaExistente_SeActualiza()
    {
        var mats = new List<MaterialRow>
        {
            new() { Codigo = "TUB-001", Descripcion = "Viejo" }
        };
        string text = "TUB-001\tNuevo";

        MaterialImportService.MergeClipboardText(text, mats, () => "NUEVO-001", v => v);

        Assert.Single(mats);
        Assert.Equal("Nuevo", mats[0].Descripcion);
    }

    [Fact]
    public void MergeClipboardText_CodigoVacioUsaNextCode()
    {
        var mats = new List<MaterialRow>();
        string text = "\tDescripcion sin código";

        MaterialImportService.MergeClipboardText(text, mats, () => "NUEVO-001", v => v);

        Assert.Single(mats);
        Assert.Equal("NUEVO-001", mats[0].Codigo);
    }

    [Fact]
    public void MergeClipboardText_LineasConSoloUnaCelda_SeIgnoran()
    {
        var mats = new List<MaterialRow>();
        string text = "solo-una-celda";

        int count = MaterialImportService.MergeClipboardText(
            text, mats, () => "NUEVO-001", v => v);

        Assert.Equal(0, count);
        Assert.Empty(mats);
    }
}
