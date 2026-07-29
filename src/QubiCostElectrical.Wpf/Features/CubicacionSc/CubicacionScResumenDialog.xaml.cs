using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public partial class CubicacionScResumenDialog : Window
{
    public CubicacionSCEngine.ResumenGrupoSC Total { get; }
    public IReadOnlyList<CubicacionSCEngine.ResumenGrupoSC> PorTablero { get; }
    public IReadOnlyList<CubicacionSCEngine.ResumenGrupoSC> PorPiso { get; }
    public IReadOnlyList<CubicacionSCEngine.ZonaResumenSC> ArtefactosPorPiso { get; }

    public bool TotalVacio => Total.Materiales.Count == 0;
    public bool TableroVacio => PorTablero.Count == 0;
    public bool PisoVacio => PorPiso.Count == 0;
    public bool ArtefactosVacio => ArtefactosPorPiso.Count == 0;

    public CubicacionScResumenDialog(
        IReadOnlyList<CuadroCargaSCRow> cuadros,
        IReadOnlyList<ZonaProyecto> zonas,
        CubicacionScConfig config)
    {
        Total = CubicacionSCEngine.AggregateTotal(cuadros, config);
        PorTablero = CubicacionSCEngine.AggregatePorTablero(cuadros, config);
        PorPiso = CubicacionSCEngine.AggregateMaterialesPorPiso(cuadros, config, zonas);
        ArtefactosPorPiso = CubicacionSCEngine.AggregateArtefactosPorZona(cuadros, zonas);
        InitializeComponent();
        DataContext = this;
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }
}

public class MaterialNameToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is string nombre)
        {
            if (nombre.StartsWith("TUBERIA", StringComparison.OrdinalIgnoreCase) &&
                nombre.IndexOf("FLEXIBLE", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669"));
            }
            if (nombre.StartsWith("CONDUCTOR", StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));
            }
            if (nombre.StartsWith("CAJA", StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569"));
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
