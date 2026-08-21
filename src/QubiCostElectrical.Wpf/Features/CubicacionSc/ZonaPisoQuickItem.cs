using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed class ZonaPisoQuickItem : ViewModelBase
{
    public string ZonaId { get; init; } = "";
    public string Nombre { get; init; } = "";
    // Avisa a la fila del circuito para refrescar el total en vivo del flyout (footer).
    public Action? OnChanged { get; set; }

    // Los TextBox del flyout se bindean a estos dos strings (no a MetrosBandeja/MetrosTuberia
    // directo): un binding TwoWay decimal con UpdateSourceTrigger=PropertyChanged reformatea el
    // texto en cada tecla (ToString del decimal) y WPF reempuja ese texto al control, saltando
    // el cursor al final — se sentía "lento"/con tirones al escribir rápido. Con string no hay
    // conversión de tipo de por medio: la tecla entra, el texto queda tal cual, cero reformateo.
    private string bandText = "";
    private string pvcText = "";
    public string BandText { get => bandText; set { bandText = value; OnPropertyChanged(); OnPropertyChanged(nameof(MetrosBandeja)); OnPropertyChanged(nameof(EsActiva)); OnChanged?.Invoke(); } }
    public string PvcText { get => pvcText; set { pvcText = value; OnPropertyChanged(); OnPropertyChanged(nameof(MetrosTuberia)); OnPropertyChanged(nameof(EsActiva)); OnChanged?.Invoke(); } }

    public decimal MetrosBandeja
    {
        get => decimal.TryParse(bandText, out var v) ? v : 0m;
        set => BandText = value == 0m ? "" : value.ToString("0.##");
    }
    public decimal MetrosTuberia
    {
        get => decimal.TryParse(pvcText, out var v) ? v : 0m;
        set => PvcText = value == 0m ? "" : value.ToString("0.##");
    }
    public bool EsActiva => MetrosBandeja > 0m || MetrosTuberia > 0m;
}
