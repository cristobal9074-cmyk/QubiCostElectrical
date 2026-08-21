using System;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Apu;

public sealed class ApuRecursoVm : ViewModelBase
{
    public ApuRecursoVm(ApuRecursoRow source)
    {
        Source = source;
    }

    public ApuRecursoRow Source { get; }

    // ─── Tipo (editable) ─────────────────────────────────────────────────────
    public ApuTipoRecurso Tipo
    {
        get => Source.Tipo;
        set
        {
            if (Source.Tipo == value) return;
            Source.Tipo = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TipoLabel));
            OnPropertyChanged(nameof(EsManodeObra));
        }
    }

    public string TipoLabel => Source.Tipo.ToString();

    public string CodigoMaterial
    {
        get => Source.CodigoMaterial;
        set { Source.CodigoMaterial = CodeHelper.Normalize(value); OnPropertyChanged(); }
    }

    public string Descripcion
    {
        get => Source.Descripcion;
        set { Source.Descripcion = value; OnPropertyChanged(); }
    }

    public string Unidad
    {
        get => Source.Unidad;
        set { Source.Unidad = value; OnPropertyChanged(); }
    }

    // ─── Cantidades y precios (editable en 5B) ────────────────────────────────
    public decimal Rendimiento
    {
        get => Source.Rendimiento;
        set
        {
            if (Source.Rendimiento == value) return;
            Source.Rendimiento = value;
            OnPropertyChanged();
            RefreshCalculated();
        }
    }

    public decimal PrecioUf
    {
        get => Source.PrecioUf;
        set
        {
            if (Source.PrecioUf == value) return;
            Source.PrecioUf = value;
            OnPropertyChanged();
            RefreshCalculated();
        }
    }

    public decimal ValorHHUf
    {
        get => Source.ValorHHUf;
        set
        {
            if (Source.ValorHHUf == value) return;
            Source.ValorHHUf = value;
            OnPropertyChanged();
            RefreshCalculated();
        }
    }

    public decimal MermaPct
    {
        get => Source.MermaPct;
        set
        {
            if (Source.MermaPct == value) return;
            Source.MermaPct = value;
            OnPropertyChanged();
            RefreshCalculated();
        }
    }

    public decimal ImprevistosPct
    {
        get => Source.ImprevistosPct;
        set
        {
            if (Source.ImprevistosPct == value) return;
            Source.ImprevistosPct = value;
            OnPropertyChanged();
            RefreshCalculated();
        }
    }

    // ─── Calculados (solo lectura) ────────────────────────────────────────────
    public decimal CostoBaseUf => Source.CostoBaseUf;
    public decimal MermaUf => Source.MermaUf;
    public decimal CostoUnitUf => Source.CostoUnitUf;

    // ─── Auditoría ────────────────────────────────────────────────────────────
    public string FuentePrecio => Source.FuentePrecio;
    public DateTime FechaPrecio => Source.FechaPrecio;
    public string Estado => Source.Estado;
    public string Nota
    {
        get => Source.Nota;
        set { Source.Nota = value; OnPropertyChanged(); }
    }

    // ─── Presentación ────────────────────────────────────────────────────────
    public bool HasPrecio => Source.PrecioBaseUf > 0m;
    public string EstadoColor => Source.Estado switch
    {
        "OK" => "#1E7A4B",
        "Falta precio" => "#B23434",
        _ => "#9A6A10"
    };

    // Tras migración CLP: PrecioBaseUf y CostoUnitUf contienen CLP (nombre legado)
    public string PrecioFormatted => $"$ {Source.PrecioBaseUf:N0}";
    public string CostoFormatted  => $"$ {Source.CostoUnitUf:N0}";

    public bool EsManodeObra => Source.Tipo is ApuTipoRecurso.MO or ApuTipoRecurso.HH;

    public void RefreshCalculated()
    {
        OnPropertyChanged(nameof(CostoBaseUf));
        OnPropertyChanged(nameof(MermaUf));
        OnPropertyChanged(nameof(CostoUnitUf));
        OnPropertyChanged(nameof(PrecioFormatted));
        OnPropertyChanged(nameof(CostoFormatted));
        OnPropertyChanged(nameof(HasPrecio));
    }
}
