using QubiCostElectrical.Wpf.Features.Project;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Instancia el diálogo real para fijar su plantilla preseleccionada. Es el punto donde el
/// usuario pidió que un proyecto nuevo nazca con el itemizado vacío: la ausencia de partidas
/// debe resolverse en el diálogo, no en una pantalla ni en estado global de la app.
/// Se usa el host STA compartido (ver <see cref="XamlViewSmokeHost"/>) porque los estilos
/// <c>Dialog.*</c> / <c>Form.*</c> sólo resuelven con la Application y el tema cargados.
/// </summary>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class NewProjectDialogDefaultTemplateTests
{
    public NewProjectDialogDefaultTemplateTests() => WpfTestHost.EnsureInitialized();

    [Fact]
    public void Dialog_SinPlantillaExplicita_PreseleccionaBlanco()
    {
        XamlViewSmokeHost.Run(() =>
        {
            try
            {
                var dialog = new NewProjectDialog(initialTemplateId: null, suggestedName: null);
                var template = dialog.SelectedTemplate;

                Assert.NotNull(template);
                Assert.True(template!.EsEnBlanco, "Un proyecto nuevo debe arrancar con el itemizado vacío.");
                Assert.Empty(template.CreateInstanceRows());
            }
            finally
            {
                XamlViewSmokeHost.ClearApplicationCurrent();
            }
        });
    }

    [Fact]
    public void Dialog_ConPlantillaExplicita_LaPreselecciona()
    {
        XamlViewSmokeHost.Run(() =>
        {
            try
            {
                var dialog = new NewProjectDialog("solar_pv_plant", suggestedName: null);

                Assert.Equal("solar_pv_plant", dialog.SelectedTemplate?.Id);
            }
            finally
            {
                XamlViewSmokeHost.ClearApplicationCurrent();
            }
        });
    }

    [Fact]
    public void Dialog_ConPlantillaDesconocida_CaeEnBlanco()
    {
        XamlViewSmokeHost.Run(() =>
        {
            try
            {
                var dialog = new NewProjectDialog("no-existe-esta-plantilla", suggestedName: null);

                Assert.True(dialog.SelectedTemplate!.EsEnBlanco);
            }
            finally
            {
                XamlViewSmokeHost.ClearApplicationCurrent();
            }
        });
    }
}
