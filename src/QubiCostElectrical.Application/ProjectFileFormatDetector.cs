using System.IO;
using System.Text;
using System.Text.Json;

namespace QubiCostElectrical;

public interface IProjectFileFormatDetector
{
    ProjectFileFormat Detect(string path);
}

public sealed class ProjectFileFormatDetector : IProjectFileFormatDetector
{
    private static readonly byte[] SqliteHeader = Encoding.ASCII.GetBytes("SQLite format 3\0");

    public ProjectFileFormat Detect(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return ProjectFileFormat.Unknown;

        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Detect(stream);
        }
        catch
        {
            return ProjectFileFormat.Unknown;
        }
    }

    private static ProjectFileFormat Detect(Stream stream)
    {
        Span<byte> header = stackalloc byte[SqliteHeader.Length];
        int read = stream.Read(header);
        if (read == SqliteHeader.Length && header.SequenceEqual(SqliteHeader))
            return ProjectFileFormat.Sqlite;

        if (stream.CanSeek)
            stream.Position = 0;

        try
        {
            using var doc = JsonDocument.Parse(
                stream,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });

            return LooksLikeProjectJson(doc.RootElement)
                ? ProjectFileFormat.LegacyJson
                : ProjectFileFormat.Unknown;
        }
        catch
        {
            return ProjectFileFormat.Unknown;
        }
    }

    private static bool LooksLikeProjectJson(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return false;

        if (root.TryGetProperty("Proyecto", out var project) && project.ValueKind == JsonValueKind.Object)
            return true;

        return root.TryGetProperty("Itemizado", out _)
               || root.TryGetProperty("Materiales", out _)
               || root.TryGetProperty("Cubicacion", out _)
               || root.TryGetProperty("Plantillas", out _)
               || root.TryGetProperty("Apus", out _)
               || root.TryGetProperty("Versiones", out _)
               || root.TryGetProperty("OrdenesCambio", out _);
    }
}
