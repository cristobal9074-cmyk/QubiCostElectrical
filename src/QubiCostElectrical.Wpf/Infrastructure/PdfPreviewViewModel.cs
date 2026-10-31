using System;
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
    private readonly RelayCommand zoom100Command;
    private readonly RelayCommand fitPageCommand;
    private readonly RelayCommand toggleTwoPageCommand;

    private string? pdfPath;
    private int currentPage;
    private int pageCount;
    private ImageSource? currentPageImage;
    private ImageSource? secondPageImage;
    private bool isTwoPageMode;
    private double zoom = 1.0;
    private string statusText = "Genera una vista previa para revisar el PDF aquí.";

    public event EventHandler? RequestFitPage;

    public PdfPreviewViewModel()
    {
        previousPageCommand = new RelayCommand(OnPreviousPage, () => CurrentPage > 0);
        nextPageCommand = new RelayCommand(OnNextPage, () => CurrentPage < PageCount - 1);
        zoomInCommand = new RelayCommand(() => StepZoom(1));
        zoomOutCommand = new RelayCommand(() => StepZoom(-1));
        zoomResetCommand = new RelayCommand(() => SetZoom(1.0));
        zoom100Command = new RelayCommand(() => SetZoom(1.0));
        fitPageCommand = new RelayCommand(() => RequestFitPage?.Invoke(this, EventArgs.Empty));
        toggleTwoPageCommand = new RelayCommand(ToggleTwoPage);
    }

    public ICommand PreviousPageCommand => previousPageCommand;
    public ICommand NextPageCommand => nextPageCommand;
    public ICommand ZoomInCommand => zoomInCommand;
    public ICommand ZoomOutCommand => zoomOutCommand;
    public ICommand ZoomResetCommand => zoomResetCommand;
    public ICommand Zoom100Command => zoom100Command;
    public ICommand FitPageCommand => fitPageCommand;
    public ICommand ToggleTwoPageCommand => toggleTwoPageCommand;

    public int CurrentPage
    {
        get => currentPage;
        set
        {
            int clamped = Math.Clamp(value, 0, Math.Max(0, pageCount - 1));
            if (!SetProperty(ref currentPage, clamped))
                return;

            OnPropertyChanged(nameof(PageIndicator));
            RenderPages();
            RaisePageCommands();
        }
    }

    public int PageCount
    {
        get => pageCount;
        private set
        {
            if (SetProperty(ref pageCount, value))
            {
                OnPropertyChanged(nameof(PageIndicator));
                OnPropertyChanged(nameof(HasPage));
                OnPropertyChanged(nameof(HasSecondPage));
            }
        }
    }

    public ImageSource? CurrentPageImage
    {
        get => currentPageImage;
        private set
        {
            if (SetProperty(ref currentPageImage, value))
                OnPropertyChanged(nameof(HasPage));
        }
    }

    public ImageSource? SecondPageImage
    {
        get => secondPageImage;
        private set
        {
            if (SetProperty(ref secondPageImage, value))
                OnPropertyChanged(nameof(HasSecondPage));
        }
    }

    public bool HasPage => currentPageImage != null && pageCount > 0;

    public bool HasSecondPage => isTwoPageMode && secondPageImage != null && pageCount > 0;

    public bool IsTwoPageMode
    {
        get => isTwoPageMode;
        set
        {
            if (SetProperty(ref isTwoPageMode, value))
            {
                OnPropertyChanged(nameof(TwoPageButtonLabel));
                OnPropertyChanged(nameof(PageIndicator));
                OnPropertyChanged(nameof(HasSecondPage));
                RenderPages();
                RaisePageCommands();
                RequestFitPage?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string TwoPageButtonLabel => isTwoPageMode ? "1 página" : "2 páginas";

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public string PageIndicator
    {
        get
        {
            if (PageCount <= 0)
                return "0 / 0";

            if (isTwoPageMode)
            {
                int endPage = Math.Min(CurrentPage + 2, PageCount);
                return $"{CurrentPage + 1}-{endPage} / {PageCount}";
            }

            return $"{CurrentPage + 1} / {PageCount}";
        }
    }

    public double Zoom => zoom;

    public string ZoomPercentText => $"{(int)Math.Round(zoom * 100)}%";

    public void SetZoomDirect(double next)
    {
        SetZoom(next);
    }

    private void ToggleTwoPage()
    {
        IsTwoPageMode = !IsTwoPageMode;
    }

    private void OnPreviousPage()
    {
        int step = isTwoPageMode ? 2 : 1;
        CurrentPage = Math.Max(0, CurrentPage - step);
    }

    private void OnNextPage()
    {
        int step = isTwoPageMode ? 2 : 1;
        CurrentPage = Math.Min(Math.Max(0, PageCount - 1), CurrentPage + step);
    }

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
        next = Math.Clamp(next, 0.2, 5.0);
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
            SecondPageImage = null;
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
        RenderPages();
        RaisePageCommands();
    }

    public void Clear()
    {
        pdfPath = null;
        PageCount = 0;
        CurrentPage = 0;
        CurrentPageImage = null;
        SecondPageImage = null;
        StatusText = "Genera una vista previa para revisar el PDF aquí.";
        RaisePageCommands();
    }

    private int renderSequence;

    private async void RenderPages()
    {
        int sequence = ++renderSequence;
        if (string.IsNullOrWhiteSpace(pdfPath) || PageCount == 0)
        {
            CurrentPageImage = null;
            SecondPageImage = null;
            return;
        }

        string currentPath = pdfPath;
        int page = CurrentPage;
        bool twoPage = isTwoPageMode;

        var result = await PdfPageRenderer.RenderPageAsync(currentPath, page);
        if (sequence != renderSequence)
            return;

        CurrentPageImage = result.Image;
        if (result.Image is null && PageCount > 0)
            StatusText = "No se pudo renderizar la página del PDF.";

        if (twoPage && page + 1 < PageCount)
        {
            var secondResult = await PdfPageRenderer.RenderPageAsync(currentPath, page + 1);
            if (sequence == renderSequence)
                SecondPageImage = secondResult.Image;
        }
        else
        {
            SecondPageImage = null;
        }
    }

    private void RaisePageCommands()
    {
        previousPageCommand.RaiseCanExecuteChanged();
        nextPageCommand.RaiseCanExecuteChanged();
    }
}
