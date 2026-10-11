using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class Building3DViewport : UserControl
{
    private int _subterraneos = 2;
    private int _torresCount = 1;
    private int _pisosComunes = 0;
    private bool _subComun = true;
    private List<TorreZonaConfig> _torres = new();
    private int _totalZonas = 23;

    public Building3DViewport()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Redraw();
        Loaded += (_, _) => Redraw();
    }

    public void UpdateModel(
        int subterraneos,
        int torresCount,
        int pisosComunes,
        bool subComun,
        IReadOnlyList<TorreZonaConfig> torres,
        int totalZonas)
    {
        _subterraneos = Math.Max(0, subterraneos);
        _torresCount = Math.Max(1, torresCount);
        _pisosComunes = Math.Max(0, pisosComunes);
        _subComun = subComun;
        _torres = torres.ToList();
        _totalZonas = totalZonas;

        Redraw();
    }

    private void Redraw()
    {
        if (ActualWidth < 50 || ActualHeight < 50)
            return;

        RenderCanvas.Children.Clear();
        BackgroundGridCanvas.Children.Clear();

        DrawBackgroundGrid();

        double canvasW = ActualWidth;
        double canvasH = ActualHeight;

        int maxPisos = _torres.Count > 0 ? _torres.Max(t => t.Pisos) : 20;
        int maxCubiertas = _torres.Count > 0 ? _torres.Max(t => t.Cubiertas) : 1;
        double alturaTotalMts = (maxPisos * 3.0) + (maxCubiertas * 2.5);
        double cotaSubMts = _subterraneos * 3.0;

        TxtAlturaTotal.Text = $"+{alturaTotalMts:F1} m";
        TxtCotaSub.Text = cotaSubMts > 0 ? $"-{cotaSubMts:F1} m" : "0.0 m";
        TxtZonasCount.Text = $"{_totalZonas} ZONAS";

        // Actualizar escala badge si todos los pisos son compartidos
        if (_torresCount > 1 && _pisosComunes >= maxPisos && maxPisos > 0)
        {
            TxtScaleBadge.Text = "★ TORRES 100% COMPARTIDAS";
        }
        else if (_torresCount > 1 && _pisosComunes > 0)
        {
            TxtScaleBadge.Text = $"PODIO COMPARTIDO (P1-P{_pisosComunes})";
        }
        else
        {
            TxtScaleBadge.Text = "ESCALA 1:100";
        }

        // Centro y escala
        double groundY = canvasH * 0.65; // Nivel de suelo natural (±0.00)
        double centerX = canvasW * 0.48;

        // Escala vertical adaptable
        double totalHeightUnits = maxPisos + _subterraneos + 4;
        double unitH = Math.Clamp((canvasH * 0.68) / Math.Max(totalHeightUnits, 12), 4.5, 14.0);

        // 1. Dibujar plano de terreno / cota ±0.00
        DrawGroundPlane(centerX, groundY, canvasW);

        // 2. Dibujar subterráneos (en corte bajo rasante)
        if (_subterraneos > 0)
        {
            DrawBasements(centerX, groundY, unitH);
        }

        // 3. Dibujar torres y podio de pisos compartidos
        DrawTowersAndSharedPodium(centerX, groundY, unitH, maxPisos);

        // 4. Dibujar cotas arquitectónicas verticales
        DrawElevationQuotes(centerX, groundY, unitH, maxPisos, alturaTotalMts, cotaSubMts);
    }

    private void DrawBackgroundGrid()
    {
        double w = ActualWidth;
        double h = ActualHeight;
        var stroke = new SolidColorBrush(Color.FromRgb(56, 189, 248));

        for (double x = 0; x < w; x += 30)
        {
            var line = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = h,
                Stroke = stroke,
                StrokeThickness = 0.5
            };
            BackgroundGridCanvas.Children.Add(line);
        }
        for (double y = 0; y < h; y += 30)
        {
            var line = new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = w,
                Y2 = y,
                Stroke = stroke,
                StrokeThickness = 0.5
            };
            BackgroundGridCanvas.Children.Add(line);
        }
    }

    private void DrawGroundPlane(double cx, double gy, double cw)
    {
        var groundLine = new Line
        {
            X1 = 20,
            Y1 = gy,
            X2 = cw - 20,
            Y2 = gy,
            Stroke = new SolidColorBrush(Color.FromArgb(180, 16, 185, 129)),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 6, 3 }
        };
        RenderCanvas.Children.Add(groundLine);

        var groundLabel = new TextBlock
        {
            Text = "±0.00 NPT TERRENO NATURAL",
            FontSize = 9,
            FontFamily = new FontFamily("Consolas, monospace"),
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129))
        };
        Canvas.SetLeft(groundLabel, 26);
        Canvas.SetTop(groundLabel, gy - 14);
        RenderCanvas.Children.Add(groundLabel);
    }

    private void DrawBasements(double cx, double gy, double unitH)
    {
        double subW = 160 + (_torresCount - 1) * 75;
        double subH = _subterraneos * unitH;
        double isoDx = 35;
        double isoDy = 18;

        double leftX = cx - subW / 2;

        // Front face de subterráneos
        var frontPoly = new Polygon
        {
            Points = new PointCollection
            {
                new Point(leftX, gy),
                new Point(leftX + subW, gy),
                new Point(leftX + subW, gy + subH),
                new Point(leftX, gy + subH)
            },
            Fill = new LinearGradientBrush(
                Color.FromRgb(30, 41, 59),
                Color.FromRgb(15, 23, 42),
                new Point(0, 0), new Point(0, 1)),
            Stroke = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
            StrokeThickness = 1
        };
        RenderCanvas.Children.Add(frontPoly);

        // Right depth face
        var rightPoly = new Polygon
        {
            Points = new PointCollection
            {
                new Point(leftX + subW, gy),
                new Point(leftX + subW + isoDx, gy - isoDy),
                new Point(leftX + subW + isoDx, gy + subH - isoDy),
                new Point(leftX + subW, gy + subH)
            },
            Fill = new LinearGradientBrush(
                Color.FromRgb(15, 23, 42),
                Color.FromRgb(10, 15, 30),
                new Point(0, 0), new Point(1, 1)),
            Stroke = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            StrokeThickness = 1
        };
        RenderCanvas.Children.Add(rightPoly);

        // Líneas de losa de cada subterráneo
        for (int s = 1; s <= _subterraneos; s++)
        {
            double lineY = gy + s * unitH;
            var subLine = new Line
            {
                X1 = leftX,
                Y1 = lineY,
                X2 = leftX + subW,
                Y2 = lineY,
                Stroke = new SolidColorBrush(Color.FromArgb(120, 148, 163, 184)),
                StrokeThickness = 0.8
            };
            RenderCanvas.Children.Add(subLine);

            string subLabel = _subComun && _torresCount > 1 ? $"S{s} (Común)" : $"S{s}";
            var lbl = new TextBlock
            {
                Text = subLabel,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas, monospace"),
                Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11))
            };
            Canvas.SetLeft(lbl, leftX + 8);
            Canvas.SetTop(lbl, lineY - unitH + 2);
            RenderCanvas.Children.Add(lbl);
        }
    }

    private void DrawTowersAndSharedPodium(double cx, double gy, double unitH, int maxPisos)
    {
        double towerW = _torresCount == 1 ? 120 : 65;
        double gap = _torresCount == 1 ? 0 : 25;
        double totalSpan = _torresCount * towerW + (_torresCount - 1) * gap;
        double startX = cx - totalSpan / 2;

        double isoDx = 30;
        double isoDy = 16;

        int pisosCompartidosEfectivos = _torresCount > 1 ? Math.Min(_pisosComunes, maxPisos) : 0;
        bool allShared = _torresCount > 1 && _pisosComunes >= maxPisos && maxPisos > 0;
        double podioTopY = gy - pisosCompartidosEfectivos * unitH;

        // ── A. DIBUJAR PODIO / PISOS COMPARTIDOS SI CORRESPONDE ────────────
        if (pisosCompartidosEfectivos > 0)
        {
            double podioW = totalSpan + 20;
            double podioLeftX = cx - podioW / 2;
            double podioH = pisosCompartidosEfectivos * unitH;

            // 1. Cara Frontal del Podio (Cian arquitectónico / Placa unificada)
            var podioFrontGrad = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            podioFrontGrad.GradientStops.Add(new GradientStop(Color.FromRgb(2, 132, 199), 0.0));
            podioFrontGrad.GradientStops.Add(new GradientStop(Color.FromRgb(3, 105, 161), 1.0));

            var podioFront = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(podioLeftX, podioTopY),
                    new Point(podioLeftX + podioW, podioTopY),
                    new Point(podioLeftX + podioW, gy),
                    new Point(podioLeftX, gy)
                },
                Fill = podioFrontGrad,
                Stroke = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                StrokeThickness = 1.5
            };
            RenderCanvas.Children.Add(podioFront);

            // 2. Cara Lateral Derecha del Podio
            var podioRight = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(podioLeftX + podioW, podioTopY),
                    new Point(podioLeftX + podioW + isoDx, podioTopY - isoDy),
                    new Point(podioLeftX + podioW + isoDx, gy - isoDy),
                    new Point(podioLeftX + podioW, gy)
                },
                Fill = new LinearGradientBrush(
                    Color.FromRgb(7, 89, 133),
                    Color.FromRgb(12, 74, 110),
                    new Point(0, 0), new Point(1, 1)),
                Stroke = new SolidColorBrush(Color.FromRgb(14, 165, 233)),
                StrokeThickness = 1.2
            };
            RenderCanvas.Children.Add(podioRight);

            // 3. Cara Superior del Podio (Terraza / Losa conectora)
            var podioTop = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(podioLeftX, podioTopY),
                    new Point(podioLeftX + isoDx, podioTopY - isoDy),
                    new Point(podioLeftX + podioW + isoDx, podioTopY - isoDy),
                    new Point(podioLeftX + podioW, podioTopY)
                },
                Fill = new SolidColorBrush(Color.FromRgb(186, 230, 253)),
                Stroke = new SolidColorBrush(Color.FromRgb(224, 242, 254)),
                StrokeThickness = 1
            };
            RenderCanvas.Children.Add(podioTop);

            // 4. Líneas de losas en los pisos comunes
            for (int p = 1; p <= pisosCompartidosEfectivos; p++)
            {
                double fy = gy - p * unitH;
                if (fy > podioTopY)
                {
                    var fLine = new Line
                    {
                        X1 = podioLeftX,
                        Y1 = fy,
                        X2 = podioLeftX + podioW,
                        Y2 = fy,
                        Stroke = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)),
                        StrokeThickness = 0.8
                    };
                    RenderCanvas.Children.Add(fLine);
                }

                var floorLbl = new TextBlock
                {
                    Text = $"P{p}",
                    FontSize = 8.5,
                    FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Consolas, monospace"),
                    Foreground = Brushes.White
                };
                Canvas.SetLeft(floorLbl, podioLeftX + 6);
                Canvas.SetTop(floorLbl, gy - p * unitH + 2);
                RenderCanvas.Children.Add(floorLbl);
            }

            // Rótulo distintivo en el podio
            string podioTitulo = allShared
                ? $"★ ESTRUCTURA 100% COMPARTIDA ({_torresCount} CUERPOS · P1 A P{pisosCompartidosEfectivos})"
                : $"PODIO COMPARTIDO (P1 A P{pisosCompartidosEfectivos})";

            var podioBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2)
            };
            var podioText = new TextBlock
            {
                Text = podioTitulo,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas, monospace"),
                Foreground = new SolidColorBrush(Color.FromRgb(125, 211, 252))
            };
            podioBadge.Child = podioText;
            Canvas.SetLeft(podioBadge, cx - (allShared ? 140 : 95));
            Canvas.SetTop(podioBadge, podioTopY + 4);
            RenderCanvas.Children.Add(podioBadge);
        }

        // ── B. DIBUJAR CADA TORRE ──────────────────────────────────────────
        for (int i = 0; i < _torresCount; i++)
        {
            double tx = startX + i * (towerW + gap);
            int pisos = i < _torres.Count ? _torres[i].Pisos : 20;
            int cubiertas = i < _torres.Count ? _torres[i].Cubiertas : 1;
            string torreNombre = i < _torres.Count ? _torres[i].Nombre : $"Torre {i + 1}";

            double torreBaseY = pisosCompartidosEfectivos > 0 ? podioTopY : gy;
            int pisosIndependientes = Math.Max(0, pisos - pisosCompartidosEfectivos);

            double topY = torreBaseY - (pisosIndependientes * unitH);

            // Si hay pisos independientes que sobresalen del podio
            if (pisosIndependientes > 0)
            {
                // 1. Cara frontal (Cristal / Muro cortina de la torre independiente)
                var frontGrad = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1)
                };
                frontGrad.GradientStops.Add(new GradientStop(Color.FromRgb(59, 130, 246), 0.0));
                frontGrad.GradientStops.Add(new GradientStop(Color.FromRgb(29, 78, 216), 1.0));

                var frontPoly = new Polygon
                {
                    Points = new PointCollection
                    {
                        new Point(tx, topY),
                        new Point(tx + towerW, topY),
                        new Point(tx + towerW, torreBaseY),
                        new Point(tx, torreBaseY)
                    },
                    Fill = frontGrad,
                    Stroke = new SolidColorBrush(Color.FromRgb(147, 197, 253)),
                    StrokeThickness = 1.2
                };
                RenderCanvas.Children.Add(frontPoly);

                // 2. Cara lateral derecha (Sombra volumétrica isométrica)
                var rightPoly = new Polygon
                {
                    Points = new PointCollection
                    {
                        new Point(tx + towerW, topY),
                        new Point(tx + towerW + isoDx, topY - isoDy),
                        new Point(tx + towerW + isoDx, torreBaseY - isoDy),
                        new Point(tx + towerW, torreBaseY)
                    },
                    Fill = new LinearGradientBrush(
                        Color.FromRgb(30, 58, 138),
                        Color.FromRgb(23, 37, 84),
                        new Point(0, 0), new Point(1, 1)),
                    Stroke = new SolidColorBrush(Color.FromRgb(96, 165, 250)),
                    StrokeThickness = 1
                };
                RenderCanvas.Children.Add(rightPoly);

                // 3. Cara superior (Techo / Losa de Azotea)
                var topPoly = new Polygon
                {
                    Points = new PointCollection
                    {
                        new Point(tx, topY),
                        new Point(tx + isoDx, topY - isoDy),
                        new Point(tx + towerW + isoDx, topY - isoDy),
                        new Point(tx + towerW, topY)
                    },
                    Fill = new SolidColorBrush(Color.FromRgb(191, 219, 254)),
                    Stroke = new SolidColorBrush(Color.FromRgb(224, 231, 255)),
                    StrokeThickness = 1
                };
                RenderCanvas.Children.Add(topPoly);

                // 4. Líneas horizontales de losas
                int step = pisosIndependientes > 25 ? 3 : (pisosIndependientes > 12 ? 2 : 1);
                for (int p = 1; p < pisosIndependientes; p += step)
                {
                    double floorY = torreBaseY - p * unitH;
                    var floorLine = new Line
                    {
                        X1 = tx,
                        Y1 = floorY,
                        X2 = tx + towerW,
                        Y2 = floorY,
                        Stroke = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)),
                        StrokeThickness = 0.6
                    };
                    RenderCanvas.Children.Add(floorLine);
                }
            }

            // Remate de caseta técnica / cubierta
            if (cubiertas > 0)
            {
                double casetaW = towerW * 0.45;
                double casetaH = 10;
                double casetaX = tx + (towerW - casetaW) / 2;
                double casetaTopY = topY - casetaH;

                var caseta = new Polygon
                {
                    Points = new PointCollection
                    {
                        new Point(casetaX, casetaTopY),
                        new Point(casetaX + casetaW, casetaTopY),
                        new Point(casetaX + casetaW, topY),
                        new Point(casetaX, topY)
                    },
                    Fill = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                    Stroke = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    StrokeThickness = 1
                };
                RenderCanvas.Children.Add(caseta);
            }

            // Etiqueta de la Torre
            var towerLabel = new TextBlock
            {
                Text = torreNombre,
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            Canvas.SetLeft(towerLabel, tx + 6);
            Canvas.SetTop(towerLabel, topY + 4);
            RenderCanvas.Children.Add(towerLabel);
        }
    }

    private void DrawElevationQuotes(double cx, double gy, double unitH, int maxPisos, double alturaTotal, double cotaSub)
    {
        double quoteX = ActualWidth - 65;
        double topY = gy - maxPisos * unitH;
        double botY = gy + _subterraneos * unitH;

        // Línea vertical de cota
        var vLine = new Line
        {
            X1 = quoteX,
            Y1 = topY,
            X2 = quoteX,
            Y2 = botY,
            Stroke = new SolidColorBrush(Color.FromRgb(96, 165, 250)),
            StrokeThickness = 1
        };
        RenderCanvas.Children.Add(vLine);

        // Marca superior (Azotea)
        var topTick = new Line { X1 = quoteX - 6, Y1 = topY, X2 = quoteX + 6, Y2 = topY, Stroke = vLine.Stroke, StrokeThickness = 1.5 };
        RenderCanvas.Children.Add(topTick);
        var topText = new TextBlock
        {
            Text = $"+{alturaTotal:F0}m\nAzotea",
            FontSize = 8.5,
            FontFamily = new FontFamily("Consolas, monospace"),
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248))
        };
        Canvas.SetLeft(topText, quoteX - 48);
        Canvas.SetTop(topText, topY - 10);
        RenderCanvas.Children.Add(topText);

        // Marca intermedia de Podio Compartido si existe
        if (_torresCount > 1 && _pisosComunes > 0)
        {
            double podioQuoteY = gy - Math.Min(_pisosComunes, maxPisos) * unitH;
            var podioTick = new Line { X1 = quoteX - 6, Y1 = podioQuoteY, X2 = quoteX + 6, Y2 = podioQuoteY, Stroke = new SolidColorBrush(Color.FromRgb(56, 189, 248)), StrokeThickness = 1.5 };
            RenderCanvas.Children.Add(podioTick);

            var podioText = new TextBlock
            {
                Text = $"+{Math.Min(_pisosComunes, maxPisos) * 3:F0}m\nPodio",
                FontSize = 8.5,
                FontFamily = new FontFamily("Consolas, monospace"),
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248))
            };
            Canvas.SetLeft(podioText, quoteX - 44);
            Canvas.SetTop(podioText, podioQuoteY - 8);
            RenderCanvas.Children.Add(podioText);
        }

        // Marca de suelo (±0.00)
        var groundTick = new Line { X1 = quoteX - 6, Y1 = gy, X2 = quoteX + 6, Y2 = gy, Stroke = new SolidColorBrush(Color.FromRgb(16, 185, 129)), StrokeThickness = 1.5 };
        RenderCanvas.Children.Add(groundTick);
        var groundText = new TextBlock
        {
            Text = "±0.00",
            FontSize = 8.5,
            FontFamily = new FontFamily("Consolas, monospace"),
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129))
        };
        Canvas.SetLeft(groundText, quoteX - 36);
        Canvas.SetTop(groundText, gy - 6);
        RenderCanvas.Children.Add(groundText);

        // Marca de subterráneo más bajo
        if (_subterraneos > 0)
        {
            var botTick = new Line { X1 = quoteX - 6, Y1 = botY, X2 = quoteX + 6, Y2 = botY, Stroke = new SolidColorBrush(Color.FromRgb(245, 158, 11)), StrokeThickness = 1.5 };
            RenderCanvas.Children.Add(botTick);
            var botText = new TextBlock
            {
                Text = $"-{cotaSub:F0}m\nFundac.",
                FontSize = 8.5,
                FontFamily = new FontFamily("Consolas, monospace"),
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11))
            };
            Canvas.SetLeft(botText, quoteX - 48);
            Canvas.SetTop(botText, botY - 8);
            RenderCanvas.Children.Add(botText);
        }
    }
}
