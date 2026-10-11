using System;
using System.Linq;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class ShellNotificationServiceTests
{
    public ShellNotificationServiceTests() => WpfTestHost.EnsureInitialized();

    [Fact]
    public void ToastMessage_HasAction_IsTrueOnlyWhenLabelAndActionExist()
    {
        var withoutAction = new ToastMessage("Título", "Mensaje", ToastLevel.Info, () => { });
        var withoutLabel = new ToastMessage("Título", "Mensaje", ToastLevel.Info, () => { }, action: () => { });
        var withAction = new ToastMessage("Título", "Mensaje", ToastLevel.Success, () => { }, "Deshacer", () => { });

        Assert.False(withoutAction.HasAction);
        Assert.False(withoutLabel.HasAction);
        Assert.True(withAction.HasAction);
    }

    [Fact]
    public void ShowSuccess_WithAction_StoresActionLabel()
    {
        // Headless: no se crea Application.Current. En este proceso de pruebas nunca existe
        // (si existiría, su Dispatcher vivo cambiaría coalescers y rutas de todo el resto de
        // la colección WpfHost); ShellNotificationService acepta dispatcher nulo.
        var service = new ShellNotificationService();
        try
        {
            service.ShowSuccess("Pegado", "2 fila(s) importada(s).", "Deshacer", () => { });

            var toast = Assert.Single(service.Active);
            Assert.Equal("Deshacer", toast.ActionLabel);
            Assert.True(toast.HasAction);
        }
        finally
        {
            foreach (var toast in service.Active.ToList())
                service.Dismiss(toast);
        }
    }
}
