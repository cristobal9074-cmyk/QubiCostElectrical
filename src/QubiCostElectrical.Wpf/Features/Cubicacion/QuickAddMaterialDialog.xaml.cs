using System.Globalization;
using System.Windows;
using System.Windows.Input;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class QuickAddMaterialDialog : Window
{
    public MaterialRow? Result { get; private set; }

    public QuickAddMaterialDialog(string? codigo = null, string? descripcion = null, string? unidad = null, string? contexto = null)
    {
        InitializeComponent();
        CodigoBox.Text = codigo ?? "";
        DescripcionBox.Text = descripcion ?? "";
        UnidadBox.Text = string.IsNullOrWhiteSpace(unidad) ? "UN" : unidad;
        ContextoText.Text = string.IsNullOrWhiteSpace(contexto)
            ? "Completa los campos y agrega el material al cat\u00e1logo del proyecto."
            : contexto;
        if (!string.IsNullOrWhiteSpace(codigo))
            Title = $"Agregar material al cat\u00e1logo - {codigo}";
        Loaded += (_, _) =>
        {
            CodigoBox.Focus();
            CodigoBox.SelectAll();
        };
    }

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        string codigo = CodeHelper.Normalize(CodigoBox.Text);
        if (string.IsNullOrWhiteSpace(codigo))
        {
            CodigoBox.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Red");
            CodigoBox.Focus();
            return;
        }

        if (!decimal.TryParse(MatUfBox.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var matUf))
            matUf = 0m;
        if (!decimal.TryParse(MoUfBox.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var moUf))
            moUf = 0m;

        Result = new MaterialRow
        {
            Codigo = codigo,
            Descripcion = DescripcionBox.Text.Trim(),
            Unidad = string.IsNullOrWhiteSpace(UnidadBox.Text) ? "UN" : UnidadBox.Text.Trim(),
            Categoria = string.IsNullOrWhiteSpace(CategoriaBox.Text) ? "Material" : CategoriaBox.Text.Trim(),
            MatUf = matUf,
            MoUf = moUf,
            FechaPrecio = DateTime.Today,
            UltimaActualizacionPrecio = AppClock.Now
        };

        DialogResult = true;
    }

    private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
            return;
        }

        if (e.Key == System.Windows.Input.Key.Enter && Keyboard.FocusedElement is not System.Windows.Controls.Button)
        {
            OnAcceptClick(sender, e);
            e.Handled = true;
        }
    }
}
