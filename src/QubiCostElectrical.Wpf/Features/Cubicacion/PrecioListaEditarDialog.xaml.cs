using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class PrecioListaEditarDialog : Window
{
    private readonly PrecioListaResumenVm vm;

    /// <summary>Código del material creado, para que la ventana que lo abrió pueda encadenarlo a la cubicación.</summary>
    public string? CodigoCreado { get; private set; }

    public PrecioListaEditarDialog(
        PrecioListaResumenVm vm,
        PrecioListaMaterialVm? _ = null,
        string? initialCodigo = null,
        string? initialDescripcion = null,
        string? initialUnidad = null)
    {
        InitializeComponent();
        this.vm = vm;
        FechaPicker.SelectedDate = DateTime.Today;

        if (!string.IsNullOrWhiteSpace(initialCodigo))
            CodigoBox.Text = initialCodigo;

        if (!string.IsNullOrWhiteSpace(initialDescripcion))
            DescripcionBox.Text = initialDescripcion;

        if (!string.IsNullOrWhiteSpace(initialUnidad))
        {
            bool found = false;
            foreach (ComboBoxItem item in UnidadCombo.Items)
            {
                if (string.Equals(item.Content as string, initialUnidad, StringComparison.OrdinalIgnoreCase))
                {
                    UnidadCombo.SelectedItem = item;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                var customItem = new ComboBoxItem { Content = initialUnidad };
                UnidadCombo.Items.Add(customItem);
                UnidadCombo.SelectedItem = customItem;
            }
        }

        var textboxes = new[] { CodigoBox, DescripcionBox, CategoriaBox, MatClpBox, MoClpBox, ProveedorBox };
        foreach (var tb in textboxes)
        {
            tb.PreviewKeyDown += OnFieldKeyDown;
            tb.GotKeyboardFocus += OnTextBoxGotKeyboardFocus;
            tb.PreviewMouseLeftButtonDown += OnTextBoxPreviewMouseLeftButtonDown;
        }

        UnidadCombo.PreviewKeyDown += OnFieldKeyDown;
        FechaPicker.PreviewKeyDown += OnFieldKeyDown;

        Loaded += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(CodigoBox.Text))
            {
                CodigoBox.Focus();
                CodigoBox.SelectAll();
            }
            else if (string.IsNullOrWhiteSpace(DescripcionBox.Text))
            {
                DescripcionBox.Focus();
                DescripcionBox.SelectAll();
            }
            else
            {
                MatClpBox.Focus();
                MatClpBox.SelectAll();
            }
        };
    }

    private void OnTextBoxGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.SelectAll();
        }
    }

    private void OnTextBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBox tb && !tb.IsKeyboardFocusWithin)
        {
            tb.Focus();
            tb.SelectAll();
            e.Handled = true;
        }
    }

    private void OnFieldKeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+Enter: Guardar inmediato desde cualquier campo
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            e.Handled = true;
            OnGuardarClick(sender, e);
            return;
        }

        if (e.Key == Key.Enter)
        {
            // Enter en MoClpBox o ProveedorBox confirma el guardado directamente
            if (ReferenceEquals(sender, MoClpBox) || ReferenceEquals(sender, ProveedorBox))
            {
                e.Handled = true;
                OnGuardarClick(sender, e);
                return;
            }

            MoveFocusField(sender as UIElement, forward: true);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            if (sender is ComboBox combo && combo.IsDropDownOpen)
                return;

            MoveFocusField(sender as UIElement, forward: true);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            if (sender is ComboBox combo && combo.IsDropDownOpen)
                return;

            MoveFocusField(sender as UIElement, forward: false);
            e.Handled = true;
            return;
        }
    }

    private void MoveFocusField(UIElement? current, bool forward)
    {
        var fields = new UIElement[]
        {
            CodigoBox,
            DescripcionBox,
            UnidadCombo,
            CategoriaBox,
            MatClpBox,
            MoClpBox,
            ProveedorBox,
            FechaPicker
        };

        int idx = Array.IndexOf(fields, current);
        if (idx < 0) return;

        int nextIdx = forward ? idx + 1 : idx - 1;
        if (nextIdx >= 0 && nextIdx < fields.Length)
        {
            var next = fields[nextIdx];
            next.Focus();
            if (next is TextBox tb)
            {
                tb.SelectAll();
            }
        }
    }

    private static decimal ParseDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0m;
        var clean = text.Trim().Replace("$", "").Trim();
        if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out var v))
            return v;
        if (decimal.TryParse(clean.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out v))
            return v;
        return 0m;
    }

    private void OnGuardarClick(object sender, RoutedEventArgs e)
    {
        var codigo = CodigoBox.Text.Trim();
        var descripcion = DescripcionBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(codigo))
        {
            WpfDialogService.Info("Campo requerido", "El código del material es obligatorio.");
            CodigoBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(descripcion))
        {
            WpfDialogService.Info("Campo requerido", "La descripción del material es obligatoria.");
            DescripcionBox.Focus();
            return;
        }

        var matClp = ParseDecimal(MatClpBox.Text);
        var moClp = ParseDecimal(MoClpBox.Text);

        var unidad = UnidadCombo.SelectedItem is ComboBoxItem item
            ? (item.Content as string) ?? "un"
            : "un";

        var formulaTipo = moClp > 0m ? MaterialMoResolver.MoFormulaFactor : "";
        if (!vm.AgregarMaterial(
                codigo,
                descripcion,
                unidad,
                CategoriaBox.Text.Trim(),
                matClp,
                moClp,
                ProveedorBox.Text.Trim(),
                FechaPicker.SelectedDate ?? DateTime.Today,
                formulaTipo))
        {
            // El aviso de código duplicado ya lo mostró AgregarMaterial: se deja el formulario abierto
            // para corregir el código en vez de cerrar perdiendo lo escrito.
            CodigoBox.Focus();
            CodigoBox.SelectAll();
            return;
        }

        CodigoCreado = codigo;
        DialogResult = true;
    }
}
