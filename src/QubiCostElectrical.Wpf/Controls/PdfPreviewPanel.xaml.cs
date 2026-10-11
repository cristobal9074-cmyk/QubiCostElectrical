using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
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
            trackedViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        trackedViewModel = e.NewValue as PdfPreviewViewModel;

        if (trackedViewModel is not null)
            trackedViewModel.PropertyChanged += OnViewModelPropertyChanged;

        ResetView();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PdfPreviewViewModel.CurrentPageImage))
            ResetView();
        else if (e.PropertyName == nameof(PdfPreviewViewModel.Zoom))
            Relayout(keepCenter: true);
    }

    private void ResetView()
    {
        lastExtentWidth = -1;
        lastExtentHeight = -1;
        lastOffsetX = 0;
        lastOffsetY = 0;
        Relayout(keepCenter: false);
    }

    private void Relayout(bool keepCenter)
    {
        if (PART_Scroll is null || PART_Page is null || PART_Image is null)
            return;

        double viewportWidth = PART_Scroll.ActualWidth;
        double viewportHeight = PART_Scroll.ActualHeight;
        if (viewportWidth <= 0 || viewportHeight <= 0)
            return;

        if (PART_Image.Source is not System.Windows.Media.Imaging.BitmapSource bitmap
            || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
        {
            PART_Page.Visibility = Visibility.Collapsed;
            PART_Image.Width = double.NaN;
            PART_Image.Height = double.NaN;
            lastExtentWidth = -1;
            lastExtentHeight = -1;
            return;
        }

        PART_Page.Visibility = Visibility.Visible;

        double zoom = (DataContext as PdfPreviewViewModel)?.Zoom ?? 1.0;
        double extentMargin = PageMargin * 2;
        double scrollbar = SystemParameters.VerticalScrollBarWidth;
        double horizontalBar = SystemParameters.HorizontalScrollBarHeight;

        // Ajuste a ancho (base 100%) con iteración por las barras de scroll.
        bool verticalBarVisible = true;
        bool horizontalBarVisible = false;
        double width = 0;
        double height = 0;
        for (int i = 0; i < 4; i++)
        {
            double availableWidth = Math.Max(1, viewportWidth - extentMargin
                - (verticalBarVisible ? scrollbar : 0));
            double availableHeight = Math.Max(1, viewportHeight - extentMargin
                - (horizontalBarVisible ? horizontalBar : 0));
            _ = availableHeight;

            double scale = availableWidth / bitmap.PixelWidth * zoom;
            width = bitmap.PixelWidth * scale;
            height = bitmap.PixelHeight * scale;

            bool needsVerticalBar = height + extentMargin > viewportHeight + 0.5;
            bool needsHorizontalBar = width + extentMargin > viewportWidth + 0.5;
            if (needsVerticalBar == verticalBarVisible && needsHorizontalBar == horizontalBarVisible)
                break;

            verticalBarVisible = needsVerticalBar;
            horizontalBarVisible = needsHorizontalBar;
        }

        PART_Image.Width = width;
        PART_Image.Height = height;

        double extentWidth = width + extentMargin;
        double extentHeight = height + extentMargin;

        PART_Page.HorizontalAlignment = extentWidth <= viewportWidth + 0.5
            ? HorizontalAlignment.Center
            : HorizontalAlignment.Left;
        PART_Page.VerticalAlignment = extentHeight <= viewportHeight + 0.5
            ? VerticalAlignment.Center
            : VerticalAlignment.Top;

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
