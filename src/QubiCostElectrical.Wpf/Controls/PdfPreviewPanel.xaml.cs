using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Controls;

public partial class PdfPreviewPanel : UserControl
{
    public static readonly DependencyProperty ShowHeaderProperty =
        DependencyProperty.Register(nameof(ShowHeader), typeof(bool), typeof(PdfPreviewPanel), new PropertyMetadata(true));

    private const double PageMargin = 10;

    private PdfPreviewViewModel? trackedViewModel;
    private double lastExtentWidth = -1;
    private double lastExtentHeight = -1;
    private double lastOffsetX;
    private double lastOffsetY;

    public PdfPreviewPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        PART_Scroll.SizeChanged += (_, _) => Relayout(keepCenter: true);
    }

    public bool ShowHeader
    {
        get => (bool)GetValue(ShowHeaderProperty);
        set => SetValue(ShowHeaderProperty, value);
    }

    public PdfPreviewPanel(PdfPreviewViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (ReferenceEquals(trackedViewModel, e.NewValue as PdfPreviewViewModel))
            return;

        if (trackedViewModel is not null)
        {
            trackedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            trackedViewModel.RequestFitPage -= OnRequestFitPage;
        }

        trackedViewModel = e.NewValue as PdfPreviewViewModel;

        if (trackedViewModel is not null)
        {
            trackedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            trackedViewModel.RequestFitPage += OnRequestFitPage;
        }

        ResetView();
    }

    private void OnRequestFitPage(object? sender, EventArgs e)
    {
        Relayout(keepCenter: false, fitPage: true);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PdfPreviewViewModel.CurrentPageImage) or nameof(PdfPreviewViewModel.SecondPageImage))
        {
            ResetView();
        }
        else if (e.PropertyName == nameof(PdfPreviewViewModel.Zoom))
        {
            Relayout(keepCenter: true);
        }
    }

    private void ResetView()
    {
        lastExtentWidth = -1;
        lastExtentHeight = -1;
        lastOffsetX = 0;
        lastOffsetY = 0;
        Relayout(keepCenter: false);
    }

    private void Relayout(bool keepCenter, bool fitPage = false)
    {
        if (PART_Scroll is null || PART_Page is null || PART_Image is null)
            return;

        double viewportWidth = PART_Scroll.ActualWidth;
        double viewportHeight = PART_Scroll.ActualHeight;
        if (viewportWidth <= 0 || viewportHeight <= 0)
            return;

        if (PART_Image.Source is not BitmapSource bitmap || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
        {
            PART_Page.Visibility = Visibility.Collapsed;
            if (PART_Page2 is not null)
                PART_Page2.Visibility = Visibility.Collapsed;

            PART_Image.Width = double.NaN;
            PART_Image.Height = double.NaN;
            lastExtentWidth = -1;
            lastExtentHeight = -1;
            return;
        }

        PART_Page.Visibility = Visibility.Visible;
        bool hasSecond = trackedViewModel?.HasSecondPage == true && PART_Image2?.Source is BitmapSource;
        if (PART_Page2 is not null)
            PART_Page2.Visibility = hasSecond ? Visibility.Visible : Visibility.Collapsed;

        int pageMultiplier = hasSecond ? 2 : 1;
        double extentMargin = PageMargin * 2 * pageMultiplier;
        double scrollbar = SystemParameters.VerticalScrollBarWidth;
        double horizontalBar = SystemParameters.HorizontalScrollBarHeight;

        if (fitPage && trackedViewModel is not null)
        {
            double availableFitW = Math.Max(1, viewportWidth - (PageMargin * 2 * pageMultiplier) - 16);
            double availableFitH = Math.Max(1, viewportHeight - (PageMargin * 2) - 20);

            double targetWidthPerSinglePage = availableFitW / pageMultiplier;
            double scaleW = targetWidthPerSinglePage / bitmap.PixelWidth;
            double scaleH = availableFitH / bitmap.PixelHeight;

            double fitFactor = Math.Min(scaleW, scaleH);
            trackedViewModel.SetZoomDirect(Math.Round(fitFactor, 2));
            return;
        }

        double zoom = trackedViewModel?.Zoom ?? 1.0;

        // Ajuste iterativo considerando scrollbars
        bool verticalBarVisible = true;
        bool horizontalBarVisible = false;
        double singleWidth = 0;
        double singleHeight = 0;

        for (int i = 0; i < 4; i++)
        {
            double availableWidth = Math.Max(1, (viewportWidth - extentMargin - (verticalBarVisible ? scrollbar : 0)) / pageMultiplier);
            double scale = availableWidth / bitmap.PixelWidth * zoom;
            singleWidth = bitmap.PixelWidth * scale;
            singleHeight = bitmap.PixelHeight * scale;

            double totalW = singleWidth * pageMultiplier + extentMargin;
            double totalH = singleHeight + (PageMargin * 2);

            bool needsVerticalBar = totalH > viewportHeight + 0.5;
            bool needsHorizontalBar = totalW > viewportWidth + 0.5;
            if (needsVerticalBar == verticalBarVisible && needsHorizontalBar == horizontalBarVisible)
                break;

            verticalBarVisible = needsVerticalBar;
            horizontalBarVisible = needsHorizontalBar;
        }

        PART_Image.Width = singleWidth;
        PART_Image.Height = singleHeight;

        if (PART_Image2 is not null && hasSecond)
        {
            PART_Image2.Width = singleWidth;
            PART_Image2.Height = singleHeight;
        }

        double extentWidth = singleWidth * pageMultiplier + extentMargin;
        double extentHeight = singleHeight + (PageMargin * 2);

        if (PART_PagesContainer is not null)
        {
            PART_PagesContainer.HorizontalAlignment = extentWidth <= viewportWidth + 0.5
                ? HorizontalAlignment.Center
                : HorizontalAlignment.Left;
            PART_PagesContainer.VerticalAlignment = extentHeight <= viewportHeight + 0.5
                ? VerticalAlignment.Center
                : VerticalAlignment.Top;
        }

        double offsetX;
        double offsetY;
        if (keepCenter && lastExtentWidth > 0 && lastExtentHeight > 0)
        {
            offsetX = KeepCentered(lastOffsetX, lastExtentWidth, extentWidth, viewportWidth);
            offsetY = KeepCentered(lastOffsetY, lastExtentHeight, extentHeight, viewportHeight);
        }
        else
        {
            offsetX = extentWidth > viewportWidth ? (extentWidth - viewportWidth) / 2 : 0;
            offsetY = 0;
        }

        PART_Scroll.ScrollToHorizontalOffset(offsetX);
        PART_Scroll.ScrollToVerticalOffset(offsetY);

        lastExtentWidth = extentWidth;
        lastExtentHeight = extentHeight;
        lastOffsetX = offsetX;
        lastOffsetY = offsetY;
    }

    private static double KeepCentered(double offset, double oldExtent, double newExtent, double viewport)
    {
        if (newExtent <= viewport + 0.5)
            return 0;
        if (oldExtent <= 0.5)
            return Math.Max(0, (newExtent - viewport) / 2);

        double center = offset + viewport / 2;
        double ratio = newExtent / oldExtent;
        double next = center * ratio - viewport / 2;
        double maxOffset = Math.Max(0, newExtent - viewport);
        return Math.Clamp(next, 0, maxOffset);
    }
}
