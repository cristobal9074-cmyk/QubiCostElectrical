using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PDFtoImage;
using SkiaSharp;

namespace QubiCostElectrical.Wpf.Infrastructure;

public static class PdfPageRenderer
{
    public static int GetPageCount(string pdfPath)
    {
        if (!File.Exists(pdfPath))
            return 0;

        using var stream = File.OpenRead(pdfPath);
        return Conversion.GetPageCount(stream);
    }

    public static ImageSource? RenderPage(string pdfPath, int pageIndex, int width = 1800)
    {
        if (!File.Exists(pdfPath))
            return null;

        int pageCount = GetPageCount(pdfPath);
        if (pageIndex < 0 || pageIndex >= pageCount)
            return null;

        using var stream = File.OpenRead(pdfPath);
        using var bitmap = Conversion.ToImage(stream, page: pageIndex, options: new(Dpi: 200));
        if (bitmap == null)
            return null;

        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 90);
        using var memory = new MemoryStream(encoded.ToArray());

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = memory;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
