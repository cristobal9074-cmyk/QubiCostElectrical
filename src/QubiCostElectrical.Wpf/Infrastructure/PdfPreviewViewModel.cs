using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Infrastructure;

public sealed class PdfPreviewViewModel : ViewModelBase
{
    private readonly RelayCommand previousPageCommand;
    private readonly RelayCommand nextPageCommand;

    private string? pdfPath;
    private int currentPage;
    private int pageCount;
    private ImageSource? currentPageImage;
    private string statusText = "Genera una vista previa para revisar el PDF aquí.";

    public PdfPreviewViewModel()
    {
        previousPageCommand = new RelayCommand(() => CurrentPage--, () => CurrentPage > 0);
        nextPageCommand = new RelayCommand(() => CurrentPage++, () => CurrentPage < PageCount - 1);
    }

    public ICommand PreviousPageCommand => previousPageCommand;
    public ICommand NextPageCommand => nextPageCommand;

    public int CurrentPage
    {
        get => currentPage;
        set
        {
            int clamped = Math.Clamp(value, 0, Math.Max(0, pageCount - 1));
            if (!SetProperty(ref currentPage, clamped))
                return;

            OnPropertyChanged(nameof(PageIndicator));
            RenderCurrentPage();
            RaisePageCommands();
        }
    }

    public int PageCount
    {
        get => pageCount;
        private set
        {
            if (SetProperty(ref pageCount, value))
                OnPropertyChanged(nameof(PageIndicator));
        }
    }

    public ImageSource? CurrentPageImage
    {
        get => currentPageImage;
        private set => SetProperty(ref currentPageImage, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public string PageIndicator => PageCount <= 0 ? "0 / 0" : $"{CurrentPage + 1} / {PageCount}";

    public async Task LoadAsync(string? path)
    {
        pdfPath = path;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            PageCount = 0;
            CurrentPage = 0;
            CurrentPageImage = null;
            StatusText = "Sin vista previa generada.";
            RaisePageCommands();
            return;
        }

        StatusText = "Cargando vista previa...";
        int count = await Task.Run(() => PdfPageRenderer.GetPageCount(path));
        PageCount = count;
        CurrentPage = 0;
        StatusText = count == 0
            ? "No se pudo leer el PDF generado."
            : $"Mostrando {Path.GetFileName(path)}";
        RenderCurrentPage();
        RaisePageCommands();
    }

    public void Clear()
    {
        pdfPath = null;
        PageCount = 0;
        CurrentPage = 0;
        CurrentPageImage = null;
        StatusText = "Genera una vista previa para revisar el PDF aquí.";
        RaisePageCommands();
    }

    private void RenderCurrentPage()
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || PageCount == 0)
        {
            CurrentPageImage = null;
            return;
        }

        CurrentPageImage = PdfPageRenderer.RenderPage(pdfPath, CurrentPage);
    }

    private void RaisePageCommands()
    {
        previousPageCommand.RaiseCanExecuteChanged();
        nextPageCommand.RaiseCanExecuteChanged();
    }
}
