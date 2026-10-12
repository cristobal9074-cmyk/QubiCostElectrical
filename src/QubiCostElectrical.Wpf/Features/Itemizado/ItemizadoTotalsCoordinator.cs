using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public sealed record ItemizadoTotalsSnapshot(
    decimal TotalBudgetUf,
    decimal TotalBudgetClp,
    decimal TotalBudgetDelivery,
    string TotalBudgetUfText,
    string TotalBudgetClpText,
    int TotalPartidasCount,
    int VisiblePartidasCount,
    decimal VisibleTotalUf,
    decimal VisibleTotalClp,
    decimal VisibleTotalDelivery,
    string FooterPartidasText,
    string FooterTotalUfText,
    string FooterTotalClpText,
    string UfRateCaption,
    bool HasActiveFilter);

/// <summary>
/// Coordina y calcula los totales generales y filtrados del presupuesto para el Itemizado Valorado
/// en UF, CLP, USD y EUR, manteniendo los cálculos desacoplados del ViewModel para respetar el presupuesto de líneas.
/// </summary>
public static class ItemizadoTotalsCoordinator
{
    public static ItemizadoTotalsSnapshot Calculate(
        IEnumerable<ItemizadoRowVm> allRows,
        IEnumerable<ItemizadoRowVm> visibleRows,
        decimal valorUfClp,
        bool isFilterActive,
        string searchText)
    {
        var ctx = ItemizadoCurrencyCoordinator.CreateContext(Moneda.UF, valorUfClp, 950m, 1050m);
        return Calculate(allRows, visibleRows, ctx, isFilterActive, searchText);
    }

    public static ItemizadoTotalsSnapshot Calculate(
        IEnumerable<ItemizadoRowVm> allRows,
        IEnumerable<ItemizadoRowVm> visibleRows,
        CurrencyPresentationContext currencyContext,
        bool isFilterActive,
        string searchText)
    {
        var allList = allRows as IList<ItemizadoRowVm> ?? allRows.ToList();
        var visibleList = visibleRows as IList<ItemizadoRowVm> ?? visibleRows.ToList();

        // 1. Total general del presupuesto (partidas no título y no excluidas)
        var validAllPartidas = allList.Where(r => !r.EsTitulo && !r.ExcluidoCubicacion).ToList();
        int totalPartidas = validAllPartidas.Count;
        decimal totalBudgetUf = validAllPartidas.Sum(r => r.TotalUf);
        decimal totalBudgetClp = totalBudgetUf * currencyContext.ValorUfClp;
        decimal totalBudgetDelivery = ItemizadoCurrencyCoordinator.ConvertClpToDelivery(totalBudgetClp, currencyContext);

        // 2. Total de partidas visibles (bajo filtro de estado o búsqueda)
        var validVisiblePartidas = visibleList.Where(r => !r.EsTitulo && !r.ExcluidoCubicacion).ToList();
        int visiblePartidas = validVisiblePartidas.Count;
        decimal visibleTotalUf = validVisiblePartidas.Sum(r => r.TotalUf);
        decimal visibleTotalClp = visibleTotalUf * currencyContext.ValorUfClp;
        decimal visibleTotalDelivery = ItemizadoCurrencyCoordinator.ConvertClpToDelivery(visibleTotalClp, currencyContext);

        bool hasFilterOrSearch = isFilterActive || !string.IsNullOrWhiteSpace(searchText);

        string footerPartidas = hasFilterOrSearch && visiblePartidas != totalPartidas
            ? $"Mostrando {visiblePartidas:N0} de {totalPartidas:N0} partidas"
            : $"{totalPartidas:N0} partidas";

        string rateCaption = ItemizadoCurrencyCoordinator.BuildRateCaption(currencyContext);

        var (totalMainText, totalSecText) = FormatCurrencyPair(totalBudgetDelivery, totalBudgetClp, currencyContext);
        var (visMainText, visSecText) = FormatCurrencyPair(visibleTotalDelivery, visibleTotalClp, currencyContext);

        return new ItemizadoTotalsSnapshot(
            TotalBudgetUf: totalBudgetUf,
            TotalBudgetClp: totalBudgetClp,
            TotalBudgetDelivery: totalBudgetDelivery,
            TotalBudgetUfText: totalMainText,
            TotalBudgetClpText: totalSecText,
            TotalPartidasCount: totalPartidas,
            VisiblePartidasCount: visiblePartidas,
            VisibleTotalUf: visibleTotalUf,
            VisibleTotalClp: visibleTotalClp,
            VisibleTotalDelivery: visibleTotalDelivery,
            FooterPartidasText: footerPartidas,
            FooterTotalUfText: visMainText,
            FooterTotalClpText: visSecText,
            UfRateCaption: rateCaption,
            HasActiveFilter: hasFilterOrSearch);
    }

    private static (string Main, string Secondary) FormatCurrencyPair(
        decimal deliveryAmount,
        decimal clpAmount,
        CurrencyPresentationContext ctx)
    {
        return ctx.Currency switch
        {
            Moneda.CLP => ($"$ {clpAmount:N0}", ""),
            Moneda.USD => ($"US$ {deliveryAmount:N2}", $"$ {clpAmount:N0}"),
            Moneda.EUR => ($"€ {deliveryAmount:N2}", $"$ {clpAmount:N0}"),
            _ => ($"{deliveryAmount:N2} UF", $"$ {clpAmount:N0}")
        };
    }
}
