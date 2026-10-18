using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.ManoDeObra;

public sealed class AddCargoMoDialogResult
{
    public string Nombre { get; init; } = "";
    public string Tag { get; init; } = "";
    public decimal SueldoBase { get; init; }
    public decimal FraccionObra { get; init; } = 1m;
    public int Dotacion { get; init; } = 1;
}

public partial class AddCargoMoDialog : Window
{
    private static readonly CultureInfo EsCl = CultureInfo.GetCultureInfo("es-CL");
    private readonly bool isDirecta;
    private readonly Func<string, bool> tagExistsCheck;
    private bool tagManuallyEdited;
    private bool updatingTagAutomatically;

    public AddCargoMoDialogResult? Result { get; private set; }

    public AddCargoMoDialog(bool isDirecta, string suggestedTag, decimal defaultSueldo, Func<string, bool> tagExistsCheck)
    {
        this.isDirecta = isDirecta;
        this.tagExistsCheck = tagExistsCheck;
        InitializeComponent();

        if (isDirecta)
        {
            Title = "Agregar Puesto - MO Directa";
            HeaderTitleText.Text = "Nuevo Puesto (MO Directa)";
            HeaderSubtitleText.Text = "La dotación seguirá la curva del proyecto y el reparto proporcional de la Proyección MO.";
            IndirectaFieldsGrid.Visibility = Visibility.Collapsed;
            InfoText.Text = "💡 El puesto se integrará a la dotación de terreno, al reparto de la proyección mensual y a la indumentaria EPP.";
        }
        else
        {
            Title = "Agregar Puesto - MO Indirecta";
            HeaderTitleText.Text = "Nuevo Puesto (MO Indirecta)";
            HeaderSubtitleText.Text = "Puesto de supervisión o administración con dotación fija y fracción de dedicación a la obra.";
            IndirectaFieldsGrid.Visibility = Visibility.Visible;
            InfoText.Text = "💡 Puesto fijo todos los meses. La fracción (0..1) define qué porcentaje del sueldo y leyes se carga a esta obra.";
        }

        updatingTagAutomatically = true;
        TagBox.Text = suggestedTag;
        updatingTagAutomatically = false;
        SueldoBox.Text = Math.Round(defaultSueldo).ToString("N0", EsCl);
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        NombreBox.Focus();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }

    private void OnTagTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!updatingTagAutomatically)
            tagManuallyEdited = true;
    }

    private void OnNombreTextChanged(object sender, TextChangedEventArgs e)
    {
        if (tagManuallyEdited)
            return;

        var name = (NombreBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            return;

        var tag = GenerateTagFromName(name, isDirecta);
        if (!string.IsNullOrWhiteSpace(tag))
        {
            updatingTagAutomatically = true;
            TagBox.Text = tag;
            updatingTagAutomatically = false;
        }
    }

    private static string GenerateTagFromName(string name, bool directa)
    {
        var n = name.ToLowerInvariant();
        if (directa)
        {
            if (n.Contains("maestro")) return "M";
            if (n.Contains("ayudante")) return "A";
            if (n.Contains("tecnico") || n.Contains("técnico")) return "TE";
            if (n.Contains("soldador")) return "SO";
            if (n.Contains("liniero")) return "LI";
        }
        else
        {
            if (n.Contains("administrador")) return "AD";
            if (n.Contains("supervisor")) return "SV";
            if (n.Contains("capataz")) return "CA";
            if (n.Contains("oficina")) return "OT";
            if (n.Contains("bodega")) return "BG";
            if (n.Contains("prevencion") || n.Contains("prevención")) return "PR";
            if (n.Contains("asistente")) return "AS";
            if (n.Contains("chofer") || n.Contains("conductor")) return "CH";
        }

        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2)
            return ("" + words[0][0] + words[1][0]).ToUpperInvariant();
        if (words.Length == 1 && words[0].Length >= 2)
            return words[0][..2].ToUpperInvariant();

        return directa ? "D" : "I";
    }

    private void OnSueldoGotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
            tb.SelectAll();
    }

    private void OnSueldoLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb)
            return;
        var digits = new string(tb.Text.Where(char.IsDigit).ToArray());
        if (decimal.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            tb.Text = Math.Round(n).ToString("N0", EsCl);
        else
            tb.Text = "0";
    }

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        var nombre = (NombreBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            WpfDialogService.Warning("Validación", "Debe ingresar el nombre del puesto.");
            NombreBox.Focus();
            return;
        }

        var tag = (TagBox.Text ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(tag))
        {
            WpfDialogService.Warning("Validación", "Debe ingresar una sigla o tag para el puesto.");
            TagBox.Focus();
            return;
        }

        if (tagExistsCheck(tag))
        {
            WpfDialogService.Warning("Validación", $"La sigla '{tag}' ya está en uso por otro puesto. Ingrese una distinta.");
            TagBox.Focus();
            TagBox.SelectAll();
            return;
        }

        var sueldoRaw = new string((SueldoBox.Text ?? "").Where(char.IsDigit).ToArray());
        decimal sueldo = 0m;
        if (!string.IsNullOrWhiteSpace(sueldoRaw) && decimal.TryParse(sueldoRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var s))
            sueldo = Math.Max(0m, s);

        int dotacion = 1;
        decimal fraccion = 1m;

        if (!isDirecta)
        {
            var dotText = (DotacionBox.Text ?? "").Trim();
            if (!int.TryParse(dotText, NumberStyles.Integer, CultureInfo.InvariantCulture, out dotacion) || dotacion < 0)
            {
                WpfDialogService.Warning("Validación", "La dotación mensual debe ser un número entero mayor o igual a 0.");
                DotacionBox.Focus();
                return;
            }

            var fracText = (FraccionBox.Text ?? "").Trim();
            fraccion = MoCargoEditorVm.ParseFraccion(fracText, -1m);
            if (fraccion < 0m)
            {
                WpfDialogService.Warning("Validación", "La fracción de obra debe ser un valor entre 0 y 1 (ej: 0,3 o 0.3 para 30%, 1 para 100%).");
                FraccionBox.Focus();
                return;
            }
        }

        Result = new AddCargoMoDialogResult
        {
            Nombre = nombre,
            Tag = tag,
            SueldoBase = sueldo,
            FraccionObra = fraccion,
            Dotacion = dotacion
        };

        DialogResult = true;
    }
}
