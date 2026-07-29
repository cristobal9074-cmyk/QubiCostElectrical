using System;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Templates;

public sealed class ConjuntoItemVm : ViewModelBase
{
    private readonly Action onChanged;

    public ConjuntoItemVm(ConjuntoElectricoRow row, Action? onChanged = null)
    {
        Source = row;
        this.onChanged = onChanged ?? (() => {});
    }

    public ConjuntoElectricoRow Source { get; }

    public string Codigo
    {
        get => Source.Codigo;
        set
        {
            if (Source.Codigo != value)
            {
                Source.Codigo = value ?? "";
                OnPropertyChanged();
                onChanged();
            }
        }
    }

    public string Nombre
    {
        get => Source.Nombre;
        set
        {
            if (Source.Nombre != value)
            {
                Source.Nombre = value ?? "";
                OnPropertyChanged();
                onChanged();
            }
        }
    }

    public string TipoConjunto
    {
        get => Source.TipoConjunto;
        set
        {
            if (Source.TipoConjunto != value)
            {
                Source.TipoConjunto = value ?? "";
                OnPropertyChanged();
                onChanged();
            }
        }
    }

    public string Descripcion
    {
        get => Source.Descripcion;
        set
        {
            if (Source.Descripcion != value)
            {
                Source.Descripcion = value ?? "";
                OnPropertyChanged();
                onChanged();
            }
        }
    }

    public string Unidad
    {
        get => Source.Unidad;
        set
        {
            if (Source.Unidad != value)
            {
                Source.Unidad = value ?? "";
                OnPropertyChanged();
                onChanged();
            }
        }
    }

    public int RecursoCount => Source.Recursos?.Count ?? 0;
    public string ActualizadoLabel => Source.ActualizadoEn.ToString("dd/MM/yyyy");

    public void RefreshRecursoCount()
    {
        OnPropertyChanged(nameof(RecursoCount));
    }
}
