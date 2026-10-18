using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.ChangeOrders;

public partial class ChangeOrderCubicacionDialog : Window
{
    private readonly ChangeOrderLineVm _line;
    private readonly List<CubicacionRow> _baseProjectCubicacion;
    private readonly IPriceBankStore? _priceBankStore;
    private readonly bool _muestraUf;
    private readonly decimal _ufRate;

    private readonly ObservableCollection<ChangeOrderMaterialVm> _materiales = new();

    public decimal TotalCostoDirectoClp { get; private set; }
    public decimal TotalCostoDirectoUf { get; private set; }
    public decimal CostoUnitarioClp { get; private set; }
    public decimal CostoUnitarioUf { get; private set; }

    public ChangeOrderCubicacionDialog(
        ChangeOrderLineVm line,
        IEnumerable<CubicacionRow>? baseProjectCubicacion,
        IPriceBankStore? priceBankStore,
        bool muestraUf,
        decimal ufRate)
    {
        _line = line ?? throw new ArgumentNullException(nameof(line));
        _baseProjectCubicacion = baseProjectCubicacion != null ? baseProjectCubicacion.ToList() : new List<CubicacionRow>();
        _priceBankStore = priceBankStore;
        _muestraUf = muestraUf;
        _ufRate = ufRate > 0m ? ufRate : PriceConversion.DefaultValorUfClp;

        InitializeComponent();
        Owner = Application.Current?.MainWindow;

        ConfigureView();
        LoadMateriales();
    }

    private void ConfigureView()
    {
        ItemCodeText.Text = $"Partida {_line.Item}";
        DescripcionPartidaText.Text = _line.DescripcionPartida;
        CantidadAdicionalBadge.Text = $"Δ Cantidad: {(_line.CantidadAdicional % 1 == 0 ? Math.Round(_line.CantidadAdicional, MidpointRounding.AwayFromZero).ToString("N0", System.Globalization.CultureInfo.CurrentCulture) : _line.CantidadAdicional.ToString("#,##0.##", System.Globalization.CultureInfo.CurrentCulture))} {_line.Unidad}";

        if (_line.EsContractual)
        {
            TipoBadgeText.Text = "🔒 Partida Contractual (P.U. Congelado)";
            TipoBadgeBorder.Background = (System.Windows.Media.Brush)FindResource("Brush.AmberSoft");
            ChkActualizarPrecioPartida.IsChecked = false;
            ChkActualizarPrecioPartida.IsEnabled = false;
            ChkActualizarPrecioPartida.ToolTip = "El precio unitario de las partidas contractuales no puede modificarse.";
            BtnPrecargarBase.Visibility = Visibility.Visible;
        }
        else
        {
            TipoBadgeText.Text = "✨ Partida Extraordinaria";
            TipoBadgeBorder.Background = (System.Windows.Media.Brush)FindResource("Brush.BlueFaint");
            ChkActualizarPrecioPartida.IsChecked = true;
            ChkActualizarPrecioPartida.IsEnabled = true;
            BtnPrecargarBase.Visibility = Visibility.Collapsed;
        }

        MaterialesGrid.ItemsSource = _materiales;
    }

    private void LoadMateriales()
    {
        _materiales.Clear();
        if (_line.Materiales != null && _line.Materiales.Count > 0)
        {
            foreach (var mat in _line.Materiales)
            {
                var vm = ChangeOrderMaterialVm.FromModel(mat, _muestraUf, _ufRate, RecalculateTotals);
                _materiales.Add(vm);
            }
        }
        RecalculateTotals();
    }

    private void OnAddMaterialClick(object sender, RoutedEventArgs e)
    {
        var vm = new ChangeOrderMaterialVm(RecalculateTotals)
        {
            Tipo = TipoRecurso.MAT,
            Codigo = "",
            Descripcion = "",
            Unidad = "UN",
            Cantidad = 1m,
            CantidadFormula = "1",
            MuestraUf = _muestraUf,
            UfRate = _ufRate
        };
        _materiales.Add(vm);
        MaterialesGrid.SelectedItem = vm;
        RecalculateTotals();
    }

