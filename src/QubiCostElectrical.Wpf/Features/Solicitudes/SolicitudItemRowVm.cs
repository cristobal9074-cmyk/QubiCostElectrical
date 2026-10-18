using System;
using System.Collections.Generic;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Solicitudes;

public sealed class SolicitudItemRowVm : ViewModelBase
{
    private readonly SolicitudesViewModel parent;
    private string codigo = "";
    private string descripcion = "";
    private string unidad = "UN";
    private decimal cantidad = 1;
    private string link = "";
    private string observaciones = "";

    public string Id { get; }
    public string Tipo { get; }

    public string Codigo
    {
        get => codigo;
        set
        {
            if (SetProperty(ref codigo, value))
            {
                if (!string.IsNullOrWhiteSpace(value) && Tipo == "Material")
                {
                    var match = parent.FindMaterialByCode(value);
                    if (match != null)
                    {
                        if (string.IsNullOrWhiteSpace(descripcion))
                            Descripcion = match.Descripcion;
                        if (string.IsNullOrWhiteSpace(unidad) || unidad == "UN")
                            Unidad = match.Unidad;
                    }
                }
                parent.SaveToSession();
            }
        }
    }

    public string Descripcion
    {
        get => descripcion;
        set
        {
            if (SetProperty(ref descripcion, value))
            {
                parent.SaveToSession();
                OnPropertyChanged(nameof(Suggestions));
                OnPropertyChanged(nameof(ShowSuggestions));
            }
        }
    }

    public string Unidad
    {
        get => unidad;
        set
        {
            if (SetProperty(ref unidad, value))
                parent.SaveToSession();
        }
    }

    public decimal Cantidad
    {
        get => cantidad;
        set
        {
            if (SetProperty(ref cantidad, value))
                parent.SaveToSession();
        }
    }

    public string Link
    {
        get => link;
        set
        {
            if (SetProperty(ref link, value))
                parent.SaveToSession();
        }
    }

    public string Observaciones
    {
        get => observaciones;
        set
        {
            if (SetProperty(ref observaciones, value))
                parent.SaveToSession();
        }
    }

    // Autocomplete dropdown lists
    public List<AutocompleteItem> Suggestions
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Descripcion) || Descripcion.Length < 2)
                return new List<AutocompleteItem>();

            if (Tipo == "Material")
            {
                return parent.SearchMaterials(Descripcion);
            }
            else
            {
                return parent.SearchEpp(Descripcion);
            }
        }
    }

    public string SolicitudCodigo { get; set; } = "SM 1";
    public string Fecha { get; set; } = "";
    public string SolicitadoPor { get; set; } = "";
    public string Estado { get; set; } = "Aprobado";

    public bool ShowSuggestions => Suggestions.Count > 0;

    public SolicitudItemRowVm(SolicitudesViewModel parent, SolicitudItemRow row)
    {
        this.parent = parent;
        Id = row.Id;
        Tipo = row.Tipo;
        codigo = row.Codigo;
        descripcion = row.Descripcion;
        unidad = row.Unidad;
        cantidad = row.Cantidad;
        link = row.Link;
        observaciones = row.Observaciones;
        SolicitudCodigo = !string.IsNullOrWhiteSpace(row.SolicitudCodigo) ? row.SolicitudCodigo : parent.CodigoSolicitud;
        Fecha = !string.IsNullOrWhiteSpace(row.Fecha) ? row.Fecha : parent.RequestDate;
        SolicitadoPor = !string.IsNullOrWhiteSpace(row.SolicitadoPor) ? row.SolicitadoPor : parent.RequestedBy;
        Estado = !string.IsNullOrWhiteSpace(row.Estado) ? row.Estado : "Aprobado";
    }

    public SolicitudItemRow ToRow()
    {
        return new SolicitudItemRow
        {
            Id = Id,
            Tipo = Tipo,
            Codigo = Codigo,
            Descripcion = Descripcion,
            Unidad = Unidad,
            Cantidad = Cantidad,
            Link = Link,
            Observaciones = Observaciones,
            SolicitudCodigo = string.IsNullOrWhiteSpace(SolicitudCodigo) ? parent.CodigoSolicitud : SolicitudCodigo,
            Fecha = string.IsNullOrWhiteSpace(Fecha) ? parent.RequestDate : Fecha,
            SolicitadoPor = string.IsNullOrWhiteSpace(SolicitadoPor) ? parent.RequestedBy : SolicitadoPor,
            Estado = string.IsNullOrWhiteSpace(Estado) ? "Aprobado" : Estado
        };
    }
}
