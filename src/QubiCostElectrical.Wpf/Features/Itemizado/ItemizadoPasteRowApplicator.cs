using System;
using System.Collections.Generic;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

internal static class ItemizadoPasteRowApplicator
{
    public static void Apply(ItemizadoRowVm row, int columnIndex, string value, IReadOnlyList<ItemizadoRow> peers)
    {
        switch (columnIndex)
        {
            case 0:
                row.Item = value;
                row.CommitItemEdit(peers);
                break;
            case 1:
                row.Descripcion = value;
                break;
            case 2:
                row.Unidad = value;
                break;
            case 3:
                if (DataGridExcelNavigation.TryParseFlexibleDecimal(value, out var qty))
                    row.Cantidad = qty;
                break;
            case 4:
                if (ItemizadoPasteValueParser.TryParseExcluidoCubicacion(value, out var excluded))
                    row.ExcluidoCubicacion = excluded;
                break;
        }
    }
}
