using System;
using System.IO;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

public sealed class EconomicIndicatorCacheTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"indicadores_test_{Guid.NewGuid():N}.json");

    [Fact]
    public void TryRead_WhenFileNotExists_ReturnsNull()
    {
        using var scope = EconomicIndicatorCache.UseCachePathForTests(TempPath());
        Assert.Null(EconomicIndicatorCache.TryRead());
    }

    [Fact]
    public void Write_And_TryRead_RoundTrips()
    {
        using var scope = EconomicIndicatorCache.UseCachePathForTests(TempPath());
        string path = EconomicIndicatorCache.CachePath;
        try
        {
            decimal uf  = 37500.50m;
            decimal usd = 945.25m;
            var at  = new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc);

            EconomicIndicatorCache.Write(uf, usd, at);
            var result = EconomicIndicatorCache.TryRead();

            Assert.NotNull(result);
            Assert.Equal(uf,  result!.UfClp);
            Assert.Equal(usd, result.UsdClp);
            Assert.Equal(at,  result.FetchedAt.ToUniversalTime());
        }
        finally
        {
            RestoreOrDelete(path);
        }
    }

    [Fact]
    public void TryRead_WithZeroUf_ReturnsNull()
    {
        using var scope = EconomicIndicatorCache.UseCachePathForTests(TempPath());
        string path = EconomicIndicatorCache.CachePath;
        try
        {
            EconomicIndicatorCache.Write(0m, 900m, DateTime.UtcNow);
            Assert.Null(EconomicIndicatorCache.TryRead());
        }
        finally
        {
            RestoreOrDelete(path);
        }
    }

    [Fact]
    public void TryRead_WithCorruptFile_ReturnsNull()
    {
        using var scope = EconomicIndicatorCache.UseCachePathForTests(TempPath());
        string path = EconomicIndicatorCache.CachePath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{ not valid json {{{{");
            Assert.Null(EconomicIndicatorCache.TryRead());
        }
        finally
        {
            RestoreOrDelete(path);
        }
    }

    [Fact]
    public void IsStale_WithOldDate_ReturnsTrue()
    {
        var old = DateTime.UtcNow.AddHours(-(EconomicIndicatorCache.StaleHours + 1));
        Assert.True(EconomicIndicatorCache.IsStale(old));
    }

    [Fact]
    public void IsStale_WithRecentDate_ReturnsFalse()
    {
        var recent = DateTime.UtcNow.AddHours(-1);
        Assert.False(EconomicIndicatorCache.IsStale(recent));
    }

    [Fact]
    public void IsStale_WithMinValue_ReturnsTrue()
    {
        Assert.True(EconomicIndicatorCache.IsStale(DateTime.MinValue));
    }

    [Fact]
    public void Write_IdempotentOverwrite()
    {
        using var scope = EconomicIndicatorCache.UseCachePathForTests(TempPath());
        string path = EconomicIndicatorCache.CachePath;
        try
        {
            var at = new DateTime(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc);
            EconomicIndicatorCache.Write(36000m, 900m, at);
            EconomicIndicatorCache.Write(37500m, 950m, at);
            var result = EconomicIndicatorCache.TryRead();
            Assert.Equal(37500m, result!.UfClp);
            Assert.Equal(950m, result.UsdClp);
        }
        finally
        {
            RestoreOrDelete(path);
        }
    }

    private static void RestoreOrDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
        }
        catch { }
    }
}
