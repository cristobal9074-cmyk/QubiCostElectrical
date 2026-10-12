using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Cubicacion;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Smoke del diálogo real de la casilla de cubicación (material creado en la lista de precios →
/// "¿en qué zona y con qué cantidad?"). Los estilos <c>Dialog.*</c> y <c>Form.*</c> solo resuelven
/// con la Application y el tema cargados, así que se usa el host STA compartido (ver
/// <see cref="XamlViewSmokeHost"/>).
/// </summary>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class ZonaCantidadDialogSmokeTests
{
    public ZonaCantidadDialogSmokeTests() => WpfTestHost.EnsureInitialized();

    private static List<ZonaProyecto> Zonas() => new()
    {
        new ZonaProyecto { Id = "z1", Nombre = "Subterráneo", Orden = 0, Activa = true },
        new ZonaProyecto { Id = "z2", Nombre = "Piso 2", Orden = 1, Activa = true }
    };

    /// <summary>
    /// El índice elegido es el de la zona en la lista ordenada (el mismo offset de la columna de
    /// casillas) y la cantidad sale normalizada con punto decimal para el motor de fórmulas, aunque
    /// el usuario la escriba con coma.
    /// </summary>
    [Fact]
    public void Dialog_TraduceZonaYCantidad_ALaCasillaYElTextoQueEsperaElMotor()
    {
        XamlViewSmokeHost.Run(() =>
        {
            try
            {
                var dialog = new ZonaCantidadDialog(Zonas(), "GG-003", "GENERADOR 150KVA", "Arranque 220 volts");
                var combo = (ComboBox)dialog.FindName("ZonaCombo");
                var cantidad = (TextBox)dialog.FindName("CantidadBox");

                Assert.Equal("Subterráneo", combo.SelectedItem);
                Assert.Equal(0, dialog.Offset);
                Assert.Equal("1", dialog.CantidadTexto); // arranca en 1 lista para Enter

                combo.SelectedIndex = 1;
                Assert.Equal(1, dialog.Offset);

                cantidad.Text = "2,5";
                Assert.Equal("2.5", dialog.CantidadTexto);

                // Cantidades inválidas o no positivas no se pueden aplicar.
                cantidad.Text = "0";
                Assert.Equal("", dialog.CantidadTexto);
                cantidad.Text = "1,5,5";
                Assert.Equal("", dialog.CantidadTexto);
            }
            finally
            {
                XamlViewSmokeHost.ClearApplicationCurrent();
            }
        });
    }

    /// <summary>Sin zonas configuradas se avisa y se usa la primera casilla, sin selector fantasma.</summary>
    [Fact]
    public void Dialog_SinZonas_AvisaYUsaLaPrimeraCasilla()
    {
        XamlViewSmokeHost.Run(() =>
        {
            try
            {
                var dialog = new ZonaCantidadDialog(new List<ZonaProyecto>(), "GG-003", "GENERADOR 150KVA", null);

                Assert.Equal(Visibility.Collapsed, ((ComboBox)dialog.FindName("ZonaCombo")).Visibility);
                Assert.Equal(Visibility.Visible, ((Border)dialog.FindName("SinZonasBanner")).Visibility);
                Assert.Equal(0, dialog.Offset);
            }
            finally
            {
                XamlViewSmokeHost.ClearApplicationCurrent();
            }
        });
    }

    /// <summary>El contexto del material y la partida se muestran para no cubicar el material equivocado.</summary>
    [Fact]
    public void Dialog_MuestraElMaterialYLaPartida()
    {
        XamlViewSmokeHost.Run(() =>
        {
            try
            {
                var dialog = new ZonaCantidadDialog(Zonas(), "GG-003", "GENERADOR 150KVA", "Arranque 220 volts. S.C");
                var material = ((TextBlock)dialog.FindName("MaterialText")).Text;
                var partida = ((TextBlock)dialog.FindName("PartidaText")).Text;

                Assert.Contains("GG-003", material);
                Assert.Contains("GENERADOR 150KVA", material);
                Assert.Contains("Arranque 220 volts. S.C", partida);
            }
            finally
            {
                XamlViewSmokeHost.ClearApplicationCurrent();
            }
        });
    }

    /// <summary>
    /// El selector de zona baja a la cantidad con Enter en vez de abrir el desplegable: el flujo de
    /// teclado es zona → cantidad → Enter (evita el congelamiento reportado con popups en modales).
    /// </summary>
    [Fact]
    public void Dialog_EnterEnLaZona_BajaALaCantidad_SinAbrirElDesplegable()
    {
        XamlViewSmokeHost.Run(() =>
        {
            var dialog = new ZonaCantidadDialog(Zonas(), "GG-003", "GENERADOR 150KVA", "Arranque 220 volts")
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -4000,
                Top = -4000,
                ShowInTaskbar = false
            };
            var combo = (ComboBox)dialog.FindName("ZonaCombo");
            var cantidad = (TextBox)dialog.FindName("CantidadBox");

            // Con varias zonas el foco arranca en el selector: es la pregunta que se está haciendo.
            dialog.Loaded += (_, _) => Assert.True(combo.IsKeyboardFocusWithin);
            try
            {
                dialog.Show();
                XamlViewSmokeHost.Pump();
                XamlViewSmokeHost.Pump();

                Assert.False(combo.IsDropDownOpen);
                Assert.True(combo.IsKeyboardFocusWithin);

                var enter = new System.Windows.Input.KeyEventArgs(
                    System.Windows.Input.Keyboard.PrimaryDevice,
                    System.Windows.PresentationSource.FromVisual(dialog),
                    0,
                    System.Windows.Input.Key.Enter)
                {
                    RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent
                };
                combo.RaiseEvent(enter);
                XamlViewSmokeHost.Pump();

                Assert.False(combo.IsDropDownOpen);
                Assert.True(cantidad.IsKeyboardFocusWithin);
                Assert.Equal("1", cantidad.SelectedText); // seleccionada para reemplazarla escribiendo
            }
            finally
            {
                dialog.Close();
                XamlViewSmokeHost.Pump();
            }
        });
    }

    /// <summary>Con una sola zona no hay nada que elegir: el foco arranca en la cantidad.</summary>
    [Fact]
    public void Dialog_ConUnaSolaZona_EnfocaLaCantidad()
    {
        XamlViewSmokeHost.Run(() =>
        {
            var dialog = new ZonaCantidadDialog(
                Zonas().Take(1).ToList(), "GG-003", "GENERADOR 150KVA", "Arranque 220 volts")
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -4000,
                Top = -4000,
                ShowInTaskbar = false
            };
            try
            {
                dialog.Show();
                XamlViewSmokeHost.Pump();
                XamlViewSmokeHost.Pump();

                Assert.True(((TextBox)dialog.FindName("CantidadBox")).IsKeyboardFocusWithin);
            }
            finally
            {
                dialog.Close();
                XamlViewSmokeHost.Pump();
            }
        });
    }
}
