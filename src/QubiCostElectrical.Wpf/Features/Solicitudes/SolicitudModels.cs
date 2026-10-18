using System;
using System.Collections.ObjectModel;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Solicitudes;

public sealed class HistorialSolicitudVm : ViewModelBase
{
    public string Codigo { get; set; } = "";
    public string Fecha { get; set; } = "";
    public string SolicitadoPor { get; set; } = "";
    public string Estado { get; set; } = "Aprobado";
    public int ItemsCount { get; set; }
    public decimal TotalUnidades { get; set; }
    public ObservableCollection<SolicitudItemRowVm> Items { get; } = new();

    public string ItemsSummaryText => $"{ItemsCount} ítems ({TotalUnidades:N0} u.)";
    public bool EsAprobado => string.Equals(Estado, "Aprobado", StringComparison.OrdinalIgnoreCase);
}

public sealed class AutocompleteItem
{
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = "";
}

public sealed class SolicitudAutocompleteSuggestionVm
{
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = "UN";
    public string Categoria { get; set; } = "";
    public decimal CantidadCubicada { get; set; }
    public decimal CantidadSolicitada { get; set; }
    public decimal CantidadDisponible => CantidadCubicada - CantidadSolicitada;

    public bool IsDisponible => CantidadDisponible > 0;
    public bool IsAgotado => CantidadCubicada > 0 && CantidadDisponible <= 0;

    public string CantidadCubicadaDisplay => CantidadCubicada > 0 ? $"{CantidadCubicada:N0} {Unidad}" : "—";
    public string CantidadDisponibleDisplay => CantidadCubicada > 0 ? $"{CantidadDisponible:N0} {Unidad}" : "Banco";
}
