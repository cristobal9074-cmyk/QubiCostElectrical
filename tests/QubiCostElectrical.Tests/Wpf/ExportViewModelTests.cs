using System;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Export;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class ExportViewModelTests
{
    // 1. Construcci?n ? no lanza excepci?n
    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var ex = Record.Exception(() => new ExportViewModel(WpfAppHost.Current));
        Assert.Null(ex);
    }

    // 2. Todos los comandos de exportaci?n no son nulos
    [Fact]
    public void Constructor_ExportCommands_NotNull()
    {
        var vm = new ExportViewModel(WpfAppHost.Current);

        Assert.NotNull(vm.ExportOfferPdfCommand);
        Assert.NotNull(vm.ExportExcelCommand);
        Assert.NotNull(vm.ExportLetterCommand);
        Assert.NotNull(vm.ExportMemoriaExcelCommand);
        Assert.NotNull(vm.ExportMemoriaPdfCommand);
        Assert.NotNull(vm.ExportScTechnicalReportCommand);
        Assert.NotNull(vm.ExportJsonCommand);
        Assert.NotNull(vm.ExportPackageCommand);
        Assert.NotNull(vm.PreviewOfferCommand);
    }

    // 3. CanExport sin proyecto abierto = false
    [Fact]
    public void CanExport_WithoutProject_IsFalse()
    {
        WpfTestHost.EnsureInitialized();
        var vm = new ExportViewModel(WpfAppHost.Current);

        // Sin proyecto abierto los comandos de exportaci?n est?n deshabilitados
        Assert.False(vm.ExportOfferPdfCommand.CanExecute(null));
        Assert.False(vm.ExportExcelCommand.CanExecute(null));
        Assert.False(vm.ExportLetterCommand.CanExecute(null));
        Assert.False(vm.ExportMemoriaExcelCommand.CanExecute(null));
        Assert.False(vm.ExportMemoriaPdfCommand.CanExecute(null));
        Assert.False(vm.ExportScTechnicalReportCommand.CanExecute(null));
        Assert.False(vm.ExportJsonCommand.CanExecute(null));
        Assert.False(vm.ExportPackageCommand.CanExecute(null));
        Assert.False(vm.PreviewOfferCommand.CanExecute(null));
    }

    // 4. CanExport: el guard usa HasOpenProject ? verifica que el WorkspaceService lo reporta correctamente
    [Fact]
    public void CanExport_GuardUsesHasOpenProject()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;

        // Sin proyecto: HasOpenProject = false
        host.Session.CurrentProjectPath = "";
        Assert.False(host.ProjectWorkspace.HasOpenProject);

        // Con proyecto: HasOpenProject = true
        host.Session.NewProject("export-guard-test");
        host.Session.CurrentProjectPath = "export-guard-test.qce";
        Assert.True(host.ProjectWorkspace.HasOpenProject);
    }

    // 5. Actions tiene exactamente 8 entradas tras Refresh
    [Fact]
    public void Actions_HasEightEntries()
    {
        var vm = new ExportViewModel(WpfAppHost.Current);
        Assert.Equal(8, vm.Actions.Count);
    }

    // 6. PackageItems tiene exactamente 7 entradas
    [Fact]
    public void PackageItems_HasSevenEntries()
    {
        var vm = new ExportViewModel(WpfAppHost.Current);
        Assert.Equal(7, vm.PackageItems.Count);
    }

    // 7. ScopeModes tiene exactamente 3 modos
    [Fact]
    public void ScopeModes_HasThreeEntries()
    {
        var vm = new ExportViewModel(WpfAppHost.Current);
        Assert.Equal(3, vm.ScopeModes.Count);
        Assert.Contains("Completo", vm.ScopeModes);
        Assert.Contains("Partida", vm.ScopeModes);
        Assert.Contains("\u00c1rea", vm.ScopeModes);
    }

    // 8. Sin proyecto abierto ? SummaryText indica sin proyecto
    [Fact]
    public void SummaryText_WithoutProject_IndicatesNoProject()
    {
        WpfTestHost.EnsureInitialized();
        var vm = new ExportViewModel(WpfAppHost.Current);

        Assert.Contains("Sin proyecto", vm.SummaryText, StringComparison.OrdinalIgnoreCase);
    }

    // 9. SummaryText sin proyecto indica estado sin proyecto
    [Fact]
    public void SummaryText_WithoutProject_IsDefaultMessage()
    {
        WpfTestHost.EnsureInitialized();
        var vm = new ExportViewModel(WpfAppHost.Current);

        // Sin proyecto: "Sin proyecto abierto."
        Assert.Equal("Sin proyecto abierto.", vm.SummaryText);
    }

    [Fact]
    public void PreviewStatusText_WithoutProject_ExplainsPreviewIsBlocked()
    {
        WpfTestHost.EnsureInitialized();
        var vm = new ExportViewModel(WpfAppHost.Current);

        Assert.Contains("Abre un proyecto", vm.PreviewStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Sin vista previa generada.", vm.PreviewFileText);
    }

    // 10. ProjectTitle refleja info del proyecto
    [Fact]
    public void ProjectTitle_WithProject_ReflectsProjectName()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        host.Session.NewProject("export-title-test");
        host.Session.ProjectInfo.Nombre = "Edificio Torres del Mar";

        var vm = new ExportViewModel(WpfAppHost.Current);
        vm.Refresh();

        Assert.Equal("Edificio Torres del Mar", vm.ProjectTitle);
    }

    // 11. ClientName refleja info del proyecto
    [Fact]
    public void ClientName_WithProject_ReflectsClientName()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        host.Session.NewProject("export-client-test");
        host.Session.ProjectInfo.Cliente = "Inmobiliaria Ejemplo";

        var vm = new ExportViewModel(WpfAppHost.Current);
        vm.Refresh();

        Assert.Equal("Inmobiliaria Ejemplo", vm.ClientName);
    }

    // 12. SelectedScopeMode por defecto es Completo
    [Fact]
    public void SelectedScopeMode_Default_IsCompleto()
    {
        var vm = new ExportViewModel(WpfAppHost.Current);
        Assert.Equal("Completo", vm.SelectedScopeMode);
    }

    // 13. IsPartidaScope false por defecto
    [Fact]
    public void IsPartidaScope_Default_IsFalse()
    {
        var vm = new ExportViewModel(WpfAppHost.Current);
        Assert.False(vm.IsPartidaScope);
    }

    // 14. IsAreaScope false por defecto
    [Fact]
    public void IsAreaScope_Default_IsFalse()
    {
        var vm = new ExportViewModel(WpfAppHost.Current);
        Assert.False(vm.IsAreaScope);
    }

    // 15. Cambiar scope a Partida actualiza IsPartidaScope
    [Fact]
    public void SelectedScopeMode_SetPartida_UpdatesIsPartidaScope()
    {
        var vm = new ExportViewModel(WpfAppHost.Current);
        vm.SelectedScopeMode = "Partida";

        Assert.True(vm.IsPartidaScope);
        Assert.False(vm.IsAreaScope);
    }

    // 16. Cambiar scope a ?rea actualiza IsAreaScope
    [Fact]
    public void SelectedScopeMode_SetArea_UpdatesIsAreaScope()
    {
        var vm = new ExportViewModel(WpfAppHost.Current);
        vm.SelectedScopeMode = "\u00c1rea";

        Assert.True(vm.IsAreaScope);
        Assert.False(vm.IsPartidaScope);
    }

    // 17. ExportScopeSummaryText Completo ? texto por defecto
    [Fact]
    public void ExportScopeSummaryText_Completo_ContainsCompleto()
    {
        var vm = new ExportViewModel(WpfAppHost.Current);
        vm.SelectedScopeMode = "Completo";

        Assert.Contains("completa", vm.ExportScopeSummaryText, StringComparison.OrdinalIgnoreCase);
    }

    // 18. ExportScopeSummaryText Partida sin selecci?n ? pide seleccionar
    [Fact]
    public void ExportScopeSummaryText_Partida_NoSelection_AsksToSelect()
    {
        WpfTestHost.EnsureInitialized();
        var vm = new ExportViewModel(WpfAppHost.Current);
        vm.SelectedScopeMode = "Partida";
        vm.SelectedPartida = null;

        Assert.Contains("partida", vm.ExportScopeSummaryText, StringComparison.OrdinalIgnoreCase);
    }

    // 19. ExportScopeSummaryText ?rea sin selecci?n ? pide seleccionar
    [Fact]
    public void ExportScopeSummaryText_Area_NoSelection_AsksToSelect()
    {
        WpfTestHost.EnsureInitialized();
        var vm = new ExportViewModel(WpfAppHost.Current);
        vm.SelectedScopeMode = "\u00c1rea";
        vm.SelectedArea = "";

        Assert.Contains("\u00e1rea", vm.ExportScopeSummaryText, StringComparison.OrdinalIgnoreCase);
    }

    // 20. ExportActionVm ? propiedades expuestas correctamente
    [Fact]
    public void ExportActionVm_ExposesProperties()
    {
        var cmd = new RelayCommand(() => { });
        var action = new ExportActionVm("Mi acci?n", "Descripci?n larga", "Exportar PDF", cmd, true);

        Assert.Equal("Mi acci?n", action.Title);
        Assert.Equal("Descripci?n larga", action.Description);
        Assert.Equal("Exportar PDF", action.ButtonText);
        Assert.True(action.Primary);
        Assert.Equal("PDF", action.IconText);
    }

    // 21. ExportActionVm ? ?cono derivado del texto del bot?n
    [Fact]
    public void ExportActionVm_IconText_DerivedFromButtonText()
    {
        var cmd = new RelayCommand(() => { });

        var pdf = new ExportActionVm("T", "D", "Exportar PDF", cmd);
        var xls = new ExportActionVm("T", "D", "Exportar Excel", cmd);
        var doc = new ExportActionVm("T", "D", "Exportar DOCX", cmd);
        var zip = new ExportActionVm("T", "D", "Crear ZIP", cmd);

        Assert.Equal("PDF", pdf.IconText);
        Assert.Equal("XLS", xls.IconText);
        Assert.Equal("DOC", doc.IconText);
        Assert.Equal("ZIP", zip.IconText);
    }

    // 22. ExportPackageItemVm ? propiedades expuestas correctamente
    [Fact]
    public void ExportPackageItemVm_ExposesProperties()
    {
        var item = new ExportPackageItemVm("PDF de oferta", "Documento principal");

        Assert.Equal("PDF de oferta", item.Title);
        Assert.Equal("Documento principal", item.Description);
    }

    // 23. Refresh es idempotente ? no lanza excepci?n
    [Fact]
    public void Refresh_CalledTwice_DoesNotThrow()
    {
        var vm = new ExportViewModel(WpfAppHost.Current);
        var ex = Record.Exception(() => { vm.Refresh(); vm.Refresh(); });
        Assert.Null(ex);
    }

    // 24. Refresh repuebla Actions (no acumula)
    [Fact]
    public void Refresh_DoesNotAccumulateActions()
    {
        var vm = new ExportViewModel(WpfAppHost.Current);
        int first = vm.Actions.Count;
        vm.Refresh();
        Assert.Equal(first, vm.Actions.Count);
    }

    // 25. StateText por defecto es Borrador
    [Fact]
    public void StateText_Default_IsBorrador()
    {
        WpfTestHost.EnsureInitialized();
        var vm = new ExportViewModel(WpfAppHost.Current);

        Assert.Equal("Borrador", vm.StateText);
    }

    // 26. Export registrado en ShellRouteCatalog con grupo Cierre
    [Fact]
    public void Export_RegisteredInShellRouteCatalog()
    {
        var info = ShellRouteCatalog.Get(ShellRoute.OfferDocuments);
        Assert.Equal("Cierre", info.Group);
        Assert.Contains("exportación", info.Title, StringComparison.OrdinalIgnoreCase);
    }

    // 27. Sin referencias a System.Windows.Forms ni a WinForms legacy
    [Fact]
    public void ExportViewModel_HasNoWinFormsReference()
    {
        var type = typeof(ExportViewModel);
        var referencedAssemblies = type.Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(referencedAssemblies, a =>
            a.Name != null &&
            (a.Name.Contains("System.Windows.Forms", StringComparison.OrdinalIgnoreCase) ||
             a.Name.Contains("QubiCostElectrical.WinForms", StringComparison.OrdinalIgnoreCase)));
    }

    // 28. CorporateThemeText y ReportTemplateText no son nulos
    [Fact]
    public void CorporateThemeText_And_ReportTemplateText_AreNotNull()
    {
        var vm = new ExportViewModel(WpfAppHost.Current);

        Assert.NotNull(vm.CorporateThemeText);
        Assert.NotNull(vm.ReportTemplateText);
        Assert.NotEmpty(vm.CorporateThemeText);
        Assert.NotEmpty(vm.ReportTemplateText);
    }

    [Fact]
    public void DeliverPackage_IsDisabled_WhenBlockersPresent()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("export-blockers-test");
        host.Session.CurrentProjectPath = "export-blockers-test.qce";
        host.Session.ProjectInfo.Nombre = "Proyecto con bloqueos";
        host.Session.ProjectInfo.Cliente = "Cliente A";
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "T1", Item = "1", Descripcion = "T?tulo 1", EsTitulo = true });
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", ParentId = "T1", Descripcion = "Partida 1", Unidad = "GL" });
        host.Session.Cubicacion.Add(new CubicacionRow { ItemId = "P1", Codigo = "MAT01", Descripcion = "Material 1", Cantidad = 10m, MatUf = 1m, Unidad = "UN" });

        var vm = new ExportViewModel(host);
        vm.Refresh();

        Assert.True(vm.Blockers > 0);
        Assert.False(vm.DeliverPackageCommand.CanExecute(null));
        Assert.Equal("Revisar antes de exportar", vm.ExportReadinessText);
    }

    [Fact]
    public void DeliverPackage_IsDisabled_WhenNoDocumentsSelected()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("export-no-docs-test");
        host.Session.CurrentProjectPath = "export-no-docs-test.qce";
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Partida 1" });
        host.Session.Cubicacion.Add(new CubicacionRow { ItemId = "P1", Codigo = "MAT01", Cantidad = 10m, MatUf = 1m });

        var vm = new ExportViewModel(host);
        foreach (var doc in vm.DocumentSelector.Documents)
            doc.IsSelected = false;
        vm.Refresh();

        Assert.False(vm.DeliverPackageCommand.CanExecute(null));
        Assert.Equal("Selecciona al menos un documento para incluir en el paquete.", vm.DeliverySummaryText);
    }

    [Fact]
    public void IsBusy_StartsFalse()
    {
        var vm = new ExportViewModel(WpfAppHost.Current);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public void OfferSettings_LoadFromSession_Roundtrips()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("export-settings-test");
        host.Session.OfferPdfSettings.CompanyName = "Mi Empresa SpA";
        host.Session.OfferPdfSettings.ValidityDays = 45;

        var vm = new ExportViewModel(host);
        vm.Refresh();

        Assert.Equal("Mi Empresa SpA", vm.OfferSettings.CompanyName);
        Assert.Equal(45, vm.OfferSettings.ValidityDays);
    }

    [Fact]
    public void ExportPriceFreezePolicy_WhenFrozen_DoesNotRequireConfirmation()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("export-freeze-test");
        host.Session.ProjectInfo.PreciosCongelados = true;

        var policy = new ExportPriceFreezePolicy();
        Assert.False(policy.RequiresConfirmation(host.Session.ProjectInfo));
    }

    [Fact]
    public void ExportPriceFreezePolicy_WhenNotFrozen_RequiresConfirmation()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("export-unfrozen-test");
        host.Session.ProjectInfo.PreciosCongelados = false;

        var policy = new ExportPriceFreezePolicy();
        Assert.True(policy.RequiresConfirmation(host.Session.ProjectInfo));
    }
}
