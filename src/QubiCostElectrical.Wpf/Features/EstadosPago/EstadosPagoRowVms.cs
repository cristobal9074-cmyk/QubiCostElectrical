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

    public EstadoPagoMesCellVm(int mes, string mesCorto, string mesNombre, string estado, string estadoLabel, bool isGenerated, bool isGenerable)
    {
        Mes = mes;
        MesCorto = mesCorto;
        MesNombre = mesNombre;
        Estado = estado;
        EstadoLabel = estadoLabel;
        IsGenerated = isGenerated;
        IsGenerable = isGenerable;
        montoLabel = "—";
    }

    public int Mes { get; }
    public string MesCorto { get; }
    public string MesNombre { get; }
    public string Estado { get; }
    public string EstadoLabel { get; }
    public bool IsGenerated { get; }
    public bool IsGenerable { get; }

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
/// avance del mes (editable en borrador) y acumulado derivado.
/// </summary>
public sealed class EstadoPagoPartidaVm : ViewModelBase
{
    private readonly EstadosPagoViewModel parent;
    private readonly decimal avanceAnterior;
    private decimal avanceMes;
    private decimal montoMes;
    private decimal acumPct;

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
        this.avanceAnterior = avanceAnterior;
        this.avanceMes = avanceMes;
        this.montoMes = montoMes;
        this.acumPct = acumPct;
        IsEditable = isEditable;
    }

    public string ItemId { get; }
    public string Item { get; }
    public string Descripcion { get; }
    public decimal Monto { get; }
    public decimal AvanceAnterior => avanceAnterior;
    public bool IsEditable { get; }

    /// <summary>Monto contratado de la partida en la moneda del proyecto (CLP o UF).</summary>
    public string MontoLabel => parent.FormatMoney(Monto);

    /// <summary>Monto del mes en la moneda del proyecto (CLP o UF).</summary>
    public string MontoMesLabel => parent.FormatMoney(montoMes);

    public decimal AvanceMes
    {
        get => avanceMes;
        set
        {
            decimal v = value;
            if (v < 0m) v = 0m;
            decimal max = 100m - avanceAnterior;
            if (max < 0m) max = 0m;
            if (v > max) v = max;

            bool changed = SetProperty(ref avanceMes, v);
            if (v != value)
                OnPropertyChanged(nameof(AvanceMes));
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

    /// <summary>Ancho de la barra de acumulado (0–100) para el template.</summary>
    public double AcumFillPct => (double)acumPct;

    internal void UpdateComputed(decimal acum, decimal montoMesValor)
    {
        AcumPct = acum;
        MontoMes = montoMesValor;
        OnPropertyChanged(nameof(AcumFillPct));
        OnPropertyChanged(nameof(MontoMesLabel));
    }
}
