using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed partial class BudgetEngine
{
    private readonly HashSet<string> dirtyItems = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Hist.: lista granular de partidas modificadas. La UI marca aquí el ItemId editado
    /// y el cálculo incremental intenta recomponer el presupuesto sin recorrer todo cuando es seguro.
    /// </summary>
    public IReadOnlyCollection<string> DirtyItems => dirtyItems.ToArray();
}
