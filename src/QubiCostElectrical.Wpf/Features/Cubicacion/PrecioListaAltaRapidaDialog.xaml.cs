using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Alta rápida de materiales desde la lista de precios (Alt+V): pide familia, precio y la M.O. se
/// deriva con la fórmula del motor (Material × % × FA) en vez de teclearse. El código no se pide:
/// se sugiere solo por familia + correlativo, para crear familias completas (tableros, etc.) sin
/// frenar a escribir códigos.
/// </summary>
public partial class PrecioListaAltaRapidaDialog : Window
{
    private const decimal PorcentajeMoPorDefecto = 2.5m;

    private readonly PrecioListaResumenVm vm;

    /// <summary>Enter se pulsó sobre la lista de familias abierta: al cerrarse, el foco avanza.</summary>
    private bool avanzarTrasElegirFamilia;

    /// <summary>Guarda en curso: evita que un doble clic o Ctrl+Enter repetido agregue dos veces.</summary>
    private bool guardando;

    /// <summary>Código del material creado al aceptar; permite a la lista de precios resaltar la fila nueva.</summary>
    public string? CodigoCreado { get; private set; }

    public PrecioListaAltaRapidaDialog(PrecioListaResumenVm vm)
    {
        InitializeComponent();
        this.vm = vm;
        DataContext = vm;
        vm.PropertyChanged += OnVmPropertyChanged;

        RefrescarVistaPrevia();
        Loaded += OnDialogLoaded;
    }

    /// <summary>
    /// Arranca en Familia, que es el primer dato y el que se repite al cargar una familia completa
    /// (varios tableros, varios artefactos).
    ///
    /// El desplegable NO se abre por código: un Popup abierto al mostrar el modal captura el mouse
    /// y se come el primer clic (el de «Agregar material»), dejando la ventana sin responder. Se
    /// abre con ↓ o escribiendo, que es el comportamiento nativo del ComboBox y no captura nada.
    /// </summary>
    private void OnDialogLoaded(object sender, RoutedEventArgs e) => FamiliaCombo.Focus();

    /// <summary>
    /// Enter sobre la lista abierta no se intercepta: el ComboBox elige el elemento resaltado y
    /// cierra. Si esa tecla fue un Enter, al cerrarse la lista el foco sigue al campo siguiente,
    /// para que elegir familia y avanzar sea una sola pulsación.
    /// </summary>
    private void OnFamiliaDropDownClosed(object? sender, EventArgs e)
    {
        if (!avanzarTrasElegirFamilia)
            return;

        avanzarTrasElegirFamilia = false;
        MoverAlSiguienteCampo();
    }

