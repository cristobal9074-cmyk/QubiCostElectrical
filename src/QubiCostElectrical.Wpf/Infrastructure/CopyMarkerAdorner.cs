using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace QubiCostElectrical.Wpf.Infrastructure;

// Marco punteado animado sobre la selección copiada — el equivalente visual de las
// "hormigas marchando" de Excel, pero con el azul corporativo de la app en vez del verde
// nativo de Excel, para que se sienta parte del mismo sistema de diseño.
public sealed class CopyMarkerAdorner : Adorner
{
    private static readonly SolidColorBrush MarkerBrush = CreateBrush();
    private readonly DispatcherTimer timer;
    private readonly Pen pen;
    private Rect rect = Rect.Empty;
    private double dashOffset;

    public CopyMarkerAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
        pen = new Pen(MarkerBrush, 2);
        timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = System.TimeSpan.FromMilliseconds(90) };
        timer.Tick += OnTick;
        timer.Start();
    }

    public void UpdateRect(Rect newRect)
    {
        rect = newRect;
        InvalidateVisual();
    }

    public void Stop()
    {
        timer.Stop();
        timer.Tick -= OnTick;
    }

    private void OnTick(object? sender, System.EventArgs e)
    {
        dashOffset = (dashOffset + 1) % 8;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0)
            return;

        pen.DashStyle = new DashStyle(new double[] { 4, 3 }, dashOffset);
        drawingContext.DrawRectangle(null, pen, rect);
    }

    private static SolidColorBrush CreateBrush()
    {
        var brush = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xB0));
        brush.Freeze();
        return brush;
    }
}
