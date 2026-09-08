using System;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed class ArtefactoScRowVm : ViewModelBase
{
    private readonly Func<bool>? canEdit;
    private readonly Action? onChanged;

    internal ArtefactoSCRow Source { get; }

    public ArtefactoScRowVm(ArtefactoSCRow row, Func<bool>? canEdit = null, Action? onChanged = null)
    {
        Source = row;
        this.canEdit = canEdit;
        this.onChanged = onChanged;
    }

    private bool AllowEdit => canEdit?.Invoke() ?? true;

    public string Nombre
    {
        get => Source.Nombre;
        set
        {
            if (!AllowEdit) return;
            if (Source.Nombre == value) return;
            Source.Nombre = value;
            OnPropertyChanged();
            onChanged?.Invoke();
        }
    }

    public string Categoria
    {
        get => Source.Categoria;
        set
        {
            if (!AllowEdit) return;
            if (Source.Categoria == value) return;
            Source.Categoria = value;
            OnPropertyChanged();
            onChanged?.Invoke();
        }
    }

    public decimal Cantidad
    {
        get => Source.Cantidad;
        set
        {
            if (!AllowEdit) return;
            if (Source.Cantidad == value) return;
            Source.Cantidad = value;
            OnPropertyChanged();
            onChanged?.Invoke();
        }
    }
}
