using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Epp;

public enum EppGroupingMode
{
    Categoria,
    Cuadrilla,
    Fase
}

public sealed class EppGroupItemVm : ViewModelBase
{
    public string Concepto { get; init; } = "";
    public string Uso { get; init; } = "";
    public decimal Cantidad { get; init; }
    public decimal PrecioUnitario { get; init; }
    public decimal Total => Math.Round(Cantidad * PrecioUnitario);

    public string CantidadLabel => $"{Cantidad:N1}";
    public string PrecioUnitarioLabel => $"$ {PrecioUnitario:N0}";
    public string TotalLabel => $"$ {Total:N0}";
}

public sealed class EppGroupVm : ViewModelBase
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Titulo { get; init; } = "";
    public string Subtitulo { get; init; } = "";
    public string Icon { get; init; } = "👷";
    public string BadgeText { get; init; } = "";
    public string BadgeBrush { get; init; } = "Brush.BlueSoft";
    public string BadgeBorderBrush { get; init; } = "Brush.BlueFocus";
    public string AccentBrush { get; init; } = "Brush.Blue";
    public int Dotacion { get; init; }
    public string DotacionLabel => Dotacion > 0 ? $"{Dotacion} operarios" : "Equipo general";
    public ObservableCollection<EppGroupItemVm> Items { get; } = new();

    public decimal Subtotal => Items.Sum(i => i.Total);
    public string SubtotalLabel => $"$ {Subtotal:N0}";

    public decimal CostoPorTrabajador => Dotacion > 0 ? Math.Round(Subtotal / Dotacion) : 0m;
    public string CostoPorTrabajadorLabel => Dotacion > 0 ? $"$ {CostoPorTrabajador:N0} / trabajador" : "Costo distribuido";

    private double porcentajeTotal;
    public double PorcentajeTotal
    {
        get => porcentajeTotal;
        set
        {
            if (SetProperty(ref porcentajeTotal, value))
            {
                OnPropertyChanged(nameof(PorcentajeLabel));
                OnPropertyChanged(nameof(PorcentajeProgress));
            }
        }
    }

    public string PorcentajeLabel => $"{PorcentajeTotal:P1}";
    public double PorcentajeProgress => Math.Clamp(PorcentajeTotal * 100.0, 0.0, 100.0);
}

public sealed partial class EppViewModel
{
    private EppGroupingMode groupingMode = EppGroupingMode.Categoria;

    public EppGroupingMode GroupingMode
    {
        get => groupingMode;
        set
        {
            if (SetProperty(ref groupingMode, value))
            {
                OnPropertyChanged(nameof(IsGroupingCategoria));
                OnPropertyChanged(nameof(IsGroupingCuadrilla));
                OnPropertyChanged(nameof(IsGroupingFase));
                OnPropertyChanged(nameof(CurrentGroupingTitle));
                OnPropertyChanged(nameof(CurrentGroupingSubtitle));
            }
        }
    }

    public bool IsGroupingCategoria => GroupingMode == EppGroupingMode.Categoria;
    public bool IsGroupingCuadrilla => GroupingMode == EppGroupingMode.Cuadrilla;
    public bool IsGroupingFase => GroupingMode == EppGroupingMode.Fase;

    public string CurrentGroupingTitle => GroupingMode switch
    {
        EppGroupingMode.Cuadrilla => "Desglose de EPP Agrupado por Cuadrilla Operativa",
        EppGroupingMode.Fase => "Desglose de EPP Agrupado por Fase y Avance de Obra",
        _ => "Desglose Estándar por Categorías de Mano de Obra"
    };

    public string CurrentGroupingSubtitle => GroupingMode switch
    {
        EppGroupingMode.Cuadrilla => "Asignación de indumentaria, accesorios y protección individual por especialidad y dotación de faena.",
        EppGroupingMode.Fase => "Distribución temporal del presupuesto de EPP según etapas de ejecución, dotación activa y ciclos de reposición.",
        _ => "Clasificación canónica entre M.O. Directa, M.O. Indirecta y Accesorios Opcionales."
    };

    public ObservableCollection<EppGroupVm> CuadrillaGroups { get; } = new();
    public ObservableCollection<EppGroupVm> FaseGroups { get; } = new();

    public ICommand SelectCategoriaViewCommand => new RelayCommand(() => GroupingMode = EppGroupingMode.Categoria);
    public ICommand SelectCuadrillaViewCommand => new RelayCommand(() => GroupingMode = EppGroupingMode.Cuadrilla);
    public ICommand SelectFaseViewCommand => new RelayCommand(() => GroupingMode = EppGroupingMode.Fase);