    private void OnPrecargarBaseClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_line.ItemId))
        {
            WpfDialogService.Warning("Sin partida base", "Esta partida no tiene un identificador de contrato vinculado.");
            return;
        }

        // Filtramos filas del presupuesto base para esta partida (con código)
        var rawRows = _baseProjectCubicacion
            .Where(c => string.Equals(c.ItemId, _line.ItemId, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(c.Codigo))
            .ToList();

        // Consolidamos por Código: sumamos cantidades de todas las filas de zona
        // (una partida puede tener varias filas por zona/sub-expresión para el mismo material)
        var grouped = rawRows
            .GroupBy(c => c.Codigo.Trim().ToUpperInvariant())
            .Select(g =>
            {
                var first = g.First();
                return new
                {
                    Tipo = first.Tipo,
                    Codigo = first.Codigo,
                    Descripcion = first.Descripcion,
                    Unidad = first.Unidad,
                    CantidadTotal = g.Sum(r => r.Cantidad),
                    MatClp = first.MatClpConFallback,
                    MoClp = first.MoClpConFallback
                };
            })
            .ToList();

        if (grouped.Count == 0)
        {
            WpfDialogService.Warning("Sin materiales en presupuesto base",
                $"No se encontraron materiales cubicados para la partida {_line.Item} en el presupuesto original.");
            return;
        }

        var confirm = WpfDialogService.Confirm(
            "Precargar materiales del contrato",
            $"Se cargarán {grouped.Count} materiales del presupuesto base escalados a la cantidad adicional ({(_line.CantidadAdicional % 1 == 0 ? Math.Round(_line.CantidadAdicional, MidpointRounding.AwayFromZero).ToString("N0", System.Globalization.CultureInfo.CurrentCulture) : _line.CantidadAdicional.ToString("#,##0.##", System.Globalization.CultureInfo.CurrentCulture))}) {_line.Unidad}).\n\n¿Desea continuar?");

        if (!confirm) return;

        // Ratio para escalar: CantAdicional / CantBase del contrato
        decimal ratio = _line.CantidadBase > 0m
            ? (_line.CantidadAdicional / _line.CantidadBase)
            : 1m;

        _materiales.Clear();
        foreach (var g in grouped)
        {
            decimal cantEscalada = Math.Round(g.CantidadTotal * ratio, 4);
            var vm = new ChangeOrderMaterialVm(RecalculateTotals)
            {
                Tipo = string.IsNullOrWhiteSpace(g.Tipo) ? TipoRecurso.MAT : g.Tipo,
                Codigo = g.Codigo,
                Descripcion = g.Descripcion,
                Unidad = string.IsNullOrWhiteSpace(g.Unidad) ? "UN" : g.Unidad,
                Cantidad = cantEscalada,
                CantidadFormula = cantEscalada.ToString("G29", System.Globalization.CultureInfo.InvariantCulture),
                MatClp = g.MatClp,
                MoClp = g.MoClp,
                MuestraUf = _muestraUf,
                UfRate = _ufRate
            };
            vm.Recalculate();
            _materiales.Add(vm);
        }

        RecalculateTotals();
    }

    private void OnBuscarEnBancoClick(object sender, RoutedEventArgs e)
    {
        if (_priceBankStore == null)
        {
            WpfDialogService.Warning("Banco de precios", "El banco de precios no está disponible.");
            return;
        }

        // Si hay una fila seleccionada vacía, la usamos; de lo contrario agregamos una nueva
        var current = MaterialesGrid.SelectedItem as ChangeOrderMaterialVm;
        if (current == null || !string.IsNullOrWhiteSpace(current.Codigo))
        {
            current = new ChangeOrderMaterialVm(RecalculateTotals)
            {
                Tipo = TipoRecurso.MAT,
                Unidad = "UN",
                Cantidad = 1m,
                CantidadFormula = "1",
                MuestraUf = _muestraUf,
                UfRate = _ufRate
            };
            _materiales.Add(current);
            MaterialesGrid.SelectedItem = current;
        }

        WpfDialogService.Info("Banco de precios",
            "Ingrese el código del material en la celda 'Código' o edite la descripción y precio directamente.\n" +
            "Al presionar Enter o cambiar de celda, el sistema buscará y auto-completará los datos desde el banco de precios.");
    }

    private void OnRemoveMaterialClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ChangeOrderMaterialVm vm)
        {
            _materiales.Remove(vm);
            RecalculateTotals();
        }
    }

    private void OnCodigoCellLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is ChangeOrderMaterialVm vm)
        {
            vm.ResolveFromPriceBank(_priceBankStore);
            RecalculateTotals();
        }
    }

    private void OnCantidadCellLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is ChangeOrderMaterialVm vm)
        {
            vm.EvaluateFormula();
            RecalculateTotals();
        }
    }

    private void RecalculateTotals()
    {
        decimal totMatClp = 0m;
        decimal totMoClp = 0m;

        foreach (var m in _materiales)
        {
            totMatClp += Math.Round(m.Cantidad * m.MatClp, 0, MidpointRounding.AwayFromZero);
            totMoClp += Math.Round(m.Cantidad * m.MoClp, 0, MidpointRounding.AwayFromZero);
        }

        TotalCostoDirectoClp = totMatClp + totMoClp;
        TotalCostoDirectoUf = _ufRate > 0m ? PriceConversion.ClpToUf(TotalCostoDirectoClp, _ufRate) : 0m;

        CostoUnitarioClp = _line.CantidadAdicional > 0m
            ? Math.Round(TotalCostoDirectoClp / _line.CantidadAdicional, 0, MidpointRounding.AwayFromZero)
            : 0m;

        CostoUnitarioUf = _line.CantidadAdicional > 0m
            ? Math.Round(TotalCostoDirectoUf / _line.CantidadAdicional, 4)
            : 0m;

        if (_muestraUf)
        {
            var matUf = _ufRate > 0m ? PriceConversion.ClpToUf(totMatClp, _ufRate) : 0m;
            var moUf = _ufRate > 0m ? PriceConversion.ClpToUf(totMoClp, _ufRate) : 0m;

            TotalMaterialesText.Text = $"{matUf:N3} UF";
            TotalMoText.Text = $"{moUf:N3} UF";
            TotalCostoDirectoText.Text = $"{TotalCostoDirectoUf:N3} UF";
            CostoUnitarioPartidaText.Text = $"{CostoUnitarioUf:N3} UF / {_line.Unidad}";
        }
        else
        {
            TotalMaterialesText.Text = $"$ {totMatClp:N0}";
            TotalMoText.Text = $"$ {totMoClp:N0}";
            TotalCostoDirectoText.Text = $"$ {TotalCostoDirectoClp:N0}";
            CostoUnitarioPartidaText.Text = $"$ {CostoUnitarioClp:N0} / {_line.Unidad}";
        }
    }

    private void OnGuardarClick(object sender, RoutedEventArgs e)
    {
        // Guardar la lista de cubicación en la línea del adicional
        var resultRows = _materiales
            .Where(m => !string.IsNullOrWhiteSpace(m.Codigo) || !string.IsNullOrWhiteSpace(m.Descripcion))
            .Select(m => m.ToModel(_line.ItemId))
            .ToList();

        _line.SetMateriales(resultRows);

        // Si es extraordinaria y el usuario marcó actualizar precio
        if (!_line.EsContractual && ChkActualizarPrecioPartida.IsChecked == true && CostoUnitarioClp > 0m)
        {
            _line.PrecioUnitarioClp = CostoUnitarioClp;
            _line.PrecioUnitarioUf = CostoUnitarioUf;
            if (_muestraUf)
                _line.PrecioUnitarioText = CostoUnitarioUf.ToString("N3", System.Globalization.CultureInfo.InvariantCulture);
            else
                _line.PrecioUnitarioText = CostoUnitarioClp.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        }

        DialogResult = true;
    }

    public static bool Show(
        ChangeOrderLineVm line,
        IEnumerable<CubicacionRow>? baseProjectCubicacion,
        IPriceBankStore? priceBankStore,
        bool muestraUf,
        decimal ufRate)
    {
        var dlg = new ChangeOrderCubicacionDialog(line, baseProjectCubicacion, priceBankStore, muestraUf, ufRate);
        return dlg.ShowDialog() == true;
    }
}
