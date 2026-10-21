using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

/// <summary>
/// Los pasos 1..33 quedan congelados. Un archivo viejo los vuelve a correr.
/// El cambio siguiente se agrega después del marcador FROZEN_THROUGH: 33.
/// </summary>
public sealed class FrozenMigrationStepsTests
{
    private const string FrozenSha256 = "0e0645132d3424e02f78ce4c6d98e02c01722babbf2a57f4ebe9aefe8e381cf6";

    [Fact]
    public void Steps001Through033_AreFrozen()
    {
        string path = FindStepsFile();
        string text = File.ReadAllText(path).Replace("\r\n", "\n").Replace('\r', '\n');
        int start = text.IndexOf("void Migration001_BaseInicial", StringComparison.Ordinal);
        int end = text.IndexOf("FROZEN_THROUGH: 33", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "Falta el inicio de Migration001 o el marcador FROZEN_THROUGH: 33.");

        string slice = text[start..end];
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(slice))).ToLowerInvariant();
        Assert.True(hash == FrozenSha256,
            "Se reescribió un paso de migración entre el 1 y el 33. No se edita un paso ya publicado. "
            + "El cambio de base siguiente es Migration034, después del marcador FROZEN_THROUGH: 33. "
            + $"Hash actual: {hash}");
    }

    private static string FindStepsFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "src", "QubiCostElectrical.Infrastructure", "SQLite", "SqliteMigrationSteps.Methods.cs");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("No está SqliteMigrationSteps.Methods.cs.");
    }
}
