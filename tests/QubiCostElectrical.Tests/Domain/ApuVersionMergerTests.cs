using System.Collections.Generic;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

/// <summary>
/// Merge de 3 vías de versiones de APU (left/right/base). Cubre especialmente el camino de
/// <c>MergeResourceRefs</c> donde un recurso existe solo en una rama (left u right) mientras la
/// otra no lo tiene: ese es el patrón "declarada temprano, asignada tarde" (variable de salida de
/// TryGetValue reutilizada entre ramas `if`/`else if`) que motivó dejar este archivo con la
/// supresión de nulabilidad intacta hasta ahora.
/// </summary>
public sealed class ApuVersionMergerTests
{
    private static ApuVersion MakeVersion(IReadOnlyList<ApuVersionResourceRef> resourceRefs)
    {
        return new ApuVersion
        {
            ResourceRefs = resourceRefs
        };
    }

    private static ApuVersionResourceRef MakeResource(string id, decimal price)
    {
        return new ApuVersionResourceRef { ResourceId = id, Price = price };
    }

    [Fact]
    public void Merge_ResourceOnlyInLeft_KeepsLeftResourceWithoutConflict()
    {
        // "R1" solo existe en left (no en right ni en base): ejercita la rama
        // `else if (dictionary2.TryGetValue(item2, out value))` de MergeResourceRefs, la que usaba
        // la variable `value5` declarada temprano y asignada recién en esa rama.
        var baseVersion = MakeVersion(new List<ApuVersionResourceRef>());
        var left = MakeVersion(new List<ApuVersionResourceRef> { MakeResource("R1", 100m) });
        var right = MakeVersion(new List<ApuVersionResourceRef>());

        var merger = new ApuVersionMerger();
        MergeResult result = merger.Merge(left, right, baseVersion, "tester", "test merge");

        Assert.True(result.Success);
        Assert.Empty(result.Conflicts);
        ApuVersionResourceRef merged = Assert.Single(result.MergedVersion.ResourceRefs);
        Assert.Equal("R1", merged.ResourceId);
        Assert.Equal(100m, merged.Price);
    }

    [Fact]
    public void Merge_ResourceOnlyInRight_AddsItAndRecordsAppliedChange()
    {
        // Caso simétrico: "R2" solo existe en right. Ejercita la rama `else` (dictionary3[item2])
        // de MergeResourceRefs.
        var baseVersion = MakeVersion(new List<ApuVersionResourceRef>());
        var left = MakeVersion(new List<ApuVersionResourceRef>());
        var right = MakeVersion(new List<ApuVersionResourceRef> { MakeResource("R2", 50m) });

        var merger = new ApuVersionMerger();
        MergeResult result = merger.Merge(left, right, baseVersion, "tester", "test merge");

        Assert.True(result.Success);
        Assert.Empty(result.Conflicts);
        ApuVersionResourceRef merged = Assert.Single(result.MergedVersion.ResourceRefs);
        Assert.Equal("R2", merged.ResourceId);
        Assert.Contains(result.AppliedChanges, c => c.Contains("R2") && c.Contains("added from right branch"));
    }

    [Fact]
    public void Merge_ResourcePriceConflictOnBothSides_RaisesManualConflict()
    {
        // "R3" existe en las 3 versiones, con precios distintos en left y right, ninguno igual al
        // base: debe generar un conflicto manual (no resuelto automáticamente) y no debe perder el
        // recurso de la lista fusionada.
        var baseVersion = MakeVersion(new List<ApuVersionResourceRef> { MakeResource("R3", 10m) });
        var left = MakeVersion(new List<ApuVersionResourceRef> { MakeResource("R3", 20m) });
        var right = MakeVersion(new List<ApuVersionResourceRef> { MakeResource("R3", 30m) });

        var merger = new ApuVersionMerger();
        MergeResult result = merger.Merge(left, right, baseVersion, "tester", "test merge");

        Assert.False(result.Success);
        MergeConflict conflict = Assert.Single(result.Conflicts);
        Assert.Equal("R3", conflict.Identifier);
        Assert.Equal("Price", conflict.Property);
        Assert.False(conflict.IsResolved);
        ApuVersionResourceRef merged = Assert.Single(result.MergedVersion.ResourceRefs);
        Assert.Equal("R3", merged.ResourceId);
    }
}
