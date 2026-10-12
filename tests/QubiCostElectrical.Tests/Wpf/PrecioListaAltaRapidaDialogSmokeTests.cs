using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Cubicacion;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Smoke del diálogo real de alta rápida (Alt+V → "Alta rápida"). Los estilos <c>Dialog.*</c> y
/// <c>Form.*</c> solo resuelven con la Application y el tema cargados, así que se usa el host STA
/// compartido (ver <see cref="XamlViewSmokeHost"/>).
/// </summary>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class PrecioListaAltaRapidaDialogSmokeTests
{
    private sealed class MockPriceBankStore : IPriceBankStore
    {
        private readonly List<PriceBankEntry> entries;
        public MockPriceBankStore(List<PriceBankEntry> entries) => this.entries = entries;
        public IReadOnlyList<PriceBankEntry> GetAll() => entries.ToList();
        public PriceBankEntry? GetByCodigo(string codigo) => entries.FirstOrDefault(e => string.Equals(e.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
        public IReadOnlyList<PriceBankEntry> Search(string text) => entries.Where(e => (e.Codigo + e.Descripcion).Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
        public void Upsert(PriceBankEntry entry)
        {
            int idx = entries.FindIndex(e => string.Equals(e.Codigo, entry.Codigo, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) entries[idx] = entry;
            else entries.Add(entry);
        }
        public bool Delete(string codigo) => entries.RemoveAll(e => string.Equals(e.Codigo, codigo, StringComparison.OrdinalIgnoreCase)) > 0;
        public int DeleteAll() { int count = entries.Count; entries.Clear(); return count; }
        public int UpsertMany(IEnumerable<PriceBankEntry> newEntries) { foreach (var e in newEntries) Upsert(e); return newEntries.Count(); }
        public int Count() => entries.Count;
        public int SeedIfEmpty(IEnumerable<PriceBankEntry> seed) => 0;
    }

    public PrecioListaAltaRapidaDialogSmokeTests() => WpfTestHost.EnsureInitialized();

    private static PrecioListaResumenVm CrearVm(decimal fa = 62.80m)
    {
        var session = new BudgetProjectSession();
        // FA calibrado del proyecto (la app arranca en 0 = sin calibrar): la vista previa de la
        // M.O. por % necesita el índice para mostrar una cifra distinta de cero.
        session.ProjectInfo.IndiceMoFa = fa;
        var store = new MockPriceBankStore(new List<PriceBankEntry>
        {
            new() { Codigo = "TAB-001", Descripcion = "Tablero existente", Categoria = "Tableros" },
        });
        return new PrecioListaResumenVm(session, store);
    }

    [Fact]
    public void Dialog_CalculaEnVivoCodigoYMo_SinPedirCodigo()
    {
        XamlViewSmokeHost.Run(() =>
        {
            try
            {
                var vm = CrearVm();
                var dialog = new PrecioListaAltaRapidaDialog(vm);

                var codigo = (TextBlock)dialog.FindName("CodigoText");
                var mo = (TextBlock)dialog.FindName("MoCalculadaText");
                var familia = (ComboBox)dialog.FindName("FamiliaCombo");
                var descripcion = (TextBox)dialog.FindName("DescripcionBox");
                var precio = (TextBox)dialog.FindName("MatClpBox");

                // Arranca vacío: el código cae al prefijo de emergencia, sin pedirle nada al usuario.
                Assert.Equal("MAT-001", codigo.Text);

                // Al elegir familia y describir, el código se recalcula solo y no choca con el
                // TAB-001 que ya existe en el banco.
                familia.SelectedItem = "Tableros";
                descripcion.Text = "Tablero eléctrico 12 circuitos";
                XamlViewSmokeHost.Pump();
                Assert.Equal("TAB-002", codigo.Text);

                // La M.O. se muestra en vivo con Material × % × FA: 100.000 × 2,5% × 62,80 = 157.000.
                precio.Text = "100000";
                XamlViewSmokeHost.Pump();
                Assert.Equal(157000m, LeerNumero(mo.Text));

                // La familia real del proyecto está disponible para elegir.
                Assert.Contains("Tableros", familia.Items.Cast<string>());
            }
            finally
            {
                XamlViewSmokeHost.ClearApplicationCurrent();
            }
        });
    }

    private static decimal LeerNumero(string? texto)
    {
        decimal.TryParse(texto, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out decimal valor);
        return valor;
    }

    /// <summary>
    /// La casilla "usar fórmula" decide si la M.O. sale de Material × % × FA o queda sin M.O.:
    /// desmarcada, la vista previa cae a cero y el % / FA dejan de ser editables.
    /// </summary>
    [Fact]
    public void Dialog_DesmarcarFormula_DejaLaMoEnCeroYDeshabilitaPorcentajeYFa()
    {
        XamlViewSmokeHost.Run(() =>
        {
            try
            {
                var vm = CrearVm();
                var dialog = new PrecioListaAltaRapidaDialog(vm);

                var usarFormula = (CheckBox)dialog.FindName("UsarFormulaCheck");
                var porcentaje = (TextBox)dialog.FindName("PorcentajeBox");
                var fa = (TextBox)dialog.FindName("FaBox");
                var mo = (TextBlock)dialog.FindName("MoCalculadaText");

                Assert.True(usarFormula.IsChecked);
                porcentaje.Text = "2,5";
                ((TextBox)dialog.FindName("MatClpBox")).Text = "100000";
                XamlViewSmokeHost.Pump();
                Assert.Equal(157000m, LeerNumero(mo.Text));

                usarFormula.IsChecked = false;
                XamlViewSmokeHost.Pump();

                Assert.Equal(0m, LeerNumero(mo.Text));
                Assert.False(porcentaje.IsEnabled);
                Assert.False(fa.IsEnabled);
            }
            finally
            {
                XamlViewSmokeHost.ClearApplicationCurrent();
            }
        });
    }

    /// <summary>
    /// Con el FA sin calibrar, la M.O. se calcula con factor neutro (Material × %) y el diálogo tiene
    /// que decir de dónde sale esa cifra, para que no se lea como un dato que falta ingresar.
    /// </summary>
    [Fact]
    public void Dialog_SinFaCalibrado_ExplicaLaMoConFactorNeutro()
    {
        XamlViewSmokeHost.Run(() =>
        {
            try
            {
                var vm = CrearVm(fa: 0m);
                var dialog = new PrecioListaAltaRapidaDialog(vm);

                var explicacion = (TextBlock)dialog.FindName("MoExplicacionText");
                ((TextBox)dialog.FindName("MatClpBox")).Text = "100000";
                ((TextBox)dialog.FindName("FaBox")).Text = "0";
                XamlViewSmokeHost.Pump();

                // 100.000 × 2,5 % × 1 = 2.500
                Assert.Equal(2500m, LeerNumero(((TextBlock)dialog.FindName("MoCalculadaText")).Text));
                Assert.Contains("sin calibrar", explicacion.Text);
                Assert.Contains("factor 1", explicacion.Text);
                Assert.Contains("plan de mano de obra", vm.ExplicacionFa);
            }
            finally
            {
                XamlViewSmokeHost.ClearApplicationCurrent();
            }
        });
    }

    /// <summary>El campo con foco se determina subiendo por el árbol: en el combo editable el foco vive en su TextBox interno.</summary>
    [Fact]
    public void SiguienteCampoHabilitado_CiclaYSaltaLosDeshabilitados()
    {
        static bool Todos(int _) => true;

        // Desde el último campo (FA) vuelve a Familia para encadenar altas sin tocar el mouse.
        Assert.Equal(0, PrecioListaAltaRapidaDialog.SiguienteCampoHabilitado(6, 7, Todos));
        Assert.Equal(4, PrecioListaAltaRapidaDialog.SiguienteCampoHabilitado(3, 7, Todos));

        // Sin fórmula, % (5) y FA (6) quedan deshabilitados y no frenan el salto.
        static bool SinFormula(int i) => i is not (5 or 6);
        Assert.Equal(4, PrecioListaAltaRapidaDialog.SiguienteCampoHabilitado(3, 7, SinFormula));
        Assert.Equal(0, PrecioListaAltaRapidaDialog.SiguienteCampoHabilitado(4, 7, SinFormula));

        // Foco fuera del formulario: entra por el primer campo, no se queda pegado.
        Assert.Equal(0, PrecioListaAltaRapidaDialog.SiguienteCampoHabilitado(-1, 7, Todos));

        Assert.Equal(-1, PrecioListaAltaRapidaDialog.SiguienteCampoHabilitado(0, 7, _ => false));
        Assert.Equal(-1, PrecioListaAltaRapidaDialog.SiguienteCampoHabilitado(0, 0, Todos));
    }

    /// <summary>La cifra de M.O. siempre viene con su fórmula escrita al lado (con o sin FA calibrado).</summary>
    [Fact]
    public void ExplicarMo_EscribeLaFormulaYElMotivoDelFactorNeutro()
    {
        Assert.Contains("sin fórmula", PrecioListaAltaRapidaDialog.ExplicarMo(false, 100_000m, 2.5m, 0m), StringComparison.OrdinalIgnoreCase);

        string sinFa = PrecioListaAltaRapidaDialog.ExplicarMo(true, 100_000m, 2.5m, 0m);
        Assert.Contains("sin calibrar", sinFa);
        Assert.Contains("factor 1", sinFa);
        Assert.DoesNotContain("$0", sinFa);

        string conFa = PrecioListaAltaRapidaDialog.ExplicarMo(true, 100_000m, 2.5m, 62.80m);
        Assert.Contains("FA 62", conFa);
        Assert.Contains("mano de obra", conFa);
        Assert.DoesNotContain("$0", conFa);
    }

    /// <summary>
    /// Regresión del cuelgue reportado: el diálogo abría el desplegable de Familia por código y ese
    /// Popup, al mostrarse el modal, se comía el primer clic («Agregar material») dejando la ventana
    /// sin responder. El desplegable debe quedar cerrado hasta que el usuario lo pida (↓ o tecleando).
    /// </summary>
    [Fact]
    public void Dialog_AlAbrir_NoAbreElDesplegableDeFamilia()
    {
        XamlViewSmokeHost.Run(() =>
        {
            var vm = CrearVm();
            var dialog = new PrecioListaAltaRapidaDialog(vm)
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -4000,
                Top = -4000,
                ShowInTaskbar = false
            };

            var familia = (ComboBox)dialog.FindName("FamiliaCombo");
            try
            {
                dialog.Show();
                XamlViewSmokeHost.Pump();
                XamlViewSmokeHost.Pump();

                Assert.False(familia.IsDropDownOpen);
                Assert.True(familia.IsKeyboardFocusWithin);
            }
            finally
            {
                dialog.Close();
                XamlViewSmokeHost.Pump();
            }
        });
    }
}
