using System.Collections.Generic;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

/// <summary>
/// Antes de extraer BuildingZoneGenerator, esta lógica vivía en el code-behind de
/// AsistenteEdificioDialog.xaml.cs y no se podía testear sin instanciar un Window de WPF.
/// </summary>
public sealed class BuildingZoneGeneratorTests
{
    [Theory]
    [InlineData(-1, 1, 0)]
    [InlineData(51, 1, 0)]
    public void Validate_RejectsSubterraneosOutOfRange(int subterraneos, int torresCount, int pisosComunes)
    {
        var torres = new List<TorreZonaConfig> { new() { Nombre = "Edificio Principal", Pisos = 10, Cubiertas = 1 } };

        var error = BuildingZoneGenerator.Validate(subterraneos, torresCount, pisosComunes, torres);

        Assert.NotNull(error);
        Assert.Contains("subterráneos", error);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(51)]
    public void Validate_RejectsPisosComunesOutOfRange_OnlyWhenMultipleTorres(int pisosComunes)
    {
        var torres = new List<TorreZonaConfig> { new() { Nombre = "Torre A", Pisos = 10, Cubiertas = 1 } };

        var error = BuildingZoneGenerator.Validate(subterraneos: 0, torresCount: 2, pisosComunes, torres);

        Assert.NotNull(error);
        Assert.Contains("pisos comunes", error);
    }

    [Fact]
    public void Validate_IgnoresPisosComunesRange_WhenSingleTorre()
    {
        // Con una sola torre, el caller real nunca lee pisosComunes del formulario (queda en 0) —
        // se confirma que el chequeo de rango no dispara falso positivo en ese escenario real.
        var torres = new List<TorreZonaConfig> { new() { Nombre = "Edificio Principal", Pisos = 10, Cubiertas = 1 } };

        var error = BuildingZoneGenerator.Validate(subterraneos: 0, torresCount: 1, pisosComunes: 0, torres);

        Assert.Null(error);
    }

    [Fact]
    public void Validate_RejectsTorrePisosLowerThanPisosComunes()
    {
        var torres = new List<TorreZonaConfig> { new() { Nombre = "Torre A", Pisos = 5, Cubiertas = 1 } };

        var error = BuildingZoneGenerator.Validate(subterraneos: 0, torresCount: 2, pisosComunes: 10, torres);

        Assert.NotNull(error);
        Assert.Contains("no pueden ser menores", error);
    }

    [Fact]
    public void Validate_RejectsCubiertasOutOfRange()
    {
        var torres = new List<TorreZonaConfig> { new() { Nombre = "Torre A", Pisos = 10, Cubiertas = 11 } };

        var error = BuildingZoneGenerator.Validate(subterraneos: 0, torresCount: 1, pisosComunes: 0, torres);

        Assert.NotNull(error);
        Assert.Contains("cubiertas", error);
    }

    [Fact]
    public void GenerateZones_SingleTorre_NoSubterraneos_GeneratesPisosAndSingleCubierta()
    {
        var torres = new List<TorreZonaConfig> { new() { Nombre = "Edificio Principal", Pisos = 3, Cubiertas = 1 } };

        var zonas = BuildingZoneGenerator.GenerateZones(
            subterraneos: 0, torresCount: 1, pisosComunes: 0, subterraneoComun: false, prefijoTipo: "LETRAS", torres);

        Assert.Equal(new[] { "P1", "P2", "P3", "CUB" }, zonas);
    }

    [Fact]
    public void GenerateZones_SingleTorre_WithSubterraneos_OrdersFromDeepestToGround()
    {
        var torres = new List<TorreZonaConfig> { new() { Nombre = "Edificio Principal", Pisos = 2, Cubiertas = 0 } };

        var zonas = BuildingZoneGenerator.GenerateZones(
            subterraneos: 2, torresCount: 1, pisosComunes: 0, subterraneoComun: false, prefijoTipo: "LETRAS", torres);

        Assert.Equal(new[] { "S2", "S1", "P1", "P2" }, zonas);
    }

    [Fact]
    public void GenerateZones_MultipleTorres_LetterPrefix_SharedUndergroundAndCommonFloors()
    {
        var torres = new List<TorreZonaConfig>
        {
            new() { Nombre = "Torre A", Pisos = 5, Cubiertas = 1 },
            new() { Nombre = "Torre B", Pisos = 4, Cubiertas = 0 },
        };

        var zonas = BuildingZoneGenerator.GenerateZones(
            subterraneos: 1, torresCount: 2, pisosComunes: 2, subterraneoComun: true, prefijoTipo: "LETRAS", torres);

        Assert.Equal(new[] { "S1", "P1", "P2", "TA-P3", "TA-P4", "TA-P5", "TA-CUB", "TB-P3", "TB-P4" }, zonas);
    }

    [Fact]
    public void GenerateZones_MultipleTorres_NumericPrefix_IndependentUnderground()
    {
        var torres = new List<TorreZonaConfig>
        {
            new() { Nombre = "Torre 1", Pisos = 3, Cubiertas = 2 },
            new() { Nombre = "Torre 2", Pisos = 3, Cubiertas = 0 },
        };

        var zonas = BuildingZoneGenerator.GenerateZones(
            subterraneos: 1, torresCount: 2, pisosComunes: 0, subterraneoComun: false, prefijoTipo: "NUMEROS", torres);

        Assert.Equal(new[] { "T1-S1", "T2-S1", "T1-P1", "T1-P2", "T1-P3", "T1-CUB1", "T1-CUB2", "T2-P1", "T2-P2", "T2-P3" }, zonas);
    }
}
