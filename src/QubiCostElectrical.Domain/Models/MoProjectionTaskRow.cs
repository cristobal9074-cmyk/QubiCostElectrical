using System;

namespace QubiCostElectrical;

public sealed class MoProjectionTaskRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Nombre { get; set; } = "";
    public int[] CantidadesMensuales { get; set; } = Array.Empty<int>();
}