    /// <summary>
    /// Flujo 100 % teclado: Enter pasa al campo siguiente, Ctrl+Enter agrega el material desde
    /// cualquier campo y Esc cancela. Va en PreviewKeyDown y no en KeyDown porque el botón "Agregar
    /// material" es IsDefault: su Enter se resuelve al burbujear y se adelantaría al teclado del
    /// formulario, guardando a mitad de camino.
    /// </summary>
    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return))
            return;

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            OnGuardarClick(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        // Lista de familias abierta: que el ComboBox elija el elemento resaltado (y avise por
        // DropDownClosed para saltar al campo siguiente). Interceptarlo aquí perdería la selección.
        if (FamiliaCombo.IsDropDownOpen)
        {
            avanzarTrasElegirFamilia = true;
            return;
        }

        // El foco ya está en un botón (Calcular FA): Enter es su acción, no un salto de campo.
        if (Keyboard.FocusedElement is Button)
            return;

        MoverAlSiguienteCampo();
        e.Handled = true;
    }

    /// <summary>Campos del formulario en el orden en que se completan de corrido.</summary>
    private Control[] OrdenDeCampos => [FamiliaCombo, DescripcionBox, MatClpBox, UnidadCombo, UsarFormulaCheck, PorcentajeBox, FaBox];

    /// <summary>Salta al siguiente campo habilitado; desde el último vuelve a Familia (para encadenar).</summary>
    private void MoverAlSiguienteCampo()
    {
        var orden = OrdenDeCampos;
        int indice = IndiceDelFoco(orden);

        int destino = SiguienteCampoHabilitado(indice, orden.Length, i => orden[i].IsEnabled && orden[i].IsVisible);
        if (destino < 0)
            return;

        orden[destino].Focus();

        // Los campos numéricos traen un valor por defecto ("0", "2,5"): al llegar con Enter se
        // seleccionan para que lo tecleado lo reemplace. La descripción no, porque es texto libre
        // que el usuario suele seguir editando.
        if (orden[destino] is TextBox tb && !ReferenceEquals(tb, DescripcionBox))
            tb.SelectAll();
    }

    /// <summary>
    /// Campo del formulario que tiene el foco. Se sube por el árbol porque en un ComboBox editable
    /// el foco real está en su TextBox interno, no en el ComboBox, y comparar el foco directo contra
    /// el orden dejaría el salto atascado en Familia.
    /// </summary>
    private static int IndiceDelFoco(Control[] orden)
    {
        var nodo = Keyboard.FocusedElement as DependencyObject;
        while (nodo != null)
        {
            if (nodo is Control control)
            {
                int indice = Array.IndexOf(orden, control);
                if (indice >= 0)
                    return indice;
            }

            nodo = nodo is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(nodo)
                : LogicalTreeHelper.GetParent(nodo);
        }

        return -1;
    }

    /// <summary>
    /// Índice del próximo campo habilitado partiendo de <paramref name="actual"/>, con vuelta al
    /// inicio. Se aísla como función pura porque el salto de foco real (Keyboard.FocusedElement) no
    /// se puede simular en un test.
    /// </summary>
    internal static int SiguienteCampoHabilitado(int actual, int total, Func<int, bool> estaHabilitado)
    {
        if (total <= 0)
            return -1;

        for (int paso = 1; paso <= total; paso++)
            if (estaHabilitado(((actual + paso) % total + total) % total))
                return ((actual + paso) % total + total) % total;

        return -1;
    }

    /// <summary>
    /// Deja escrito de dónde sale la cifra de mano de obra para que el FA sin calibrar no se lea
    /// como un dato que faltó ingresar: con FA 0 la fórmula queda en Material × % (factor 1) y se
    /// recalcula sola al mover el FA desde Costos.
    /// </summary>
    internal static string ExplicarMo(bool usarFormula, decimal matClp, decimal porcentaje, decimal fa)
    {
        if (!usarFormula)
            return "Sin fórmula: el material entra al presupuesto solo con su precio de material.";

        if (fa <= 0m)
            return $"Material {matClp:N0} × {porcentaje:N2} % = "
                 + $"{MaterialMoResolver.MoClpPorPorcentaje(matClp, porcentaje, fa):N0} de mano de obra "
                 + "(FA sin calibrar: se calcula con factor 1 y se recalcula al mover el FA desde Costos).";

        return $"({matClp:N0} × {porcentaje:N2} %) × FA {fa:N2} = "
             + $"{MaterialMoResolver.MoClpPorPorcentaje(matClp, porcentaje, fa):N0} de mano de obra.";
    }

    /// <summary>
    /// El FA puede cambiar desde fuera del diálogo (botón Calcular FA, edición en Costo): la vista
    /// previa de la M.O. tiene que seguir la cifra vigente, no la que había al abrir.
    /// </summary>
    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PrecioListaResumenVm.IndiceMoFa)
            or nameof(PrecioListaResumenVm.IndiceMoFaTexto))
        {
            RefrescarVistaPrevia();
        }
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnFamiliaChanged(object sender, SelectionChangedEventArgs e) => RefrescarVistaPrevia();

    private void OnDescripcionChanged(object sender, TextChangedEventArgs e) => RefrescarVistaPrevia();

    private void OnPrecioChanged(object sender, TextChangedEventArgs e) => RefrescarVistaPrevia();

    private void OnPorcentajeChanged(object sender, TextChangedEventArgs e) => RefrescarVistaPrevia();

    private void OnFaChanged(object sender, TextChangedEventArgs e) => RefrescarVistaPrevia();

    /// <summary>La casilla de fórmula decide si la M.O. sale de Material × % × FA o queda en cero.</summary>
    private void OnUsarFormulaChanged(object sender, RoutedEventArgs e) => RefrescarVistaPrevia();

    /// <summary>Recalcula código sugerido y M.O. en vivo con el mismo cálculo que usará el guardado.</summary>
    private void RefrescarVistaPrevia()
    {
        // Locals: el análisis de nulos no sostiene el estado de los campos (generados por XAML)
        // a través de las llamadas intermedias de este método.
        var codigoText = CodigoText;
        var moText = MoCalculadaText;
        var porcentajeBox = PorcentajeBox;
        var faBox = FaBox;
        if (codigoText == null || moText == null || porcentajeBox == null || faBox == null)
            return;

        bool usarFormula = UsarFormulaCheck?.IsChecked == true;
        porcentajeBox.IsEnabled = usarFormula;
        faBox.IsEnabled = usarFormula;
        if (CalcularFaButton != null) CalcularFaButton.IsEnabled = usarFormula;

        string familia = FamiliaTexto();
        string descripcion = DescripcionBox.Text.Trim();
        codigoText.Text = vm.SugerirCodigo(familia, descripcion);

        decimal matClp = LeerDecimal(MatClpBox.Text, 0m);
        decimal porcentaje = LeerDecimal(porcentajeBox.Text, PorcentajeMoPorDefecto);
        decimal fa = LeerDecimal(faBox.Text, vm.IndiceMoFa);
        decimal mo = usarFormula ? MaterialMoResolver.MoClpPorPorcentaje(matClp, porcentaje, fa) : 0m;
        moText.Text = mo.ToString("N2", CultureInfo.CurrentCulture);
        if (MoExplicacionText != null)
            MoExplicacionText.Text = ExplicarMo(usarFormula, matClp, porcentaje, fa);
    }

    private void OnGuardarClick(object sender, RoutedEventArgs e)
    {
        // Doble clic o Ctrl+Enter repetido sobre un diálogo que ya se está cerrando: el segundo
        // intento reentraría en el guardado (y hasta en el Info de código duplicado).
        if (guardando)
            return;

        // Nunca se guarda con la lista de familias abierta: un Popup abierto sobre el modal deja
        // la ventana sin responder a los clics.
        FamiliaCombo.IsDropDownOpen = false;

        string descripcion = DescripcionBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(descripcion))
        {
            WpfDialogService.Info("Campo requerido", "La descripción del material es obligatoria.");
            DescripcionBox.Focus();
            return;
        }

        decimal porcentaje = LeerDecimal(PorcentajeBox.Text, PorcentajeMoPorDefecto);
        bool usarFormula = UsarFormulaCheck?.IsChecked == true;
        if (usarFormula && porcentaje <= 0m)
        {
            WpfDialogService.Info("Porcentaje inválido", "El % de M.O. debe ser mayor que 0 (por defecto 2,5).");
            PorcentajeBox.Focus();
            return;
        }

        string familia = FamiliaTexto();
        decimal matClp = LeerDecimal(MatClpBox.Text, 0m);
        string unidad = UnidadCombo.SelectedItem is ComboBoxItem item
            ? (item.Content as string) ?? "un"
            : "un";

        CodigoCreado = vm.SugerirCodigo(familia, descripcion);
        guardando = true;
        if (!vm.AgregarMaterial(
                CodigoCreado,
                descripcion,
                unidad,
                familia,
                matClp,
                0m,
                "",
                DateTime.Today,
                usarFormula ? MaterialMoResolver.MoFormulaPorcentaje : "",
                usarFormula ? porcentaje : 0m))
        {
            // El aviso de duplicado ya se mostró: se reabre el guardado para que el usuario pueda
            // corregir en vez de cerrar el diálogo sin crear nada.
            guardando = false;
            DescripcionBox.Focus();
            return;
        }

        DialogResult = true;
    }

    /// <summary>Familia elegida en el combo o escrita a mano (el combo es editable).</summary>
    private string FamiliaTexto()
    {
        if (FamiliaCombo.SelectedItem is string elegida && !string.IsNullOrWhiteSpace(elegida))
            return elegida.Trim();
        return (FamiliaCombo.Text ?? "").Trim();
    }

    /// <summary>Acepta "1.234,56" y "1234.56"; devuelve el respaldo si el texto no es un número.</summary>
    private static decimal LeerDecimal(string? texto, decimal respaldo)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return respaldo;

        string limpio = texto.Trim();
        if (decimal.TryParse(limpio, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal local))
            return local;
        if (decimal.TryParse(limpio.Replace('.', ','), NumberStyles.Any, CultureInfo.CurrentCulture, out decimal normalizado))
            return normalizado;
        if (decimal.TryParse(limpio.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal invariante))
            return invariante;

        return respaldo;
    }
}
