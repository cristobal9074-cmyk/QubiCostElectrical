using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Provee desplazamiento suave (smooth scrolling) inercial e interpolado para ScrollViewer en WPF,
/// garantizando además que los controles internos (como DataGrid, Canvas, Cards) no capturen
/// ni bloqueen la rueda del ratón (resolución del problema de wheel trapping).
/// </summary>
public static class SmoothScroll
{
    public static readonly DependencyProperty EnableProperty =
        DependencyProperty.RegisterAttached(
            "Enable",
            typeof(bool),
            typeof(SmoothScroll),
            new PropertyMetadata(false, OnEnableChanged));

    public static bool GetEnable(DependencyObject obj) => (bool)obj.GetValue(EnableProperty);
    public static void SetEnable(DependencyObject obj, bool value) => obj.SetValue(EnableProperty, value);

    public static readonly DependencyProperty StepSizeProperty =
        DependencyProperty.RegisterAttached(
            "StepSize",
            typeof(double),
            typeof(SmoothScroll),
            new PropertyMetadata(130.0));

    public static double GetStepSize(DependencyObject obj) => (double)obj.GetValue(StepSizeProperty);
    public static void SetStepSize(DependencyObject obj, double value) => obj.SetValue(StepSizeProperty, value);

    public static readonly DependencyProperty DecayConstantProperty =
        DependencyProperty.RegisterAttached(
            "DecayConstant",
            typeof(double),
            typeof(SmoothScroll),
            new PropertyMetadata(20.0));

    public static double GetDecayConstant(DependencyObject obj) => (double)obj.GetValue(DecayConstantProperty);
    public static void SetDecayConstant(DependencyObject obj, double value) => obj.SetValue(DecayConstantProperty, value);

    private static readonly DependencyProperty ControllerProperty =
        DependencyProperty.RegisterAttached(
            "Controller",
            typeof(SmoothScrollController),
            typeof(SmoothScroll),
            new PropertyMetadata(null));

    private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer scrollViewer) return;

        var existing = (SmoothScrollController?)scrollViewer.GetValue(ControllerProperty);
        if (existing != null)
        {
            existing.Dispose();
            scrollViewer.ClearValue(ControllerProperty);
        }

        if ((bool)e.NewValue)
        {
            var controller = new SmoothScrollController(scrollViewer);
            scrollViewer.SetValue(ControllerProperty, controller);
        }
    }

    private sealed class SmoothScrollController : IDisposable
    {
        private readonly ScrollViewer _scrollViewer;
        private readonly Stopwatch _stopwatch = new();
        private double _targetOffset;
        private double _lastOffsetSet;
        private bool _isAnimating;

        public SmoothScrollController(ScrollViewer scrollViewer)
        {
            _scrollViewer = scrollViewer;
            _targetOffset = scrollViewer.VerticalOffset;
            _lastOffsetSet = scrollViewer.VerticalOffset;

            _scrollViewer.PreviewMouseWheel += OnPreviewMouseWheel;
            _scrollViewer.ScrollChanged += OnScrollChanged;
            _scrollViewer.Loaded += OnLoaded;
            _scrollViewer.Unloaded += OnUnloaded;
        }

        public void Dispose()
        {
            StopAnimation();
            _scrollViewer.PreviewMouseWheel -= OnPreviewMouseWheel;
            _scrollViewer.ScrollChanged -= OnScrollChanged;
            _scrollViewer.Loaded -= OnLoaded;
            _scrollViewer.Unloaded -= OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _targetOffset = _scrollViewer.VerticalOffset;
            _lastOffsetSet = _scrollViewer.VerticalOffset;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopAnimation();
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isAnimating)
            {
                // Si el offset cambió externamente por más de 1 pixel (arrastre manual del thumb de la barra o teclado),
                // sincronizar de inmediato y detener la animación.
                if (Math.Abs(_scrollViewer.VerticalOffset - _lastOffsetSet) > 1.0)
                {
                    StopAnimation();
                    _targetOffset = _scrollViewer.VerticalOffset;
                }
            }
            else
            {
                _targetOffset = _scrollViewer.VerticalOffset;
            }

            if (_targetOffset > _scrollViewer.ScrollableHeight)
            {
                _targetOffset = _scrollViewer.ScrollableHeight;
            }
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_scrollViewer.ScrollableHeight <= 0)
                return;

            // Si el cursor está sobre un ScrollViewer hijo que REALMENTE tiene capacidad de scroll en esta dirección,
            // permitirle el scroll a ese control interno.
            if (HasActiveScrollableChild(e.OriginalSource as DependencyObject, e.Delta))
                return;

            e.Handled = true;

            double stepSize = GetStepSize(_scrollViewer);
            // Normalizar el delta de la rueda (un tick estándar de ratón Windows es ±120)
            double notches = e.Delta / 120.0;
            double scrollDistance = notches * stepSize;

            if (!_isAnimating)
            {
                _targetOffset = _scrollViewer.VerticalOffset;
            }

            _targetOffset -= scrollDistance;
            _targetOffset = Math.Clamp(_targetOffset, 0, _scrollViewer.ScrollableHeight);

            StartAnimation();
        }

        private bool HasActiveScrollableChild(DependencyObject? source, int delta)
        {
            var current = source;
            while (current != null && current != _scrollViewer)
            {
                if (current is ScrollViewer childSv && childSv != _scrollViewer)
                {
                    if (childSv.ScrollableHeight > 1.0)
                    {
                        if (delta < 0 && childSv.VerticalOffset < childSv.ScrollableHeight - 0.5)
                            return true; // Puede bajar más internamente
                        if (delta > 0 && childSv.VerticalOffset > 0.5)
                            return true; // Puede subir más internamente
                    }
                }
                current = VisualTreeHelperSafe.GetParentSafe(current);
            }
            return false;
        }

        private void StartAnimation()
        {
            if (_isAnimating) return;

            _isAnimating = true;
            _stopwatch.Restart();
            CompositionTarget.Rendering += OnRendering;
        }

        private void StopAnimation()
        {
            if (!_isAnimating) return;

            _isAnimating = false;
            _stopwatch.Stop();
            CompositionTarget.Rendering -= OnRendering;
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            if (!_isAnimating) return;

            double dt = _stopwatch.Elapsed.TotalSeconds;
            _stopwatch.Restart();

            if (dt <= 0) return;
            if (dt > 0.1) dt = 0.1; // Prevenir saltos bruscos si hubo lag de sistema

            double current = _scrollViewer.VerticalOffset;
            double diff = _targetOffset - current;

            if (Math.Abs(diff) < 0.5 || double.IsNaN(diff))
            {
                _scrollViewer.ScrollToVerticalOffset(_targetOffset);
                _lastOffsetSet = _targetOffset;
                StopAnimation();
                return;
            }

            double decay = GetDecayConstant(_scrollViewer);
            double factor = 1.0 - Math.Exp(-decay * dt);
            double step = diff * factor;

            if (Math.Abs(step) < 0.25)
            {
                step = Math.Sign(diff) * 0.25;
            }

            double nextOffset = current + step;
            _lastOffsetSet = nextOffset;
            _scrollViewer.ScrollToVerticalOffset(nextOffset);
        }
    }
}