    public void RebuildGroupedBreakdowns()
    {
        BuildCuadrillaBreakdown();
        BuildFaseBreakdown();
    }

    private void BuildCuadrillaBreakdown()
    {
        CuadrillaGroups.Clear();

        int totDir = Math.Max(1, DotacionDirecta);
        int totInd = Math.Max(1, DotacionIndirecta);

        // Cuadrilla 1: Montaje y Canalización (~40% dotación directa)
        int dotC1 = Math.Max(1, (int)Math.Round(totDir * 0.40));
        // Cuadrilla 2: Tendido y Cableado (~35% dotación directa)
        int dotC2 = Math.Max(1, (int)Math.Round(totDir * 0.35));
        // Cuadrilla 3: Tableros y Conexionado (~25% dotación directa o residuo)
        int dotC3 = Math.Max(1, totDir - dotC1 - dotC2);
        if (dotC3 <= 0) dotC3 = 1;

        // Cuadrilla 4: Supervisión, Calidad y Prevención APR
        int dotC4 = totInd;

        var g1 = new EppGroupVm
        {
            Titulo = "Cuadrilla 1 — Montaje y Canalización",
            Subtitulo = "Canalizaciones conduit, escalerillas, bandejas portacables y mallas a tierra",
            Icon = "🔧",
            BadgeText = "Faena Gruesa & Soportación",
            BadgeBrush = "Brush.BlueSoft",
            BadgeBorderBrush = "Brush.BlueFocus",
            AccentBrush = "Brush.Blue",
            Dotacion = dotC1
        };

        var g2 = new EppGroupVm
        {
            Titulo = "Cuadrilla 2 — Cableado y Tendido Eléctrico",
            Subtitulo = "Tendido de alimentadores, cableado de fuerza, alumbrado y control en altura",
            Icon = "⚡",
            BadgeText = "Tendido & Trabajo en Altura",
            BadgeBrush = "Brush.GreenSoft",
            BadgeBorderBrush = "Brush.Success",
            AccentBrush = "Brush.Success",
            Dotacion = dotC2
        };

        var g3 = new EppGroupVm
        {
            Titulo = "Cuadrilla 3 — Tableros, Conexionado y Puesta en Marcha",
            Subtitulo = "Peinado de tableros, conexionado fino, protecciones y pruebas de aislamiento",
            Icon = "🎛️",
            BadgeText = "Especialistas & Fuerza",
            BadgeBrush = "Brush.PurpleSoft",
            BadgeBorderBrush = "Brush.Violet",
            AccentBrush = "Brush.Violet",
            Dotacion = dotC3
        };

        var g4 = new EppGroupVm
        {
            Titulo = "Cuadrilla 4 — Supervisión, Jefatura y Prevención APR",
            Subtitulo = "Jefe de faena, supervisores eléctricos, prevencionista y aseguramiento de calidad",
            Icon = "👷‍♂️",
            BadgeText = "Supervisión & APR",
            BadgeBrush = "Brush.AmberSoft",
            BadgeBorderBrush = "Brush.AmberRisk",
            AccentBrush = "Brush.AmberRisk",
            Dotacion = dotC4
        };

        // Distribuir ítems directos entre G1, G2 y G3 según peso de operarios
        decimal f1 = (decimal)dotC1 / totDir;
        decimal f2 = (decimal)dotC2 / totDir;
        decimal f3 = 1m - f1 - f2;

        foreach (var seccion in MoDirectaSecciones)
        {
            foreach (var item in seccion.Items)
            {
                if (item.Cantidad <= 0 || item.PrecioUnitario <= 0)
                    continue;

                decimal c1 = Math.Round(item.Cantidad * f1, 2);
                decimal c2 = Math.Round(item.Cantidad * f2, 2);
                decimal c3 = item.Cantidad - c1 - c2;

                if (c1 > 0)
                    g1.Items.Add(new EppGroupItemVm { Concepto = item.Concepto, Uso = seccion.Titulo, Cantidad = c1, PrecioUnitario = item.PrecioUnitario });
                if (c2 > 0)
                    g2.Items.Add(new EppGroupItemVm { Concepto = item.Concepto, Uso = seccion.Titulo, Cantidad = c2, PrecioUnitario = item.PrecioUnitario });
                if (c3 > 0)
                    g3.Items.Add(new EppGroupItemVm { Concepto = item.Concepto, Uso = seccion.Titulo, Cantidad = c3, PrecioUnitario = item.PrecioUnitario });
            }
        }

        // Asignar M.O. Indirecta íntegra a Cuadrilla 4 (Supervisión)
        foreach (var seccion in MoIndirectaSecciones)
        {
            foreach (var item in seccion.Items)
            {
                if (item.Cantidad <= 0 || item.PrecioUnitario <= 0)
                    continue;

                g4.Items.Add(new EppGroupItemVm { Concepto = item.Concepto, Uso = "Supervisión y Jefatura", Cantidad = item.Cantidad, PrecioUnitario = item.PrecioUnitario });
            }
        }

        // Asignar opcionales según especialidad
        if (OpcionalesIncluidos)
        {
            foreach (var seccion in AccOpcionalesSecciones)
            {
                foreach (var item in seccion.Items)
                {
                    if (item.Cantidad <= 0 || item.PrecioUnitario <= 0)
                        continue;

                    string c = item.Concepto.ToLowerInvariant();
                    if (c.Contains("arnés") || c.Contains("arnes") || c.Contains("cabo"))
                    {
                        // Trabajos en altura: mayormente tendido de alimentadores y canalización elevada
                        decimal qTendido = Math.Round(item.Cantidad * 0.60m, 2);
                        decimal qMontaje = item.Cantidad - qTendido;
                        if (qMontaje > 0) g1.Items.Add(new EppGroupItemVm { Concepto = item.Concepto, Uso = "Montaje Elevado", Cantidad = qMontaje, PrecioUnitario = item.PrecioUnitario });
                        if (qTendido > 0) g2.Items.Add(new EppGroupItemVm { Concepto = item.Concepto, Uso = "Líneas de Vida & Altura", Cantidad = qTendido, PrecioUnitario = item.PrecioUnitario });
                    }
                    else if (c.Contains("masprot") || c.Contains("filtro") || c.Contains("fono"))
                    {
                        // Salas de máquinas y tableros
                        decimal qTableros = Math.Round(item.Cantidad * 0.70m, 2);
                        decimal qSupervision = item.Cantidad - qTableros;
                        if (qTableros > 0) g3.Items.Add(new EppGroupItemVm { Concepto = item.Concepto, Uso = "Salas Técnicas / Ruido", Cantidad = qTableros, PrecioUnitario = item.PrecioUnitario });
                        if (qSupervision > 0) g4.Items.Add(new EppGroupItemVm { Concepto = item.Concepto, Uso = "Inspección de Salas", Cantidad = qSupervision, PrecioUnitario = item.PrecioUnitario });
                    }
                    else
                    {
                        // Protección solar, cubrenuca y otros: terreno y supervisión
                        decimal qMont = Math.Round(item.Cantidad * 0.50m, 2);
                        decimal qTendido = Math.Round(item.Cantidad * 0.30m, 2);
                        decimal qSup = item.Cantidad - qMont - qTendido;
                        if (qMont > 0) g1.Items.Add(new EppGroupItemVm { Concepto = item.Concepto, Uso = "Exposición Solar", Cantidad = qMont, PrecioUnitario = item.PrecioUnitario });
                        if (qTendido > 0) g2.Items.Add(new EppGroupItemVm { Concepto = item.Concepto, Uso = "Exposición Solar", Cantidad = qTendido, PrecioUnitario = item.PrecioUnitario });
                        if (qSup > 0) g4.Items.Add(new EppGroupItemVm { Concepto = item.Concepto, Uso = "Exposición Solar", Cantidad = qSup, PrecioUnitario = item.PrecioUnitario });
                    }
                }
            }
        }

        CuadrillaGroups.Add(g1);
        CuadrillaGroups.Add(g2);
        CuadrillaGroups.Add(g3);
        CuadrillaGroups.Add(g4);

        decimal granTotal = CuadrillaGroups.Sum(g => g.Subtotal);
        if (granTotal > 0)
        {
            foreach (var g in CuadrillaGroups)
                g.PorcentajeTotal = (double)(g.Subtotal / granTotal);
        }
    }

