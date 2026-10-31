using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QubiCostElectrical;

public sealed class CompraMaterialViewModel : INotifyPropertyChanged
{
    private decimal toleranciaPct;
    private decimal cantidadCompra;
    private decimal totalCompraUf;
    private decimal totalCompraClp;

    public string Codigo { get; }
    public string Descripcion { get; }
    public string Unidad { get; }
    public string Categoria { get; }
    public string Proveedor { get; }
    public decimal CantidadNeta { get; }
    public decimal MatUfUnitario { get; }
    public decimal TotalNetoUf { get; }
    public int Lineas { get; }
    public decimal MatClpUnitario { get; }
    public decimal TotalNetoClp { get; }

    public CompraMaterialViewModel(
        string Codigo,
        string Descripcion,
        string Unidad,
        string Categoria,
        string Proveedor,
        decimal CantidadNeta,
        decimal ToleranciaPct,
        decimal CantidadCompra,
        decimal MatUfUnitario,
        decimal TotalNetoUf,
        decimal TotalCompraUf,
        int Lineas,
        decimal MatClpUnitario = 0m,
        decimal TotalNetoClp = 0m,
        decimal TotalCompraClp = 0m)
    {
        this.Codigo = Codigo;
        this.Descripcion = Descripcion;
        this.Unidad = Unidad;
        this.Categoria = Categoria;
        this.Proveedor = Proveedor;
        this.CantidadNeta = CantidadNeta;
        this.toleranciaPct = ToleranciaPct;
        this.cantidadCompra = CantidadCompra;
        this.MatUfUnitario = MatUfUnitario;
        this.TotalNetoUf = TotalNetoUf;
        this.totalCompraUf = TotalCompraUf;
        this.Lineas = Lineas;
        this.MatClpUnitario = MatClpUnitario;
        this.TotalNetoClp = TotalNetoClp;
        this.totalCompraClp = TotalCompraClp;
    }

    public decimal ToleranciaPct
    {
        get => toleranciaPct;
        set
        {
            decimal safe = Math.Clamp(value, 0m, 100m);
            if (toleranciaPct != safe)
            {
                toleranciaPct = safe;
                Recalcular();
                OnPropertyChanged();
                ToleranceChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public decimal CantidadCompra
    {
        get => cantidadCompra;
        private set
        {
            if (cantidadCompra != value)
            {
                cantidadCompra = value;
                OnPropertyChanged();
            }
        }
    }

    public decimal TotalCompraUf
    {
        get => totalCompraUf;
        private set
        {
            if (totalCompraUf != value)
            {
                totalCompraUf = value;
                OnPropertyChanged();
            }
        }
    }

    public decimal TotalCompraClp
    {
        get => totalCompraClp;
        private set
        {
            if (totalCompraClp != value)
            {
                totalCompraClp = value;
                OnPropertyChanged();
            }
        }
    }

    public event EventHandler? ToleranceChanged;

    private void Recalcular()
    {
        decimal factor = 1m + (toleranciaPct / 100m);
        CantidadCompra = CantidadNeta * factor;
        TotalCompraUf = CantidadCompra * MatUfUnitario;
        TotalCompraClp = CantidadCompra * MatClpUnitario;
    }

    /// <summary>
    /// Clave de agrupación: proveedor si existe; si no, categoría (fallback UI).
    /// </summary>
    public string GrupoProveedor =>
        !string.IsNullOrWhiteSpace(Proveedor)
            ? Proveedor.Trim()
            : string.IsNullOrWhiteSpace(Categoria) ? "Sin categoría" : Categoria.Trim();

    /// <summary>
    /// Clave de agrupación por familia / categoría de material.
    /// </summary>
    public string GrupoFamilia =>
        !string.IsNullOrWhiteSpace(Categoria) ? Categoria.Trim() : "Sin familia / categoría";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Desglose de trazabilidad viva: en qué partida y área participa un material comprado.
/// </summary>
public sealed record MaterialUsageBreakdown(
    string ItemId,
    string ItemNumber,
    string PartidaDescripcion,
    string Area,
    decimal Cantidad,
    decimal PorcentajeDelTotal,
    string Codigo,
    string Descripcion);
