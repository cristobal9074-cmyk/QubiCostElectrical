using System;
using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Templates;

public partial class PlantillasView : UserControl
{
    public static ApuTipoRecurso[] ApuTiposRecurso { get; } = (ApuTipoRecurso[])Enum.GetValues(typeof(ApuTipoRecurso));
    public static TipoCalculoParametrico[] TiposCalculo { get; } = (TipoCalculoParametrico[])Enum.GetValues(typeof(TipoCalculoParametrico));

    public PlantillasView(PlantillasVm vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
