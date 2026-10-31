using System.Windows;

namespace QubiCostElectrical.Wpf.Features.Cubicacion.Tipologias;

public partial class TipologiasEdificioDialog : Window
{
    private readonly TipologiasEdificioViewModel viewModel;

    public bool ApplyToCubicacion { get; private set; }
    public TipologiasEdificioData ResultData { get; private set; } = TipologiasEdificioData.Default();

    public TipologiasEdificioDialog(TipologiasEdificioData? initialData, decimal valorUfClp = 38500m)
    {
        InitializeComponent();
        viewModel = new TipologiasEdificioViewModel(initialData, valorUfClp);
        DataContext = viewModel;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnSaveOnlyClick(object sender, RoutedEventArgs e)
    {
        ApplyToCubicacion = false;
        ResultData = viewModel.ToDomain();
        DialogResult = true;
        Close();
    }

    private void OnSaveAndApplyClick(object sender, RoutedEventArgs e)
    {
        ApplyToCubicacion = true;
        ResultData = viewModel.ToDomain();
        DialogResult = true;
        Close();
    }
}
