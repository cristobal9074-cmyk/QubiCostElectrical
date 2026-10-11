using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Infrastructure;

public sealed class PdfPreviewViewModel : ViewModelBase
{
    private static readonly double[] ZoomSteps = { 0.5, 0.75, 1.0, 1.25, 1.5, 2.0, 3.0 };

    private readonly RelayCommand previousPageCommand;
    private readonly RelayCommand nextPageCommand;
    private readonly RelayCommand zoomInCommand;
    private readonly RelayCommand zoomOutCommand;
    private readonly RelayCommand zoomResetCommand;

    private string? pdfPath;
    private int currentPage;
    private int pageCount;
    private ImageSource? currentPageImage;
    private double zoom = 1.0;
    private string statusText = "Genera una vista previa para revisar el PDF aquí.";

    public PdfPreviewViewModel()
    {
        previousPageCommand = new RelayCommand(() => CurrentPage--, () => CurrentPage > 0);
        nextPageCommand = new RelayCommand(() => CurrentPage++, () => CurrentPage < PageCount - 1);
        zoomInCommand = new RelayCommand(() => StepZoom(1));
        zoomOutCommand = new RelayCommand(() => StepZoom(-1));
        zoomResetCommand = new RelayCommand(() => SetZoom(1.0));
    }

    public ICommand PreviousPageCommand => previousPageCommand;
    public ICommand NextPageCommand => nextPageCommand;
    public ICommand ZoomInCommand => zoomInCommand;
    public ICommand ZoomOutCommand => zoomOutCommand;
    public ICommand ZoomResetCommand => zoomResetCommand;

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

    public double Zoom => zoom;

    public string ZoomPercentText => $"{(int)Math.Round(zoom * 100)}%";

    private void StepZoom(int direction)
    {
        if (direction > 0)
        {
            double next = ZoomSteps[^1];
            foreach (double step in ZoomSteps)
            {
                if (step > zoom + 0.0001)
                {
                    next = step;
                    break;
                }
            }

            SetZoom(next);
            return;
        }

        double previous = ZoomSteps[0];
        for (int i = ZoomSteps.Length - 1; i >= 0; i--)
        {
            if (ZoomSteps[i] < zoom - 0.0001)
            {
                previous = ZoomSteps[i];
                break;
            }
        }

        SetZoom(previous);
    }

    private void SetZoom(double next)
    {
        if (Math.Abs(next - zoom) < 0.0001)
            return;

        zoom = next;
        OnPropertyChanged(nameof(Zoom));
        OnPropertyChanged(nameof(ZoomPercentText));
    }

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

    private int renderSequence;

    private async void RenderCurrentPage()
    {
        int sequence = ++renderSequence;
        if (string.IsNullOrWhiteSpace(pdfPath) || PageCount == 0)
        {
            CurrentPageImage = null;
            return;
        }

        string currentPath = pdfPath;
        int page = CurrentPage;
        var result = await PdfPageRenderer.RenderPageAsync(currentPath, page);
        if (sequence == renderSequence)
        {
            CurrentPageImage = result.Image;
            if (result.Image is null && PageCount > 0)
                StatusText = "No se pudo renderizar la página del PDF.";
        }
    }

    private void RaisePageCommands()
    {
        previousPageCommand.RaiseCanExecuteChanged();
        nextPageCommand.RaiseCanExecuteChanged();
    }
}
