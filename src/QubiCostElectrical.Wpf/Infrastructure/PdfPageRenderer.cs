using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PDFtoImage;
using QubiCostElectrical;
using SkiaSharp;

namespace QubiCostElectrical.Wpf.Infrastructure;

public readonly record struct PdfRenderResult(int PageCount, ImageSource? Image);

public static class PdfPageRenderer
{
    private const int DefaultPreviewDpi = 110;

    public static int GetPageCount(string pdfPath)
    {
        if (!File.Exists(pdfPath))
            return 0;

        try
        {
            using var stream = File.OpenRead(pdfPath);
            return Conversion.GetPageCount(stream);
        }
        catch
        {
            return 0;
        }
    }

    public static async System.Threading.Tasks.Task<PdfRenderResult> RenderPageAsync(
        string pdfPath,
        int pageIndex,
        int dpi = DefaultPreviewDpi,
        System.Threading.CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            return new PdfRenderResult(0, null);

        return await System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                ct.ThrowIfCancellationRequested();

                // Conversion.GetPageCount cierra el stream que recibe (PDFtoImage 5.x);
                // el conteo se obtiene en un stream aparte y ToImage abre el suyo propio.
                int pageCount = GetPageCount(pdfPath);
                if (pageCount <= 0 || pageIndex < 0 || pageIndex >= pageCount)
                    return new PdfRenderResult(pageCount, null);

                ct.ThrowIfCancellationRequested();

                using var stream = new FileStream(
                    pdfPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: 64 * 1024,
                    useAsync: false);

                using var bitmap = Conversion.ToImage(stream, page: pageIndex, options: new(Dpi: dpi));
                if (bitmap == null)
                    return new PdfRenderResult(pageCount, null);

                ct.ThrowIfCancellationRequested();

                // JPEG 85 es hasta 10 veces más rápido de codificar en CPU que PNG para planos grandes
                using var encoded = bitmap.Encode(SKEncodedImageFormat.Jpeg, 85);
                if (encoded == null)
                    return new PdfRenderResult(pageCount, null);

                using var memory = new MemoryStream(encoded.ToArray());
                ct.ThrowIfCancellationRequested();

                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = memory;
                image.EndInit();
                image.Freeze(); // Seguro para transferir entre hilos

                return new PdfRenderResult(pageCount, image);
            }
            catch (System.OperationCanceledException)
            {
                return new PdfRenderResult(0, null);
            }
            catch (System.Exception ex)
            {
                AppLog.Error("PdfRender", $"No se pudo rasterizar la página {pageIndex + 1} de '{Path.GetFileName(pdfPath)}'.", ex);
                return new PdfRenderResult(0, null);
            }
        }, ct).ConfigureAwait(false);
    }

    public static ImageSource? RenderPage(string pdfPath, int pageIndex, int width = 1800)
    {
        if (!File.Exists(pdfPath))
            return null;

        try
        {
            int pageCount = GetPageCount(pdfPath);
            if (pageIndex < 0 || pageIndex >= pageCount)
                return null;

            using var stream = File.OpenRead(pdfPath);
            using var bitmap = Conversion.ToImage(stream, page: pageIndex, options: new(Dpi: DefaultPreviewDpi));
            if (bitmap == null)
                return null;

            using var encoded = bitmap.Encode(SKEncodedImageFormat.Jpeg, 85);
            using var memory = new MemoryStream(encoded.ToArray());

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = memory;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (System.Exception ex)
        {
            AppLog.Error("PdfRender", $"No se pudo rasterizar la página {pageIndex + 1} de '{Path.GetFileName(pdfPath)}'.", ex);
            return null;
        }
    }
}
