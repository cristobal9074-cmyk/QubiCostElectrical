using System.Globalization;
using System.Windows;
using System.Windows.Input;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class QuickAddMaterialDialog : Window
{
    public MaterialRow? Result { get; private set; }

    private readonly decimal valorUfClp;

    public QuickAddMaterialDialog(string? codigo = null, string? descripcion = null, string? unidad = null, string? contexto = null, decimal valorUfClp = 0m)
    {
        InitializeComponent();
        this.valorUfClp = valorUfClp > 0m ? valorUfClp : PriceConversion.DefaultValorUfClp;
        CodigoBox.Text = codigo ?? "";
        DescripcionBox.Text = descripcion ?? "";
        UnidadBox.Text = string.IsNullOrWhiteSpace(unidad) ? "UN" : unidad;
        ContextoText.Text = string.IsNullOrWhiteSpace(contexto)
            ? "Completa los campos y agrega el material al cat\u00e1logo del proyecto."
            : contexto;
        if (!string.IsNullOrWhiteSpace(codigo))
            Title = $"Agregar material al catálogo - {codigo}";
        else if (!string.IsNullOrWhiteSpace(descripcion))
            Title = $"Agregar material al catálogo - {descripcion}";

        Loaded += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(CodigoBox.Text))
            {
                CodigoBox.Focus();
                CodigoBox.SelectAll();
            }
            else
            {
                DescripcionBox.Focus();
                DescripcionBox.SelectAll();
            }
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

        if (!decimal.TryParse(MatClpBox.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal matClp))
            matClp = 0m;
        if (!decimal.TryParse(MoClpBox.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal moClp))
            moClp = 0m;

        // El catálogo de materiales se ingresa y persiste SIEMPRE en pesos chilenos
        // (MatClp/MoClp); MatUf/MoUf es un valor derivado con la UF vigente, igual que en el
        // Banco de Precios. Antes este diálogo escribía directo en MatUf/MoUf y dejaba MatClp=0,
        // por lo que el material aplicado a la cubicación quedaba con precio $0.
        var result = new MaterialRow();
        result.Codigo = codigo;
        result.Descripcion = DescripcionBox.Text.Trim();
        result.Unidad = string.IsNullOrWhiteSpace(UnidadBox.Text) ? "UN" : UnidadBox.Text.Trim();
        result.Categoria = string.IsNullOrWhiteSpace(CategoriaBox.Text) ? "Material" : CategoriaBox.Text.Trim();
        result.MatClp = matClp;
        result.MoClp = moClp;
        result.FechaPrecio = DateTime.Today;
        result.UltimaActualizacionPrecio = AppClock.Now;
        MaterialMoResolver.NormalizarFormulaMaterial(result);
        result.RecalcularUf(valorUfClp);

        Result = result;
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
