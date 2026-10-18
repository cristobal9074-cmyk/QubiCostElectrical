using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Templates;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class CrearPlantillaDesdeCubicacionDialog : Window
{
    private readonly BudgetProjectSession session;
    private readonly IConjuntoTemplateStore? conjuntoStore;
    private readonly ObservableCollection<CrearPlantillaRecursoVm> recursos = new();
    private readonly ObservableCollection<MaestroOptionVm> maestrosDisponibles = new();

    public ConjuntoElectricoRow? ConjuntoCreado { get; private set; }

    public CrearPlantillaDesdeCubicacionDialog(
        IReadOnlyList<CubicacionRowVm> sourceRows,
        ItemizadoRow? partida,
        BudgetProjectSession session,
        IConjuntoTemplateStore? conjuntoStore)
    {
        this.session = session;
        this.conjuntoStore = conjuntoStore;

        InitializeComponent();

        // Categorías disponibles
        CmbTipo.ItemsSource = ConjuntoTipo.All;
        CmbTipo.SelectedItem = GuessCategoria(partida?.Descripcion ?? "");

        // Sugerir nombre y código
        string cleanNombre = (partida?.Descripcion ?? "").Trim();
        if (string.IsNullOrWhiteSpace(cleanNombre)) cleanNombre = "Nueva Plantilla";
        TxtNombre.Text = cleanNombre;

        string cleanCodigo = (partida?.Item ?? "").Trim();
        if (string.IsNullOrWhiteSpace(cleanCodigo))
            cleanCodigo = "PLANT-" + DateTime.Now.ToString("yyMMdd-HHmm");
        else
            cleanCodigo = "PLANT-" + cleanCodigo.Replace(".", "-");
        TxtCodigo.Text = cleanCodigo;

        // Inicializar recursos desde las filas de cubicación
        InicializarRecursos(sourceRows);

        GridRecursos.ItemsSource = recursos;
        ActualizarMaestrosDisponibles();
    }

    private static string GuessCategoria(string descripcion)
    {
        string d = (descripcion ?? "").ToLowerInvariant();
        if (d.Contains("alim") || d.Contains("subfeeder") || d.Contains("acomet")) return ConjuntoTipo.Alimentador;
        if (d.Contains("bandej") || d.Contains("bpc") || d.Contains("escaler")) return ConjuntoTipo.Bandeja;
        if (d.Contains("tub") || d.Contains("conduit") || d.Contains("canaliz") || d.Contains("cpc")) return ConjuntoTipo.Canalizacion;
        if (d.Contains("enchuf") || d.Contains("toma")) return ConjuntoTipo.PuntoEnchufe;
        if (d.Contains("alumb") || d.Contains("ilum") || d.Contains("lumin") || d.Contains("foco")) return ConjuntoTipo.PuntoAlumbrado;
        if (d.Contains("tabler") || d.Contains("tdf") || d.Contains("tda") || d.Contains("tg")) return ConjuntoTipo.Tablero;
        if (d.Contains("tierra") || d.Contains("malla") || d.Contains("pat")) return ConjuntoTipo.PuestaTierra;
        return ConjuntoTipo.General;
    }

    private void InicializarRecursos(IReadOnlyList<CubicacionRowVm> sourceRows)
    {
        decimal maestroCant = sourceRows.Count > 0 ? sourceRows[0].Cantidad : 0m;
        string? primerMaestroId = null;

        for (int i = 0; i < sourceRows.Count; i++)
        {
            var r = sourceRows[i];
            var vm = new CrearPlantillaRecursoVm(i + 1, r, ActualizarMaestrosDisponibles, maestrosDisponibles);

            if (i == 0)
            {
                vm.Rol = "Maestro";
                primerMaestroId = vm.Id;
            }
            else
            {
                // Si la fila ya traía vínculo esclavo previo de una plantilla
                if (r.Source.Esclavo != null)
                {
                    vm.Rol = "Esclavo";
                    vm.MaestroId = primerMaestroId ?? "";
                    if (r.Source.Esclavo.TipoCalculo == TipoCalculoParametrico.Espaciamiento)
                    {
                        vm.TipoCalculoLabel = "Espaciamiento (Metros)";
                        vm.FactorText = r.Source.Esclavo.Espaciamiento.ToString("0.##", CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        vm.TipoCalculoLabel = "Factor (Multiplicador)";
                        vm.FactorText = r.Source.Esclavo.Factor.ToString("0.####", CultureInfo.InvariantCulture);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(r.Source.Operacion))
                {
                    vm.Rol = "Operación";
                    vm.Operacion = r.Source.Operacion;
                }
                else
                {
                    // Deducción inteligente de rol y factor basada en las cantidades actuales
                    vm.Rol = "Esclavo";
                    vm.MaestroId = primerMaestroId ?? "";

                    if (maestroCant > 0m && r.Cantidad > 0m)
                    {
                        if (r.Cantidad == maestroCant)
                        {
                            vm.TipoCalculoLabel = "Factor (Multiplicador)";
                            vm.FactorText = "1";
                        }
                        else if (r.Cantidad > maestroCant)
                        {
                            // Múltiplo (ej. 55 m maestro -> 275 m conductor = factor 5)
                            decimal ratio = Math.Round(r.Cantidad / maestroCant, 4);
                            vm.TipoCalculoLabel = "Factor (Multiplicador)";
                            vm.FactorText = ratio.ToString("0.####", CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            // Cantidad menor: si la unidad es UN/JGO/PZA y la relación da algo como 1.5m
                            decimal ratioEspaciamiento = Math.Round(maestroCant / r.Cantidad, 2);
                            if (ratioEspaciamiento >= 1m && ratioEspaciamiento <= 5m && string.Equals(r.Unidad, "UN", StringComparison.OrdinalIgnoreCase))
                            {
                                vm.TipoCalculoLabel = "Espaciamiento (Metros)";
                                vm.FactorText = ratioEspaciamiento.ToString("0.##", CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                decimal ratioFactor = Math.Round(r.Cantidad / maestroCant, 4);
                                vm.TipoCalculoLabel = "Factor (Multiplicador)";
                                vm.FactorText = ratioFactor.ToString("0.####", CultureInfo.InvariantCulture);
                            }
                        }
                    }
                    else
                    {
                        vm.TipoCalculoLabel = "Factor (Multiplicador)";
                        vm.FactorText = "1";
                    }
                }
            }

            recursos.Add(vm);
        }
    }

    private void ActualizarMaestrosDisponibles()
    {
        var nuevos = recursos
            .Where(r => r.EsMaestro)
            .Select(r => new MaestroOptionVm(r.Id, $"[{r.DisplayIndex}] {r.Codigo} - {r.DescripcionShort}"))
            .ToList();

        maestrosDisponibles.Clear();
        foreach (var m in nuevos) maestrosDisponibles.Add(m);

        // Si algún esclavo quedó huérfano, apuntarlo al primer maestro disponible
        string? primerId = maestrosDisponibles.FirstOrDefault()?.Id;
        foreach (var r in recursos.Where(x => x.EsEsclavo))
        {
            if (string.IsNullOrWhiteSpace(r.MaestroId) || !maestrosDisponibles.Any(m => m.Id == r.MaestroId))
            {
                r.MaestroId = primerId ?? "";
            }
        }
    }

    private void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        string nombre = TxtNombre.Text.Trim();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            WpfDialogService.Warning("Nombre requerido", "Por favor ingresa un nombre para la plantilla.");
            TxtNombre.Focus();
            return;
        }

        string codigo = TxtCodigo.Text.Trim();
        if (string.IsNullOrWhiteSpace(codigo))
        {
            WpfDialogService.Warning("Código requerido", "Por favor ingresa un código para la plantilla.");
            TxtCodigo.Focus();
            return;
        }

        if (recursos.Count == 0)
        {
            WpfDialogService.Warning("Sin materiales", "No hay materiales para incluir en la plantilla.");
            return;
        }

        // Si hay esclavos, verificar que exista al menos un maestro
        bool tieneEsclavos = recursos.Any(r => r.EsEsclavo);
        bool tieneMaestros = recursos.Any(r => r.EsMaestro);
        if (tieneEsclavos && !tieneMaestros)
        {
            WpfDialogService.Warning("Falta Maestro", "Has marcado materiales como Esclavos pero ninguna fila está configurada como Maestro.\n\nPor favor marca al menos una fila con el rol 'Maestro'.");
            return;
        }

        // Construir ConjuntoElectricoRow
        var conjunto = new ConjuntoElectricoRow
        {
            Id = Guid.NewGuid().ToString("N"),
            Codigo = codigo,
            Nombre = nombre,
            TipoConjunto = CmbTipo.SelectedItem as string ?? ConjuntoTipo.General,
            Descripcion = $"Plantilla generada desde partida {TxtNombre.Text}",
            Unidad = recursos.FirstOrDefault()?.Unidad ?? Unidades.UN,
            ActualizadoEn = AppClock.Now
        };

        foreach (var r in recursos)
        {
            var rec = new ConjuntoRecursoRow
            {
                Id = r.Id,
                Tipo = ApuTipoRecurso.MAT,
                Codigo = r.Codigo,
                Descripcion = r.Descripcion,
                Unidad = r.Unidad,
                Operacion = r.EsOperacion ? r.Operacion.Trim() : "",
                MaestroId = r.EsEsclavo ? r.MaestroId : ""
            };

            if (r.EsEsclavo)
            {
                if (r.TipoCalculoLabel.StartsWith("Espaciamiento", StringComparison.OrdinalIgnoreCase))
                {
                    rec.TipoCalculo = TipoCalculoParametrico.Espaciamiento;
                    rec.Espaciamiento = decimal.TryParse(r.FactorText, NumberStyles.Number, CultureInfo.InvariantCulture, out var esp) && esp > 0m
                        ? esp : 1.5m;
                    rec.Factor = 1m;
                }
                else
                {
                    rec.TipoCalculo = TipoCalculoParametrico.Factor;
                    rec.Factor = decimal.TryParse(r.FactorText, NumberStyles.Number, CultureInfo.InvariantCulture, out var fac) && fac > 0m
                        ? fac : 1m;
                    rec.Espaciamiento = 1m;
                }
            }
            else
            {
                rec.TipoCalculo = TipoCalculoParametrico.Factor;
                rec.Factor = 1m;
            }

            conjunto.Recursos.Add(rec);
        }

        // Guardar en la biblioteca global y en la sesión del proyecto
        session.ConjuntosElectricos.Add(conjunto);
        session.MarkDirty(ProjectDirtyScope.ConjuntosElectricos);

        if (conjuntoStore != null)
        {
            try
            {
                var existentes = conjuntoStore.GetAll().ToList();
                existentes.RemoveAll(c => string.Equals(c.Id, conjunto.Id, StringComparison.OrdinalIgnoreCase)
                                       || string.Equals(c.Codigo, conjunto.Codigo, StringComparison.OrdinalIgnoreCase));
                existentes.Add(conjunto);
                conjuntoStore.SaveAll(existentes);
            }
            catch (Exception ex)
            {
                AppLog.Error("Cubicacion", "No se pudo guardar la plantilla en la biblioteca global", ex);
            }
        }

        ConjuntoCreado = conjunto;
        WpfDialogService.Info("Plantilla Guardada", $"La plantilla «{conjunto.Nombre}» se guardó exitosamente con {conjunto.Recursos.Count} materiales.\n\nYa está disponible para usar en cualquier partida.");
        DialogResult = true;
    }
}

// ─── ViewModel para cada fila en la ventana de creación de plantilla ──────────
public sealed class CrearPlantillaRecursoVm : ViewModelBase
{
    private readonly Action onMaestrosChanged;
    private string rol = "Esclavo";
    private string maestroId = "";
    private string tipoCalculoLabel = "Factor (Multiplicador)";
    private string factorText = "1";
    private string operacion = "";

    public CrearPlantillaRecursoVm(
        int displayIndex,
        CubicacionRowVm source,
        Action onMaestrosChanged,
        ObservableCollection<MaestroOptionVm> maestrosDisponibles)
    {
        this.onMaestrosChanged = onMaestrosChanged;
        MaestrosDisponibles = maestrosDisponibles;

        Id = Guid.NewGuid().ToString("N");
        DisplayIndex = displayIndex;
        Codigo = source.Codigo ?? "";
        Descripcion = source.Descripcion ?? "";
        Unidad = source.Unidad ?? "";
        CantidadActual = source.Cantidad;
    }

    public string Id { get; }
    public int DisplayIndex { get; }
    public string Codigo { get; }
    public string Descripcion { get; }
    public string Unidad { get; }
    public decimal CantidadActual { get; }

    public string CantidadDisplay => CantidadActual % 1 == 0 ? CantidadActual.ToString("N0", CultureInfo.CurrentCulture) : CantidadActual.ToString("#,##0.##", CultureInfo.CurrentCulture);
    public string DescripcionShort => Descripcion.Length > 25 ? Descripcion.Substring(0, 22) + "…" : Descripcion;

    public IReadOnlyList<string> RolesDisponibles { get; } = new[] { "Maestro", "Esclavo", "Operación" };
    public IReadOnlyList<string> TiposCalculoDisponibles { get; } = new[] { "Factor (Multiplicador)", "Espaciamiento (Metros)" };
    public ObservableCollection<MaestroOptionVm> MaestrosDisponibles { get; }

    public bool EsMaestro => string.Equals(rol, "Maestro", StringComparison.OrdinalIgnoreCase);
    public bool EsEsclavo => string.Equals(rol, "Esclavo", StringComparison.OrdinalIgnoreCase);
    public bool EsOperacion => string.Equals(rol, "Operación", StringComparison.OrdinalIgnoreCase);

    public string Rol
    {
        get => rol;
        set
        {
            if (SetProperty(ref rol, value ?? "Esclavo"))
            {
                OnPropertyChanged(nameof(EsMaestro));
                OnPropertyChanged(nameof(EsEsclavo));
                OnPropertyChanged(nameof(EsOperacion));
                onMaestrosChanged();
            }
        }
    }

    public string MaestroId
    {
        get => maestroId;
        set => SetProperty(ref maestroId, value ?? "");
    }

    public string TipoCalculoLabel
    {
        get => tipoCalculoLabel;
        set => SetProperty(ref tipoCalculoLabel, value ?? "Factor (Multiplicador)");
    }

    public string FactorText
    {
        get => factorText;
        set => SetProperty(ref factorText, value ?? "1");
    }

    public string Operacion
    {
        get => operacion;
        set => SetProperty(ref operacion, value ?? "");
    }
}
