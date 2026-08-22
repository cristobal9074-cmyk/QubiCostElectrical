using System.Collections.Generic;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IBenchmarkService
{
    /// <summary>Registra el proyecto cerrado (nombre, tipología, superficies) usando el
    /// CostBreakdown ya calculado como fuente del UF/m².</summary>
    Task<ProjectBenchmarkRow> RecordClosedProjectAsync(
        string projectName,
        string tipologia,
        decimal superficiePisosM2,
        decimal superficieSubterraneoM2,
        CostBreakdown breakdown);

    Task<BenchmarkStats> GetStatsAsync(string tipologia);

    Task<IReadOnlyList<ProjectBenchmarkRow>> GetHistoryAsync(string tipologia);
}