    private void BuildFaseBreakdown()
    {
        FaseGroups.Clear();

        int meses = Math.Max(1, MesesObra);
        int rampUp = Math.Max(1, (int)Math.Round(meses * 0.25));
        int peak = Math.Max(1, (int)Math.Round(meses * 0.50));
        int rampDown = Math.Max(1, meses - rampUp - peak);

        int dotDirecta = Math.Max(1, DotacionDirecta);
        int dotIndirecta = Math.Max(1, DotacionIndirecta);
        int dotPicoTotal = dotDirecta + dotIndirecta;

        int dotFase1 = Math.Max(1, (int)Math.Round(dotPicoTotal * 0.40));
        int dotFase2 = dotPicoTotal;
        int dotFase3 = Math.Max(1, (int)Math.Round(dotPicoTotal * 0.45));

        var f1 = new EppGroupVm
        {
            Titulo = "Fase 1 — Instalación de Faenas y Canalizaciones Iniciales",
            Subtitulo = $"Mes 1 a Mes {rampUp} · Habilitación de obra, mallas de puesta a tierra y canalizaciones principales",
            Icon = "🏗️",
            BadgeText = $"Meses 1–{rampUp} (Inicio)",
            BadgeBrush = "Brush.BlueSoft",
            BadgeBorderBrush = "Brush.BlueFocus",
            AccentBrush = "Brush.Blue",
            Dotacion = dotFase1
        };

        var f2 = new EppGroupVm
        {
            Titulo = "Fase 2 — Montaje Electromecánico y Tendido Masivo (Pico)",
            Subtitulo = $"Mes {rampUp + 1} a Mes {rampUp + peak} · Máxima dotación simultánea, rotación de indumentaria y trabajo en altura",
            Icon = "⚡",
            BadgeText = $"Meses {rampUp + 1}–{rampUp + peak} (Peak)",
            BadgeBrush = "Brush.AmberSoft",
            BadgeBorderBrush = "Brush.AmberRisk",
            AccentBrush = "Brush.AmberRisk",
            Dotacion = dotFase2
        };

        var f3 = new EppGroupVm
        {
            Titulo = "Fase 3 — Conexionado, Pruebas y Certificación SEC TE1",
            Subtitulo = $"Mes {rampUp + peak + 1} a Mes {meses} · Conexionado fino de tableros, calibración, pruebas de carga y entrega",
            Icon = "✅",
            BadgeText = $"Meses {rampUp + peak + 1}–{meses} (Cierre)",
            BadgeBrush = "Brush.GreenSoft",
            BadgeBorderBrush = "Brush.Success",
            AccentBrush = "Brush.Success",
            Dotacion = dotFase3
        };

        // Distribución temporal de entrega y reposición:
        // Fase 1: 30% del volumen de indumentaria y accesorios (entrega inicial de dotación)
        // Fase 2: 50% del volumen (período pico + reposición trimestral de ciclo)
        // Fase 3: 20% del volumen (reposición final de especialistas y puesta en servicio)
        const decimal frac1 = 0.30m;
        const decimal frac2 = 0.50m;

        void AddItemToFases(string concepto, string uso, decimal cantidad, decimal precio)
        {
            if (cantidad <= 0 || precio <= 0) return;

            decimal q1 = Math.Round(cantidad * frac1, 2);
            decimal q2 = Math.Round(cantidad * frac2, 2);
            decimal q3 = cantidad - q1 - q2;

            if (q1 > 0) f1.Items.Add(new EppGroupItemVm { Concepto = concepto, Uso = $"{uso} (Dotación inicial)", Cantidad = q1, PrecioUnitario = precio });
            if (q2 > 0) f2.Items.Add(new EppGroupItemVm { Concepto = concepto, Uso = $"{uso} (Reposición pico)", Cantidad = q2, PrecioUnitario = precio });
            if (q3 > 0) f3.Items.Add(new EppGroupItemVm { Concepto = concepto, Uso = $"{uso} (Cierre de obra)", Cantidad = q3, PrecioUnitario = precio });
        }

        foreach (var s in MoDirectaSecciones)
            foreach (var item in s.Items)
                AddItemToFases(item.Concepto, "M.O. Directa", item.Cantidad, item.PrecioUnitario);

        foreach (var s in MoIndirectaSecciones)
            foreach (var item in s.Items)
                AddItemToFases(item.Concepto, "M.O. Indirecta", item.Cantidad, item.PrecioUnitario);

        if (OpcionalesIncluidos)
        {
            foreach (var s in AccOpcionalesSecciones)
                foreach (var item in s.Items)
                    AddItemToFases(item.Concepto, "Accesorios", item.Cantidad, item.PrecioUnitario);
        }

        FaseGroups.Add(f1);
        FaseGroups.Add(f2);
        FaseGroups.Add(f3);

        decimal granTotal = FaseGroups.Sum(g => g.Subtotal);
        if (granTotal > 0)
        {
            foreach (var g in FaseGroups)
                g.PorcentajeTotal = (double)(g.Subtotal / granTotal);
        }
    }
}
