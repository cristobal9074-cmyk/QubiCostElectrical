using System;
using System.Collections.Generic;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.EstadosPago;

/// <summary>Celda de un mes en el calendario de estados (docs/propuesta-estados-pago.html).</summary>
public sealed class EstadoPagoMesCellVm : ViewModelBase
{
    private bool isSelected;
    private string montoLabel = "—";

    public EstadoPagoMesCellVm(
        int anio,
        int mes,
        string mesCorto,
        string mesNombre,
        string estado,
        string estadoLabel,
        bool isGenerated,
        bool isGenerable,
        int periodoIndex = 1)
    {
        Anio = anio;
        Mes = mes;
        PeriodoIndex = periodoIndex;
        MesCorto = mesCorto;
        MesNombre = mesNombre;
        Estado = estado;
        EstadoLabel = estadoLabel;
        IsGenerated = isGenerated;
        IsGenerable = isGenerable;
        montoLabel = "—";
    }

    public EstadoPagoMesCellVm(int mes, string mesCorto, string mesNombre, string estado, string estadoLabel, bool isGenerated, bool isGenerable)
        : this(AppClock.Now.Year, mes, mesCorto, mesNombre, estado, estadoLabel, isGenerated, isGenerable, mes)
    {
    }

    public int Anio { get; }
    public int Mes { get; }
    public int PeriodoIndex { get; }
    public string MesCorto { get; }
    public string MesNombre { get; }
    public string Estado { get; }
    public string EstadoLabel { get; }
    public bool IsGenerated { get; }
    public bool IsGenerable { get; }

    public string HeaderLabel => $"{MesCorto} '{Anio % 100:00}";

    public string MontoLabel
    {
        get => montoLabel;
        set => SetProperty(ref montoLabel, value);
    }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }
}

/// <summary>Sección de capítulo del Itemizado con sus partidas (fila de tabla agrupada).</summary>
public sealed class EstadoPagoSeccionVm
{
    public string Capitulo { get; init; } = "";
    public IReadOnlyList<EstadoPagoPartidaVm> Partidas { get; init; } = Array.Empty<EstadoPagoPartidaVm>();
}

/// <summary>
/// Fila de partida dentro del detalle del mes: monto contratado, avance anterior,
/// avance del mes (físico y financiero editable en borrador) y acumulado derivado.
/// </summary>
public sealed class EstadoPagoPartidaVm : ViewModelBase
{
    private readonly EstadosPagoViewModel parent;
    private readonly decimal avanceAnterior;
    private decimal avanceMes;
    private decimal montoMes;
    private decimal acumPct;
    private decimal cantidadMes;
    private decimal cantidadAcum;
    private decimal saldoCantidad;
    private decimal saldoMonto;
    private bool updatingQuantity;

    public EstadoPagoPartidaVm(
        EstadosPagoViewModel parent,
        EstadoPagoPartidaInput partida,
        decimal avanceAnterior,
        decimal avanceMes,
        decimal montoMes,
        decimal acumPct,
        bool isEditable)
    {
        this.parent = parent;
        ItemId = partida.Id;
        Item = partida.Item;
        Descripcion = partida.Descripcion;
        Monto = partida.Monto;
        Unidad = string.IsNullOrWhiteSpace(partida.Unidad) ? Unidades.GL : partida.Unidad;
        Cantidad = partida.Cantidad > 0m ? partida.Cantidad : 1m;
        PrecioUnitario = partida.PrecioUnitario > 0m ? partida.PrecioUnitario : (Cantidad > 0m ? Monto / Cantidad : Monto);
        this.avanceAnterior = avanceAnterior;
        this.avanceMes = avanceMes;
        this.montoMes = montoMes;
        this.acumPct = acumPct;
        IsEditable = isEditable;

        cantidadMes = Math.Round(Cantidad * (avanceMes / 100m), 2);
        cantidadAcum = Math.Round(Cantidad * (acumPct / 100m), 2);
        saldoCantidad = Math.Max(0m, Cantidad - cantidadAcum);
        saldoMonto = Math.Max(0m, Monto - (Monto * acumPct / 100m));
    }

