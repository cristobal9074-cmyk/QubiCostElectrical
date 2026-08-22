using System.Collections.Generic;
using System.Text.Json;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class ArtefactoCatalogoItemJsonConverterTests
{
    [Fact]
    public void Deserialize_PlainStringArray_FromProjectsSavedBeforeModelChange()
    {
        // Antes de introducir ArtefactoCatalogoItem, "Artefactos" era List<string> como los demás
        // catálogos (Tipos, TiposCaja, etc.). Proyectos guardados con esa versión anterior tienen
        // este shape en su JSON — no debe romper la apertura del proyecto.
        var json = """["INT 9/12", "INT 9/15"]""";

        var items = JsonSerializer.Deserialize<List<ArtefactoCatalogoItem>>(json);

        Assert.NotNull(items);
        Assert.Equal(2, items!.Count);
        Assert.Equal("INT 9/12", items[0].Nombre);
        Assert.Equal("Otro", items[0].Categoria);
    }

    [Fact]
    public void Deserialize_ObjectArray_CurrentModel()
    {
        var json = """[{"Nombre":"Sensor PIR","Categoria":"Seguridad"}]""";

        var items = JsonSerializer.Deserialize<List<ArtefactoCatalogoItem>>(json);

        var item = Assert.Single(items!);
        Assert.Equal("Sensor PIR", item.Nombre);
        Assert.Equal("Seguridad", item.Categoria);
    }

    [Fact]
    public void RoundTrip_SerializesAsObject()
    {
        var original = new List<ArtefactoCatalogoItem> { new() { Nombre = "Foco LED", Categoria = "Iluminación" } };

        var json = JsonSerializer.Serialize(original);
        var back = JsonSerializer.Deserialize<List<ArtefactoCatalogoItem>>(json);

        Assert.Equal("Foco LED", back![0].Nombre);
        Assert.Equal("Iluminación", back[0].Categoria);
    }
}
