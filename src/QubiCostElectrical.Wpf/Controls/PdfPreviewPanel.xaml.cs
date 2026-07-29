using System.Windows.Controls;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Controls;

public partial class PdfPreviewPanel : UserControl
{
    public PdfPreviewPanel()
    {
        InitializeComponent();
    }

    public PdfPreviewPanel(PdfPreviewViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
