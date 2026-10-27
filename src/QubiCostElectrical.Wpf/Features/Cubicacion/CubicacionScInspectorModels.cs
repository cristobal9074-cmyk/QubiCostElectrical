using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Representa un filtro de sistema / tipo de circuito en el Inspector SC (ej: Todos, Luminarias, Enchufes, Clima, etc.).
/// </summary>
public sealed class ScInspectorSystemVm : INotifyPropertyChanged
{
    private bool _isSelected;
    private int _count;

    public string Id { get; init; } = "ALL";
    public string Label { get; init; } = "Todos";
    public string Icon { get; init; } = "🌐";

    public int Count
    {
        get => _count;
        set { if (_count != value) { _count = value; OnPropertyChanged(); } }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? prop = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}

/// <summary>
/// Representa un material individual calculado de Servicios Comunes listo para inyectar en la partida.
/// </summary>
public sealed class ScInspectorMaterialVm : INotifyPropertyChanged
{
    private bool _isTransferred;
    private bool _isVisible = true;
    private bool _isSelected;

    public string Codigo { get; init; } = "";
    public string Descripcion { get; init; } = "";
    public string Unidad { get; init; } = "ml";
    public decimal Cantidad { get; init; }
    public string Categoria { get; init; } = "General";
    public string SystemCode { get; init; } = "ALL";
    public string SystemBadge { get; init; } = "";
    public IReadOnlyList<(string ZonaNombre, decimal Cantidad)> Zonas { get; init; } = Array.Empty<(string, decimal)>();

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    public bool IsTransferred
    {
        get => _isTransferred;
        set { if (_isTransferred != value) { _isTransferred = value; OnPropertyChanged(); } }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set { if (_isVisible != value) { _isVisible = value; OnPropertyChanged(); } }
    }

    public string CantidadDisplay => Cantidad.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-CL"));

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? prop = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}

/// <summary>
/// Representa un grupo jerárquico de materiales (Conductores, Canalizaciones, Accesorios, Protecciones).
/// </summary>
public sealed class ScInspectorGroupVm : INotifyPropertyChanged
{
    private bool _isExpanded = true;

    public string GroupId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Icon { get; init; } = "";
    public ObservableCollection<ScInspectorMaterialVm> Items { get; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set { if (_isExpanded != value) { _isExpanded = value; OnPropertyChanged(); } }
    }

    public int VisibleCount => Items.Count(i => i.IsVisible);
    public bool HasVisibleItems => VisibleCount > 0;

    public ICommand ToggleExpandCommand { get; }

    public ScInspectorGroupVm()
    {
        ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
    }

    public void NotifyVisibilityChanged()
    {
        OnPropertyChanged(nameof(VisibleCount));
        OnPropertyChanged(nameof(HasVisibleItems));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? prop = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}
