using System;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Adorner visual refinado para el controlador de relleno estilo Microsoft Excel (Fill Handle).
/// Incluye:
/// - Recuadro verde nítido (1.5px) alrededor de la selección activa.
/// - Cuadrito de relleno sólido (5x5px) en la esquina inferior derecha, estético y proporcionado.
/// - Recuadro punteado fantasma semitransparente durante el arrastre.
/// - Badge flotante tipo Excel junto al cursor que previsualiza el valor resultante (ej. "207").
/// </summary>
public sealed class DataGridAutoFillAdorner : Adorner
{
    private Rect _selectionBounds = Rect.Empty;
    private Rect _dragGhostBounds = Rect.Empty;
    private bool _showHandle = true;
    private string? _previewTooltipText;
    private Point _tooltipPosition;

    private const double HandleSize = 5.0;

    private static readonly Brush HandleBrush = CreateBrush(16, 124, 65); // Verde Excel #107C41
    private static readonly Pen OutlinePen = CreatePen(16, 124, 65, 1.5);
    private static readonly Pen HandleBorderPen = new(Brushes.White, 0.75);
    private static readonly Pen GhostPen = new(CreateBrush(16, 124, 65), 1.5)
    {
        DashStyle = new DashStyle(new double[] { 3, 2 }, 0)
    };
    private static readonly Brush GhostFillBrush = new SolidColorBrush(Color.FromArgb(28, 16, 124, 65));

    private static readonly Brush TooltipBackground = Brushes.White;
    private static readonly Pen TooltipBorderPen = new(new SolidColorBrush(Color.FromRgb(209, 213, 219)), 1.0);
    private static readonly Brush TooltipShadowBrush = new SolidColorBrush(Color.FromArgb(35, 0, 0, 0));
    private static readonly Typeface TooltipTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    static DataGridAutoFillAdorner()
    {
        HandleBorderPen.Freeze();
        GhostPen.Freeze();
        GhostFillBrush.Freeze();
        TooltipBorderPen.Freeze();
        TooltipShadowBrush.Freeze();
    }

    private static SolidColorBrush CreateBrush(byte r, byte g, byte b)
    {
        var bsh = new SolidColorBrush(Color.FromRgb(r, g, b));
        bsh.Freeze();
        return bsh;
    }

    private static Pen CreatePen(byte r, byte g, byte b, double thickness)
    {
        var pen = new Pen(CreateBrush(r, g, b), thickness);
        pen.Freeze();
        return pen;
    }

    public DataGridAutoFillAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    public Rect SelectionBounds
    {
        get => _selectionBounds;
        set
        {
            if (_selectionBounds != value)
            {
                _selectionBounds = value;
                InvalidateVisual();
            }
        }
    }

    public Rect DragGhostBounds
    {
        get => _dragGhostBounds;
        set
        {
            if (_dragGhostBounds != value)
            {
                _dragGhostBounds = value;
                InvalidateVisual();
            }
        }
    }

    public bool ShowHandle
    {
        get => _showHandle;
        set
        {
            if (_showHandle != value)
            {
                _showHandle = value;
                InvalidateVisual();
            }
        }
    }

    public string? PreviewTooltipText
    {
        get => _previewTooltipText;
        set
        {
            if (_previewTooltipText != value)
            {
                _previewTooltipText = value;
                InvalidateVisual();
            }
        }
    }

    public Point TooltipPosition
    {
        get => _tooltipPosition;
        set
        {
            if (_tooltipPosition != value)
            {
                _tooltipPosition = value;
                InvalidateVisual();
            }
        }
    }

    public Rect HandleRect
    {
        get
        {
            if (_selectionBounds.IsEmpty || !_showHandle || _selectionBounds.Width <= 0 || _selectionBounds.Height <= 0)
                return Rect.Empty;

            return new Rect(_selectionBounds.Right - HandleSize / 2, _selectionBounds.Bottom - HandleSize / 2, HandleSize, HandleSize);
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (!_selectionBounds.IsEmpty && _selectionBounds.Width > 0 && _selectionBounds.Height > 0)
        {
            // Borde verde exterior estilizado
            dc.DrawRectangle(null, OutlinePen, _selectionBounds);

            // Cuadrito de relleno nítido y estético
            if (_showHandle)
            {
                var hRect = HandleRect;
                if (!hRect.IsEmpty)
                {
                    dc.DrawRectangle(HandleBrush, HandleBorderPen, hRect);
                }
            }
        }

        // Borde fantasma punteado con fondo suave durante el arrastre
        if (!_dragGhostBounds.IsEmpty && _dragGhostBounds.Width > 0 && _dragGhostBounds.Height > 0)
        {
            dc.DrawRectangle(GhostFillBrush, GhostPen, _dragGhostBounds);
        }

        // Badge flotante con el número/texto resultante tipo Excel
        if (!string.IsNullOrEmpty(_previewTooltipText))
        {
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var formatted = new FormattedText(
                _previewTooltipText,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                TooltipTypeface,
                11.0,
                new SolidColorBrush(Color.FromRgb(31, 41, 55)),
                dpi);

            double padH = 6.0;
            double padV = 3.0;
            double w = formatted.Width + padH * 2;
            double h = formatted.Height + padV * 2;

            Point pos = new(_tooltipPosition.X + 14, _tooltipPosition.Y + 14);

            if (AdornedElement is FrameworkElement fe)
            {
                if (pos.X + w > fe.ActualWidth - 4)
                    pos.X = Math.Max(4, _tooltipPosition.X - w - 8);
                if (pos.Y + h > fe.ActualHeight - 4)
                    pos.Y = Math.Max(4, _tooltipPosition.Y - h - 8);
            }

            var shadowRect = new Rect(pos.X + 1.5, pos.Y + 1.5, w, h);
            dc.DrawRoundedRectangle(TooltipShadowBrush, null, shadowRect, 3.0, 3.0);

            var tipRect = new Rect(pos.X, pos.Y, w, h);
            dc.DrawRoundedRectangle(TooltipBackground, TooltipBorderPen, tipRect, 3.0, 3.0);

            dc.DrawText(formatted, new Point(pos.X + padH, pos.Y + padV));
        }
    }
}
