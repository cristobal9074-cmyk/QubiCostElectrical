using System.Globalization;
using QubiCostElectrical.Wpf.Features.EstadosPago;
using QubiCostElectrical.Wpf.Features.Versions;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Fase 4 de la política de moneda única: los formateadores de Versiones y Estados de pago
/// nunca exponen UF cuando el proyecto está en CLP.
/// </summary>
public sealed class MonedaUnicaFase4Tests
{
    private static string N0(decimal value) => value.ToString("N0", CultureInfo.CurrentCulture);
    private static string N3(decimal value) => value.ToString("N3", CultureInfo.CurrentCulture);

    // ── VersionsCurrencyFormat ────────────────────────────────────────────────
    [Fact]
    public void FromUf_ClpProject_ShowsPesosWithoutUf()
    {
        var format = new VersionsCurrencyFormat(muestraUf: false, ufRate: 38000m);

        string text = format.FromUf(1.25m);

        Assert.Equal($"$ {N0(47500m)}", text);
        Assert.DoesNotContain("UF", text);
    }

    [Fact]
    public void FromUf_UfProject_ShowsUf()
    {
        var format = new VersionsCurrencyFormat(muestraUf: true, ufRate: 38000m);

        Assert.Equal($"{N3(1.25m)} UF", format.FromUf(1.25m));
    }

    [Fact]
    public void FromUfSigned_ClpProject_KeepsSignAndHidesUf()
    {
        var format = new VersionsCurrencyFormat(muestraUf: false, ufRate: 38000m);

        Assert.Equal($"+$ {N0(47500m)}", format.FromUfSigned(1.25m));
        Assert.Equal($"-$ {N0(47500m)}", format.FromUfSigned(-1.25m));
        Assert.Equal("—", format.FromUfSigned(0m));
    }

    [Fact]
    public void FromClp_ClpProject_ShowsPesos()
    {
        var format = new VersionsCurrencyFormat(muestraUf: false, ufRate: 38000m);

        Assert.Equal($"$ {N0(1250000m)}", format.FromClp(1250000m));
        Assert.DoesNotContain("UF", format.FromClp(1250000m));
    }

    [Fact]
    public void FromClp_UfProject_ConvertsToUf()
    {
        var format = new VersionsCurrencyFormat(muestraUf: true, ufRate: 38000m);

        string expected = PriceConversion.ClpToUf(1250000m, 38000m).ToString("N3", CultureInfo.CurrentCulture);

        Assert.Equal($"{expected} UF", format.FromClp(1250000m));
    }

    [Fact]
    public void SignedFormats_KeepSignConventionInBothCurrencies()
    {
        var clp = new VersionsCurrencyFormat(muestraUf: false, ufRate: 38000m);
        var uf = new VersionsCurrencyFormat(muestraUf: true, ufRate: 38000m);

        Assert.StartsWith("+", clp.FromClpSigned(1000m));
        Assert.StartsWith("-$", clp.FromClpSigned(-1000m));
        Assert.StartsWith("+", uf.FromClpSigned(1000m));
        Assert.StartsWith("-", uf.FromClpSigned(-1000m));
        Assert.Equal("—", clp.FromClpSigned(0m));
    }

    [Fact]
    public void CurrencyFormat_WithZeroRate_FallsBackToDefaultUf()
    {
        var format = new VersionsCurrencyFormat(muestraUf: false, ufRate: 0m);

        Assert.Equal(PriceConversion.DefaultValorUfClp, format.UfRate);
    }

    // ── EstadoPagoFormat ─────────────────────────────────────────────────────
    [Fact]
    public void EstadoPagoFormat_Clp_UsesPesoSymbolAndNoUf()
    {
        string text = EstadoPagoFormat.Clp(1234567.4m);

        Assert.Equal($"$ {N0(1234567m)}", text);
        Assert.DoesNotContain("UF", text);
    }

    [Fact]
    public void EstadoPagoFormat_Uf_StillAvailableForUfProjects()
    {
        string text = EstadoPagoFormat.Uf(1.234m);

        Assert.Equal($"{(1.234m).ToString("N2", CultureInfo.CurrentCulture)} UF", text);
        Assert.EndsWith("UF", text);
    }
}
