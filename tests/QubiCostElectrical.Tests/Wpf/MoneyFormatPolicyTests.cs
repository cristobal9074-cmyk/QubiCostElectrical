using System;
using System.Globalization;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.EstadosPago;
using QubiCostElectrical.Wpf.Features.Itemizado;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Política de formato monetario del producto: los montos en UF siempre se muestran con
/// 3 decimales y los montos en CLP sin decimales. Aplica a pantallas, totales y exportaciones.
/// </summary>
public sealed class MoneyFormatPolicyTests
{
    [Fact]
    public void Uf_SiempreSeMuestraConTresDecimales()
    {
        foreach (decimal valor in new[] { 1.5m, 1_234.5678m, 0.0012m, 100m })
        {
            Assert.Equal(3, Decimales(PriceConversion.FormatDeliveryPrice(valor, Moneda.UF, isUnitPrice: false)));
            Assert.Equal(3, Decimales(PriceConversion.FormatDeliveryPrice(valor, Moneda.UF, isUnitPrice: true)));
            Assert.Equal(3, Decimales(EstadoPagoFormat.Uf(valor)));
        }
    }

    [Fact]
    public void Clp_SeMuestraSinDecimales()
    {
        foreach (decimal valor in new[] { 1_500.4m, 1_234_567.89m, 99m })
        {
            Assert.Equal(0, Decimales(PriceConversion.FormatDeliveryPrice(valor, Moneda.CLP, isUnitPrice: false)));
            Assert.Equal(0, Decimales(PriceConversion.FormatDeliveryPrice(valor, Moneda.CLP, isUnitPrice: true)));
            Assert.Equal(0, Decimales(EstadoPagoFormat.Clp(valor)));
        }
    }

    [Fact]
    public void ServicioDeConversion_AplicaLaMismaPolitica()
    {
        var conversion = new CurrencyConversionService();

        Assert.Equal("N3", conversion.GetNumberFormat(Moneda.UF));
        Assert.Equal("N0", conversion.GetNumberFormat(Moneda.CLP));
        Assert.Equal("N2", conversion.GetNumberFormat(Moneda.USD));

        const decimal uf = 38_000m;
        Assert.Equal(3, Decimales(conversion.FormatMoneyFromUf(1.5m, Moneda.UF, uf, 950m)));
        Assert.Equal(3, Decimales(conversion.FormatMoneyFromClp(1.5m * uf, Moneda.UF, uf, 950m)));
        Assert.Equal(0, Decimales(conversion.FormatMoneyFromUf(1.5m, Moneda.CLP, uf, 950m)));
        Assert.Equal(0, Decimales(conversion.FormatMoneyFromClp(1_500.4m, Moneda.CLP, uf, 950m)));
        Assert.Equal(3, Decimales(conversion.FormatMoneyFromUfCompact(1.5m, Moneda.UF, uf, 950m)));
    }

    [Fact]
    public void Itemizado_MuestraLosMontosEnUfConTresDecimales()
    {
        var ctx = ItemizadoCurrencyCoordinator.CreateContext(Moneda.UF, 38_000m, 950m, 1050m);

        string total = ItemizadoCurrencyCoordinator.FormatTotalPair(1.5m, ctx);
        string unitario = ItemizadoCurrencyCoordinator.FormatUnitPricePair(1.5m, ctx);

        Assert.Equal(3, Decimales(total));
        Assert.Equal(3, Decimales(unitario));
        Assert.Contains("UF", total);
        Assert.Contains("UF", unitario);
    }

    [Fact]
    public void PieDelItemizado_EnClp_MuestraSoloPesos()
    {
        var ctx = ItemizadoCurrencyCoordinator.CreateContext(Moneda.CLP, 38_000m, 950m, 1050m);

        string total = ItemizadoCurrencyCoordinator.FormatTotalPair(1.5m, ctx);

        Assert.Equal(0, Decimales(total));
        Assert.DoesNotContain("UF", total);
    }

    /// <summary>
    /// Cuenta los decimales del primer número del texto: los formatos de paridad agregan el
    /// equivalente en CLP entre paréntesis, así que sólo interesa el monto principal.
    /// </summary>
    private static int Decimales(string texto)
    {
        NumberFormatInfo nf = CultureInfo.CurrentCulture.NumberFormat;
        string? token = texto
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(t => t.Any(char.IsDigit));
        if (token is null)
            return 0;

        token = token.Replace(nf.NumberGroupSeparator, "");
        int i = token.LastIndexOf(nf.NumberDecimalSeparator, StringComparison.Ordinal);
        return i < 0 ? 0 : token.Length - i - nf.NumberDecimalSeparator.Length;
    }
}