    public string ItemId { get; }
    public string Item { get; }
    public string Descripcion { get; }
    public decimal Monto { get; }
    public string Unidad { get; }
    public decimal Cantidad { get; }
    public decimal PrecioUnitario { get; }
    public decimal AvanceAnterior => avanceAnterior;
    public decimal CantidadAnterior => Math.Round(Cantidad * (avanceAnterior / 100m), 2);
    public bool IsEditable { get; }
    public bool IsDisposed { get; internal set; }

    /// <summary>Monto contratado de la partida en la moneda del proyecto (CLP o UF).</summary>
    public string MontoLabel => parent.FormatMoney(Monto);

    /// <summary>Monto del mes en la moneda del proyecto (CLP o UF).</summary>
    public string MontoMesLabel => parent.FormatMoney(montoMes);

    public string PrecioUnitarioLabel => parent.FormatMoney(PrecioUnitario);
    public string CantidadLabel => $"{Cantidad:N2} {Unidad}";
    public string CantidadAnteriorLabel => $"{CantidadAnterior:N2}";
    public string CantidadAcumLabel => $"{CantidadAcum:N2}";
    public string SaldoCantidadLabel => $"{SaldoCantidad:N2}";
    public string SaldoMontoLabel => parent.FormatMoney(SaldoMonto);

    public decimal CantidadMes
    {
        get => cantidadMes;
        set
        {
            if (IsDisposed || updatingQuantity) return;
            updatingQuantity = true;
            try
            {
                decimal v = value;
                if (v < 0m) v = 0m;
                decimal maxCant = Cantidad - CantidadAnterior;
                if (maxCant < 0m) maxCant = 0m;
                if (v > maxCant) v = maxCant;

                SetProperty(ref cantidadMes, v);
                decimal nuevoAvance = Cantidad > 0m ? (v / Cantidad) * 100m : 0m;
                AvanceMes = Math.Round(nuevoAvance, 4);
            }
            finally
            {
                updatingQuantity = false;
            }
        }
    }

    public decimal AvanceMes
    {
        get => avanceMes;
        set
        {
            if (IsDisposed) return;
            decimal v = value;
            if (v < 0m) v = 0m;
            decimal max = 100m - avanceAnterior;
            if (max < 0m) max = 0m;
            if (v > max) v = max;

            bool changed = SetProperty(ref avanceMes, v);
            if (v != value)
                OnPropertyChanged(nameof(AvanceMes));
            if (!updatingQuantity)
            {
                updatingQuantity = true;
                try
                {
                    cantidadMes = Math.Round(Cantidad * (v / 100m), 2);
                    OnPropertyChanged(nameof(CantidadMes));
                }
                finally
                {
                    updatingQuantity = false;
                }
            }
            if (changed)
                parent.OnAvanceMesChanged(this);
        }
    }

    public decimal MontoMes
    {
        get => montoMes;
        private set => SetProperty(ref montoMes, value);
    }

    public decimal AcumPct
    {
        get => acumPct;
        private set => SetProperty(ref acumPct, value);
    }

    public decimal CantidadAcum
    {
        get => cantidadAcum;
        private set => SetProperty(ref cantidadAcum, value);
    }

    public decimal SaldoCantidad
    {
        get => saldoCantidad;
        private set => SetProperty(ref saldoCantidad, value);
    }

    public decimal SaldoMonto
    {
        get => saldoMonto;
        private set => SetProperty(ref saldoMonto, value);
    }

    /// <summary>Ancho de la barra de acumulado (0–100) para el template.</summary>
    public double AcumFillPct => (double)acumPct;

    internal void UpdateComputed(decimal acum, decimal montoMesValor)
    {
        AcumPct = acum;
        MontoMes = montoMesValor;
        CantidadAcum = Math.Round(Cantidad * (acum / 100m), 2);
        SaldoCantidad = Math.Max(0m, Cantidad - CantidadAcum);
        SaldoMonto = Math.Max(0m, Monto - (Monto * acum / 100m));
        OnPropertyChanged(nameof(AcumFillPct));
        OnPropertyChanged(nameof(MontoMesLabel));
        OnPropertyChanged(nameof(CantidadAcumLabel));
        OnPropertyChanged(nameof(SaldoCantidadLabel));
        OnPropertyChanged(nameof(SaldoMontoLabel));
    }
}
