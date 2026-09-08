using System.Text;
using System.Windows;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Shell;

public partial class KeyboardShortcutsDialog : Window
{
    public KeyboardShortcutsDialog()
    {
        InitializeComponent();
        CubicacionShortcutsText.Text = BuildCubicacionShortcutsText();
    }

    public static void Show(Window owner)
    {
        if (!ShellWindow.CanShowModalDialog)
            return;

        var dialog = new KeyboardShortcutsDialog { Owner = owner };
        dialog.ShowDialog();
    }

    private static string BuildCubicacionShortcutsText()
    {
        var prefs = UserPreferencesStore.Load();
        var sb = new StringBuilder();
        sb.Append("Vigentes (Preferencias)");

        foreach (var def in CubicacionShortcutCatalog.All)
        {
            var effective = KeyboardShortcutPreferences.GetEffective(def.Id, prefs);
            var display = KeyboardShortcutGesture.TryParse(effective, out var gesture) && gesture != null
                ? gesture.ToDisplayString().Replace(" + ", "+", StringComparison.Ordinal)
                : effective;
            if (string.IsNullOrWhiteSpace(display))
                display = def.DefaultGesture;

            sb.Append(" · ");
            sb.Append(display);
            sb.Append(' ');
            sb.Append(ShortLabel(def));
        }

        return sb.ToString();
    }

    private static string ShortLabel(KeyboardShortcutDefinition def) => def.Id switch
    {
        CubicacionShortcutCatalog.PartidaPrevious => "partida ant.",
        CubicacionShortcutCatalog.PartidaNext => "partida sig.",
        CubicacionShortcutCatalog.RowMoveUp => "subir fila",
        CubicacionShortcutCatalog.RowMoveDown => "bajar fila",
        CubicacionShortcutCatalog.PrecioLista => "precio lista",
        CubicacionShortcutCatalog.ScTomar => "tomar SC",
        CubicacionShortcutCatalog.Itemizado => "itemizado",
        CubicacionShortcutCatalog.Plantilla => "plantilla",
        CubicacionShortcutCatalog.SectorMatrix => "casillas",
        CubicacionShortcutCatalog.ResourceDetail => "detalle",
        CubicacionShortcutCatalog.NoCubicar => "excluir/incluir",
        CubicacionShortcutCatalog.Observacion => "observación",
        CubicacionShortcutCatalog.QuickJump => "ir a partida",
        CubicacionShortcutCatalog.AddPartida => "agregar partida",
        CubicacionShortcutCatalog.DeletePartida => "eliminar partida",
        CubicacionShortcutCatalog.FillDown => "rellenar abajo",
        CubicacionShortcutCatalog.FillRight => "rellenar derecha",
        CubicacionShortcutCatalog.RepeatMeasurement => "repetir medición",
        CubicacionShortcutCatalog.BatchReplace => "reemplazar material (Ctrl+H)",
        _ => def.Label.ToLowerInvariant()
    };

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
