using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Purchases;

public sealed class PurchaseGroupSubtotalConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CollectionViewGroup group)
            return string.Empty;

        var items = group.Items.OfType<CompraMaterialViewModel>().ToList();
        if (items.Count == 0)
            return "$ 0";

        string param = parameter as string ?? "Subtotal";
        if (string.Equals(param, "Count", StringComparison.OrdinalIgnoreCase))
        {
            return $"{items.Count:N0} {(items.Count == 1 ? "material" : "materiales")}";
        }

        decimal totalClp = items.Sum(x => x.TotalCompraClp);
        decimal totalNetoClp = items.Sum(x => x.TotalNetoClp);
        decimal totalUf = items.Sum(x => x.TotalCompraUf);

        if (string.Equals(param, "Uf", StringComparison.OrdinalIgnoreCase))
        {
            return $"{totalUf:N2} UF";
        }

        if (string.Equals(param, "Neto", StringComparison.OrdinalIgnoreCase))
        {
            return $"$ {totalNetoClp:N0}";
        }

        // Default: formatted CLP total
        return $"$ {totalClp:N0}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
