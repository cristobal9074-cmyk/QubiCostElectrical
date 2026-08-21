using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Delivery;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class DeliveryFrozenContextTests
{
    public DeliveryFrozenContextTests()
    {
        WpfTestHost.EnsureInitialized();
    }

    [Fact]
    public void ReviewSummary_Frozen_DoesNotChangeAfterSourceModified()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("frozen-test");
        host.Session.CurrentProjectPath = "frozen-test.qce";
        host.Session.ProjectInfo.Nombre = "Frozen";
        host.Session.ProjectInfo.Cliente = "Cliente";
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "T1", Item = "1", Descripcion = "Título", EsTitulo = true });
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", ParentId = "T1", Descripcion = "Partida 1", Unidad = "GL" });
        host.Session.Cubicacion.Add(new CubicacionRow { ItemId = "P1", Codigo = "MAT01", Descripcion = "Mat 1", Cantidad = 10m, MatUf = 1m, Unidad = "UN" });

        var selector = new DeliveryDocumentSelectorVm(WpfAppHost.Current);
        var ctx = selector.GetSelectedContext(host.Session);

        var beforeBlockers = ctx.ReviewSummary?.BlockingCount ?? -1;

        host.Session.Cubicacion.Clear();
        host.Session.MarkDirty("cubicacion");

        var afterBlockers = ctx.ReviewSummary?.BlockingCount ?? -1;

        Assert.Equal(beforeBlockers, afterBlockers);
    }

    [Fact]
    public void ReviewSummary_Frozen_IssuesListIsIndependent()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("independent-test");
        host.Session.CurrentProjectPath = "independent-test.qce";
        host.Session.ProjectInfo.Nombre = "Independent";
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Partida 1" });
        host.Session.Cubicacion.Add(new CubicacionRow { ItemId = "P1", Codigo = "MAT01", Cantidad = 10m, MatUf = 1m });

        var selector = new DeliveryDocumentSelectorVm(WpfAppHost.Current);
        var ctx1 = selector.GetSelectedContext(host.Session);
        var ctx2 = selector.GetSelectedContext(host.Session);

        var count1 = ctx1.ReviewSummary?.Issues.Count ?? -1;
        var count2 = ctx2.ReviewSummary?.Issues.Count ?? -1;

        Assert.Equal(count1, count2);
    }
}

public sealed class DeliveryFileOpenTests
{
    [Fact]
    public void FileOpenResult_FileNotFound_ReturnsCorrectEnum()
    {
        var service = new FakeFileOpenService();
        var result = service.OpenFile("C:\\nonexistent\\file.pdf");
        Assert.Equal(FileOpenResult.FileNotFound, result);
    }

    [Fact]
    public void FileOpenResult_GeneratedAndOpened_Success()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var service = new FakeFileOpenService();
            var result = service.OpenFile(tempFile);
            Assert.Equal(FileOpenResult.Success, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void FileOpenResult_ViewerFailed_ForUnsupportedExtension()
    {
        var tempDir = Path.GetTempPath();
        var tempFile = Path.Combine(tempDir, "test_" + Guid.NewGuid().ToString("N") + ".xyz");
        File.WriteAllText(tempFile, "dummy");
        try
        {
            var service = new FakeFileOpenService(failExtensions: new[] { ".xyz" });
            var result = service.OpenFile(tempFile);
            Assert.Equal(FileOpenResult.ViewerFailed, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void DeliveryExportResult_WithOpenResult_StoresCorrectly()
    {
        var r1 = new DeliveryExportResult("OfferPdf", true, "test.pdf", openResult: FileOpenResult.Success);
        var r2 = new DeliveryExportResult("OfferPdf", true, "test.pdf", openResult: FileOpenResult.ViewerFailed);
        var r3 = new DeliveryExportResult("OfferPdf", false, errorMessage: "Error");

        Assert.Equal(FileOpenResult.Success, r1.OpenResult);
        Assert.True(r1.IsSuccess);

        Assert.Equal(FileOpenResult.ViewerFailed, r2.OpenResult);
        Assert.True(r2.IsSuccess);

        Assert.Equal(FileOpenResult.Success, r3.OpenResult);
        Assert.False(r3.IsSuccess);
    }

    [Fact]
    public void DeliveryExportResultVm_SetPreview_StoresOpenResult()
    {
        var vm = new DeliveryExportResultVm();
        vm.SetPreview("test.pdf", FileOpenResult.ViewerFailed);

        Assert.Equal("test.pdf", vm.PreviewFilePath);
        Assert.Equal(FileOpenResult.ViewerFailed, vm.PreviewOpenResult);

        vm.Clear();
        Assert.Null(vm.PreviewFilePath);
        Assert.Equal(FileOpenResult.Success, vm.PreviewOpenResult);
    }
}

public sealed class DeliveryCleanupTests
{
    [Fact]
    public void CleanupOldPreviews_DoesNotDeleteRecentFolders()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "QubiCostElectrical", "Preview", "cleanup-test-recent");
        try
        {
            var recentDir = Path.Combine(tempRoot, "recent-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(recentDir);

            DeliveryPreviewServiceTestsAccess.CleanupOldPreviews();

            Assert.True(Directory.Exists(recentDir), "Recent folder should not be deleted");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void CleanupOldPreviews_FailureDoesNotThrow()
    {
        DeliveryPreviewServiceTestsAccess.CleanupOldPreviews();
    }
}

public sealed class FakeFileOpenService : IFileOpenService
{
    private readonly HashSet<string>? failExtensions;

    public FakeFileOpenService(string[]? failExtensions = null)
    {
        this.failExtensions = failExtensions != null ? new HashSet<string>(failExtensions, StringComparer.OrdinalIgnoreCase) : null;
    }

    public FileOpenResult OpenFile(string filePath)
    {
        if (!File.Exists(filePath))
            return FileOpenResult.FileNotFound;
        if (failExtensions != null)
        {
            var ext = Path.GetExtension(filePath);
            if (failExtensions.Contains(ext))
                return FileOpenResult.ViewerFailed;
        }
        return FileOpenResult.Success;
    }

    public void OpenFolder(string folderPath) { }
}

internal static class DeliveryPreviewServiceTestsAccess
{
    public static void CleanupOldPreviews() => DeliveryPreviewService.CleanupOldPreviews();
}
