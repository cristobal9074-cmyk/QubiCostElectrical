using System;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.EstadoPago;

public sealed class EstadoPagoMonthVm : ViewModelBase
{
    private string _label = "";
    private int _year;
    private int _month;
    private decimal _amount;
    private bool _isActive;
    private PaymentCertificateStatus _status;
    private PaymentCertificate? _certificate;

    public EstadoPagoMonthVm(int year, int month)
    {
        _year = year;
        _month = month;
        _label = new DateTime(year, month, 1).ToString("MMM yyyy").ToUpper();
    }

    public string Label { get => _label; set => SetProperty(ref _label, value); }
    public int Year { get => _year; set => SetProperty(ref _year, value); }
    public int Month { get => _month; set => SetProperty(ref _month, value); }
    public decimal Amount { get => _amount; set => SetProperty(ref _amount, value); }
    public string AmountLabel => Amount > 0 ? $"$ {Amount:N0}" : "—";
    public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
    public PaymentCertificateStatus Status { get => _status; set { if (SetProperty(ref _status, value)) OnPropertyChanged(nameof(StatusKind)); } }
    public PaymentCertificate? Certificate { get => _certificate; set => SetProperty(ref _certificate, value); }

    public string StatusKind => Status switch
    {
        PaymentCertificateStatus.Aprobado or PaymentCertificateStatus.Pagado or PaymentCertificateStatus.Cerrado => "approved",
        PaymentCertificateStatus.Borrador => "draft",
        PaymentCertificateStatus.Presentado or PaymentCertificateStatus.EnRevision or PaymentCertificateStatus.PendienteAprobacion => "pending",
        _ => "locked"
    };

    public string PeriodKey => $"{Year:D4}-{Month:D2}";
}

public sealed class EstadoPagoRowVm : ViewModelBase
{
    private string _item = "";
    private string _descripcion = "";
    private string _unidad = "";
    private decimal _cantidadContractual;
    private decimal _precioUnitario;
    private decimal _cantidadAnterior;
    private decimal _cantidadPeriodo;
    private decimal _cantidadAcumulada;
    private decimal _montoPeriodo;
    private decimal _montoAcumulado;
    private decimal _saldo;
    private bool _esTitulo;
    private bool _isSelected;

    public string Item { get => _item; set => SetProperty(ref _item, value); }
    public string Descripcion { get => _descripcion; set => SetProperty(ref _descripcion, value); }
    public string Unidad { get => _unidad; set => SetProperty(ref _unidad, value); }
    public decimal CantidadContractual { get => _cantidadContractual; set => SetProperty(ref _cantidadContractual, value); }
    public decimal PrecioUnitario { get => _precioUnitario; set => SetProperty(ref _precioUnitario, value); }
    public decimal CantidadAnterior { get => _cantidadAnterior; set { if (SetProperty(ref _cantidadAnterior, value)) Recalc(); } }
    public decimal CantidadPeriodo { get => _cantidadPeriodo; set { if (SetProperty(ref _cantidadPeriodo, value)) Recalc(); } }
    public decimal CantidadAcumulada { get => _cantidadAcumulada; private set => SetProperty(ref _cantidadAcumulada, value); }
    public decimal MontoPeriodo { get => _montoPeriodo; private set => SetProperty(ref _montoPeriodo, value); }
    public decimal MontoAcumulado { get => _montoAcumulado; private set => SetProperty(ref _montoAcumulado, value); }
    public decimal Saldo { get => _saldo; private set => SetProperty(ref _saldo, value); }
    public bool EsTitulo { get => _esTitulo; set => SetProperty(ref _esTitulo, value); }
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    public decimal TotalContractual => CantidadContractual * PrecioUnitario;
    public decimal PorcentajeAvance => CantidadContractual > 0 ? Math.Round(CantidadAcumulada / CantidadContractual * 100, 1) : 0m;
    public string AvancePctLabel => $"{PorcentajeAvance:F1}%";
    public string AvanceClass => PorcentajeAvance >= 80m ? "high" : PorcentajeAvance >= 30m ? "mid" : "low";

    public string TotalContractualLabel => $"$ {TotalContractual:N2}";
    public string PrecioUnitarioLabel => $"$ {PrecioUnitario:N2}";
    public string MontoPeriodoLabel => $"$ {MontoPeriodo:N2}";
    public string MontoAcumuladoLabel => $"$ {MontoAcumulado:N2}";
    public string SaldoLabel => $"$ {Saldo:N2}";
    public string CantidadContractualLabel => CantidadContractual.ToString("N2");
    public string CantidadAnteriorLabel => CantidadAnterior.ToString("N2");
    public string CantidadAcumuladaLabel => CantidadAcumulada.ToString("N2");
    public string CantidadPeriodoText
    {
        get => CantidadPeriodo.ToString("N2");
        set
        {
            if (decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out var parsed))
                CantidadPeriodo = parsed;
        }
    }

    public void Recalc()
    {
        CantidadAcumulada = CantidadAnterior + CantidadPeriodo;
        MontoPeriodo = CantidadPeriodo * PrecioUnitario;
        MontoAcumulado = CantidadAcumulada * PrecioUnitario;
        Saldo = (CantidadContractual - CantidadAcumulada) * PrecioUnitario;
        OnPropertyChanged(nameof(TotalContractual));
        OnPropertyChanged(nameof(PorcentajeAvance));
        OnPropertyChanged(nameof(AvancePctLabel));
        OnPropertyChanged(nameof(AvanceClass));
        OnPropertyChanged(nameof(TotalContractualLabel));
        OnPropertyChanged(nameof(PrecioUnitarioLabel));
        OnPropertyChanged(nameof(MontoPeriodoLabel));
        OnPropertyChanged(nameof(MontoAcumuladoLabel));
        OnPropertyChanged(nameof(SaldoLabel));
        OnPropertyChanged(nameof(CantidadContractualLabel));
        OnPropertyChanged(nameof(CantidadAnteriorLabel));
        OnPropertyChanged(nameof(CantidadAcumuladaLabel));
    }

    public void LoadFromItemizadoRow(ItemizadoRow row, decimal prevCantidad = 0)
    {
        Item = row.Item ?? "";
        Descripcion = row.Descripcion ?? "";
        Unidad = row.Unidad ?? Unidades.GL;
        CantidadContractual = row.CantidadOriginal;
        PrecioUnitario = 0m;
        CantidadAnterior = prevCantidad;
        CantidadPeriodo = 0;
        EsTitulo = row.EsTitulo;
        Recalc();
    }
}
