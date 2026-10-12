using System.Windows.Input;
using QubiCostElectrical.Wpf.Features.Cubicacion;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Gestor de teclado del alta rápida en la Lista de Precios del Proyecto (Alt+V → Ctrl++).
/// Se prueba el predicado puro y no el evento: <c>Keyboard.Modifiers</c> refleja el teclado real y
/// no se puede simular, así que un test que dispare el KeyDown no probaría nada útil.
/// </summary>
public sealed class PrecioListaAltaRapidaShortcutTests
{
    [Theory]
    [InlineData(Key.OemPlus)]   // tecla "+/=" de un teclado US
    [InlineData(Key.Add)]       // "+" del teclado numérico
    [InlineData(Key.Oem1)]      // "+" desplazado (teclados latinos)
    [InlineData(Key.OemQuestion)]
    public void EsAltaRapida_CtrlMas_EsVerdadero(Key key)
    {
        var modifiers = key is Key.Oem1 or Key.OemQuestion
            ? ModifierKeys.Control | ModifierKeys.Shift
            : ModifierKeys.Control;

        Assert.True(PrecioListaResumenDialog.EsAltaRapida(key, modifiers));
    }

    [Theory]
    [InlineData(Key.OemPlus, ModifierKeys.None)]                       // "+" sin Ctrl: se escribe el símbolo
    [InlineData(Key.OemPlus, ModifierKeys.Shift)]                      // Shift++ : tampoco es el atajo
    [InlineData(Key.Add, ModifierKeys.Shift)]                          // Shift del numérico: no
    [InlineData(Key.OemPlus, ModifierKeys.Control | ModifierKeys.Alt)]  // Ctrl+Alt++: acentos/AltGr
    [InlineData(Key.S, ModifierKeys.Control)]                          // Ctrl+S: guardar, no alta rápida
    [InlineData(Key.OemMinus, ModifierKeys.Control)]                   // Ctrl+- : el gesto hermano
    public void EsAltaRapida_OtrasCombinaciones_EsFalso(Key key, ModifierKeys modifiers)
    {
        Assert.False(PrecioListaResumenDialog.EsAltaRapida(key, modifiers));
    }

    /// <summary>El ala desplazada no dispara sin Shift: esa tecla sola es otro símbolo.</summary>
    [Fact]
    public void EsAltaRapida_AlaDesplazadaSinShift_EsFalso()
    {
        Assert.False(PrecioListaResumenDialog.EsAltaRapida(Key.Oem1, ModifierKeys.Control));
    }
}
