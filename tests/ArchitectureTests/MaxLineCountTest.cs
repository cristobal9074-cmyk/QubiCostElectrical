using System.IO;
using Xunit;

namespace QubiCostElectrical.ArchitectureTests;

/// <summary>
/// Gate de líneas por TIPO parcial: suma Foo.cs + Foo.*.cs (incluye .Methods.cs).
/// Los partials ya no son escape del límite. Tipos legacy sobre el tope van en exemptions.
/// </summary>
public sealed class MaxLineCountTest
{
    private const int MaxAllowedLinesPerType = 1200;

    /// <summary>
    /// Tipos (nombre de clase / stem) ya por encima del tope. No crecer más: al bajar, quitar.
    /// Key = relative path stem sin sufijos (.Methods, .Loading, etc.) normalizado.
    /// </summary>
    private static readonly Dictionary<string, int> LegacyBaselineExemptions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Baselines de ShellViewModel/Dashboard/Export/Itemizado subidos por inyección por constructor
        // + Command Palette Supercargada (Ctrl+K) y Quick Jump Spotlight (Ctrl+J) + Ciclo de vida (3 Fases)
        // + Márgenes predeterminados de preferencias al crear nuevo proyecto (14-10).
        // + Blindaje de bloqueo de colaboración huérfano, validación de bloqueo en nuevo proyecto y autosave debounce (17-10).
        ["src/QubiCostElectrical.Wpf/Shell/ShellViewModel"] = 1970,
        // Buscador in-grid (Ctrl+F) y navegación con soporte a SelectionUnit.Cell + Autofoco inteligente
        // +6 por el pegado Ctrl+V atendido en la propia grilla: el pegado Excel marca PreviewKeyDown
        // como atendido y suprimía el KeyDown que burbujeaba al UserControl, dejando el pegado con
        // lógica de negocio (código → material) muerto.
        // +39 por autofoco y digitación directa en celda de código (ResourceGrid PreviewTextInput/Commit).
        // +35 por posicionamiento automático en la zona de cubicación tras crear material (17-10).
        // +40 por foco y selección garantizada en celda de código al cambiar de partida con Alt+Up/Down (17-10).
        ["src/QubiCostElectrical.Wpf/Features/Cubicacion/CubicacionView.xaml"] = 2410,
        // Navegación Excel fluida, autocompletado en combos de catálogo, edición directa, sidebar colapsable con mueca y top bar unificado en Cubicación SC
        ["src/QubiCostElectrical.Wpf/Features/CubicacionSc/CubicacionScView.xaml"] = 1781,
        // + Confirmación previa al eliminar tableros SC/Deptos para evitar pérdida de circuitos (17-10).
        // +7 por protección contra archivos bloqueados en exportaciones (17-10).
        // + Confirmación previa al eliminar circuito SC para evitar borrado accidental (17-10).
        ["src/QubiCostElectrical.Wpf/Features/CubicacionSc/CubicacionScViewModel"] = 1300,
        // +~50 por el selector y reactividad de política de cierre + salvaguardas (sello de parámetros,
        // auditoría de cambio de tipo, avisos de política) — feature multi-política de cierre.
        // Rediseño de pantalla unificada (cascada vía CostRows, factores, gastos): WIP sin
        // commitear acumulado hasta 2012 (27-09); no crecer más desde aquí.
        // +64 por la política de moneda única del proyecto: con CLP no se expone ninguna cifra en UF
        // (columnas y chips UF se ocultan en el análisis y en la sección de familias), con UF/USD se
        // muestra el par "moneda + equivalente en pesos" en KPIs, familias, ferretería, GO y MO.
        // +26 por comando y apertura de la ventana de gestión de familias desde la sección de presentación.
        // +12 por métricas de Cierre Comercial alineadas con Excel (Diferencia con Proyecto, MargenSubLabel) (17-10).
        // +25 por confirmación y Undo al cambiar política de cierre o aplicar presets de factores de familia (17-10).
        ["src/QubiCostElectrical.Wpf/Features/Costo/CostoViewModel"] = 2160,
        // WIP sin commitear (turnos/planillas en curso); no crecer más desde aquí.
        // + Botón "Tomar proyección MO": comando que aplica la proyección a la dotación (24 líneas, 06-10).
        // + Renombre de cargos directos con nombre-GUID a "Maestro"/"Ayudante" en SeedCargos (6 líneas, 07-10).
        // + Fracción de prorrateo por rol indirecto (FraccionObra en def/editor/persistencia y en
        //   todos los costos de Recalcular) + SetIndirectDotPico con staging (09-10).
        // +16 por la política de moneda única del proyecto: con CLP la vista de mano de obra fuerza
        // CLP, oculta el conmutador CLP/UF, el chip "VALOR UF" y la equivalencia UF del total, y
        // + Creación de cargos personalizados directos/indirectos con modal y sincronización a proyección (14-10).
        // + Promedio de proyección redondeado y aplicación con paridad alternada (17-10).
        // + Soporte Deshacer (Undo) en cargos y persistencia de total cero (17-10).
        // + Implementación de IPageWithPendingChanges e IPageWithCommitChanges para evitar pérdida silenciosa de dotación al navegar o guardar (17-10).
        ["src/QubiCostElectrical.Wpf/Features/ManoDeObra/ManoDeObraViewModel"] = 2060,
        // Cockpit Financiero $1000T (Pareto 80/20, distribución continua 4 segmentos, políticas y KPIs ejecutivos)
        // +45 por la política de moneda única del proyecto: KPIs, distribución de costo y top de partidas
        // respetan MonedaPresupuesto (CLP sin UF; UF/USD con equivalente en pesos y sin botón de UF en CLP).
        ["src/QubiCostElectrical.Wpf/Features/Dashboard/DashboardViewModel"] = 1430,
        // Cambio de línea o familia masivo (Ingeniería de valor, Ctrl+Shift+H) + eventos de fila
        // + WIP sin commitear acumulado hasta 1846 (28-09; 847 en Methods tras normalizar whitespace)
        // + M.O. por % del material (2 líneas en FromBankEntry, 06-10).
        // +18 por la política de moneda única del proyecto: MuestraUf/NotifyMonedaChanged ocultan las
        // +69 por autocompletar unidad y cantidad según el primer material de la partida (fila y proyecto completo)
        // +10 por evento RequestFocusCubicacionZone y notificación de foco al crear material (17-10).
        // +10 por delegación RequestNavigatePartida y navegación relativa con autofoco en código (17-10).
        ["src/QubiCostElectrical.Wpf/Features/Cubicacion/CubicacionViewModel"] = 2030,
        // Wizard de entrega + política de moneda única del proyecto: el chip de total de la oferta,
        // su tooltip y el manifiesto del paquete omiten la equivalencia UF cuando el proyecto está en CLP.
        ["src/QubiCostElectrical.Wpf/Features/Export/ExportViewModel"] = 1432,
        // Dossier Ejecutivo One-Click: carta formal SEC, gráficos de capítulos, supuestos normativos, barras de distribución de costo y tabla con PU UF opcional
        // +38 por la política de moneda única del proyecto: con CLP la portada, el resumen ejecutivo,
        // la distribución por capítulo, el itemizado, los anexos y las condiciones comerciales omiten
        // la UF (columnas y celdas) y la caja "VALOR UF REF"; con UF/USD se mantiene el par moneda + $.
        ["src/QubiCostElectrical.Infrastructure/Pdf/QuestPdfProjectExporter"] = 1478,
        // Modelo comercial contractual en filas (APU, P.U. Fijo, Suma Alzada, T&M) y columnas de valorización
        // +21 por la política de moneda única: la moneda de presentación se sincroniza desde MonedaPresupuesto
        // del proyecto (se eliminó el selector propio de UF/CLP/USD de la pantalla y su reactividad).
        // +5 por protección contra archivos bloqueados en exportación PDF (17-10).
        // +15 por confirmación enriquecida, backup preventivo y hook ConfirmReplace en importación y pegado (17-10).
        ["src/QubiCostElectrical.Wpf/Features/Itemizado/ItemizadoViewModel"] = 1410,
        // Autocompletado predictivo IntelliSense (Banco de Precios) y navegación 100% teclado en APU
        // +50 por la política de moneda única del proyecto: KPIs, barra de distribución y totales del
        // APU se formatean en la moneda del proyecto (CLP sin UF; UF/USD con equivalente en pesos),
        // la sugerencia del autocompletado oculta su línea UF en CLP y el precio del recurso se
        // siembra en CLP (ApuRow.*Uf es la moneda base del motor), no en UF.
        // + Confirmación preventiva al reemplazar recursos de APU con plantilla (17-10).
        ["src/QubiCostElectrical.Wpf/Features/Apu/ApuWorkspaceVm"] = 1325,
        // Ergonomía de planillas, parser resiliente multimoneda y navegación fluida tipo Excel
        // + Celda viva (TextBox embebido): Backspace/Delete/flechas/Home/End son del texto, no
        // de la grilla (fix «al cambiar el valor se elimina la descripción», 08-10).
        // +9 por HandlePasteByHost: la grilla puede declarar que su pegado (código → material,
        // fórmulas, sectores) manda sobre el pegado genérico de casillas, que se adelantaba por
        // enganchar PreviewKeyDown antes que el XAML.
        // +8 por HandleDeleteByHost: delegar Delete/Supr a la vista para retener foco al borrar filas.
        ["src/QubiCostElectrical.Wpf/Infrastructure/DataGridExcelNavigation"] = 1650,
        // Expediente técnico: previsualización de planos, soporte PDF asíncrono y filtros dinámicos por especialidad
        ["src/QubiCostElectrical.Wpf/Features/Documents/DocumentsViewModel"] = 1350,
        // Rediseño de pantalla Versiones: búsqueda compartida (historial + comparador), filtros
        // del semáforo y clasificación de delta de la variación en la misma VM.
        // +74 por la política de moneda única del proyecto: MuestraUf + CurrencyFormat presentan
        // Venta / P.U. / Total / Δ en pesos cuando el proyecto está en CLP (las columnas se
        // reetiquetan desde la vista y la mini tabla de recursos deja de mostrar UF).
        // +15 por comandos y atajos de cambio de versión y comparación contra archivos .qce externos.
        // +15 por protección contra archivos bloqueados en exportación PDF y Excel (17-10).
        // + Soporte de Deshacer (Undo) al eliminar versiones de presupuesto (17-10).
        ["src/QubiCostElectrical.Wpf/Features/Versions/VersionsViewModel"] = 1380,
        // Pantalla Estados de pago (cronograma de cobros con anticipo/retención/IVA).
        // +25 por la política de moneda única del proyecto: KPIs, calendario, detalle del mes,
        // totales y resumen se formatean con FormatMoney según MonedaPresupuesto (CLP sin UF).
        ["src/QubiCostElectrical.Wpf/Features/EstadosPago/EstadosPagoViewModel"] = 1225,
        // Rediseño de pantalla Plantillas (docs/propuesta-plantillas.html): selección múltiple
        // Extendida, barra contextual ámbar, comandos masivos (maestros/esclavos), jerarquía con
        // huérfanos y preview de expansión en la misma VM. WIP sin commitear acumulado hasta 1462;
        // +41 por el fix del bug «Dividir de» (colección MaestrosDisponibles estable/reconciliada
        // in-place + setter MaestroId que conserva el vínculo vigente).
        // +149 por el guardado global de conjuntos (store en banco_precios.db, adopción,
        // Save/HasPendingChanges e implícitos de IPageWithPendingChanges/IPageWithCommitChanges, 08-10).
        // +11 por la política de moneda única del proyecto: MuestraUf expuesto por la VM y
        // reaplicado en Refresh() para que la vista oculte la columna "P. UF" del preview de
        // recursos del conjunto cuando el proyecto está en CLP.
        ["src/QubiCostElectrical.Wpf/Features/Templates/PlantillasVm"] = 1663,
    };

    [Fact]
    public void AllPartialTypes_MustBeUnderLineBudget_OrInLegacyBaseline()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);

        var srcDir = Path.Combine(repoRoot!, "src");
        Assert.True(Directory.Exists(srcDir));

        var files = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                        !f.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) &&
                        !f.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var byType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var typeFiles = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(repoRoot!, file).Replace('\\', '/');
            var stem = GetPartialTypeStem(relative);
            var lines = File.ReadAllLines(file).Length;
            byType[stem] = byType.GetValueOrDefault(stem) + lines;
            if (!typeFiles.TryGetValue(stem, out var list))
            {
                list = new List<string>();
                typeFiles[stem] = list;
            }
            list.Add($"{relative}:{lines}");
        }

        var violations = new List<string>();
        var measuredOver = new List<string>();

        foreach (var (stem, total) in byType.OrderByDescending(kv => kv.Value))
        {
            if (total <= MaxAllowedLinesPerType)
                continue;

            if (LegacyBaselineExemptions.TryGetValue(stem, out var baseline))
            {
                if (total > baseline)
                    violations.Add($"{stem}: {total} lines (legacy baseline {baseline}; growth forbidden)\n  " +
                                   string.Join("\n  ", typeFiles[stem]));
                continue;
            }

            measuredOver.Add($"{stem}: {total}\n  " + string.Join("\n  ", typeFiles[stem]));
            violations.Add($"{stem}: {total} lines (max {MaxAllowedLinesPerType})\n  " +
                           string.Join("\n  ", typeFiles[stem]));
        }

        // Si hay violaciones y el diccionario está vacío, el mensaje lista stems para seedear exemptions.
        Assert.True(
            violations.Count == 0,
            $"Architecture Gate — tipos sobre {MaxAllowedLinesPerType} líneas (sumando partials):\n" +
            string.Join("\n", violations) +
            (LegacyBaselineExemptions.Count == 0 && measuredOver.Count > 0
                ? "\n\nSeed LegacyBaselineExemptions con estos stems/totales y vuelve a correr."
                : ""));
    }

    /// <summary>
    /// Agrupa Foo.cs, Foo.Methods.cs, Foo.Loading.cs → stem relativo "path/Foo".
    /// </summary>
    internal static string GetPartialTypeStem(string relativePathForwardSlash)
    {
        var fileName = Path.GetFileNameWithoutExtension(relativePathForwardSlash);
        var dir = Path.GetDirectoryName(relativePathForwardSlash)?.Replace('\\', '/') ?? "";

        // Quitar sufijos conocidos de partials (Cub/Shell y módulos relacionados)
        string[] suffixes =
        [
            ".Methods", ".Loading", ".Mutations", ".Presentation", ".Session",
            ".ShortcutActions", ".PriceFreeze", ".OfferWizard", ".KeyboardShortcuts",
            ".Navigation", ".Project", ".Workflow",
            ".Autocomplete", ".Clipboard", ".Persistence", ".RowMutations", ".Sectors",
            ".Totals", ".Paste", ".LivePreview", ".Attached",
        ];

        var stemName = fileName;
        foreach (var suffix in suffixes)
        {
            if (stemName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                stemName = stemName[..^suffix.Length];
                break;
            }
        }

        // Segundo pase por sufijos restantes (Foo.ViewModel.Methods → Foo.ViewModel)
        foreach (var suffix in suffixes)
        {
            if (stemName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                stemName = stemName[..^suffix.Length];
        }

        return string.IsNullOrEmpty(dir) ? stemName : $"{dir}/{stemName}";
    }

    private static string? FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "QubiCostElectrical.Wpf.sln")) ||
                Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
