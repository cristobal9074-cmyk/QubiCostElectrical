using System.Collections.Generic;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed class BenchmarkService : IBenchmarkService
{
    private readonly IProjectBenchmarkRepository repository;

    public BenchmarkService(IProjectBenchmarkRepository repository)
    {
        this.repository = repository;
    }

    public async Task<ProjectBenchmarkRow> RecordClosedProjectAsync(
        string projectName,
        string tipologia,
        decimal superficiePisosM2,
        decimal superficieSubterraneoM2,
        CostBreakdown breakdown)
    {
        breakdown ??= new CostBreakdown();
        decimal superficieTotal = superficiePisosM2 + superficieSubterraneoM2;
        decimal ventaSinEquipos = breakdown.TotalVentaUf - breakdown.EquiposUf;

        var row = new ProjectBenchmarkRow
        {
            ProjectName = projectName ?? "",
            Tipologia = tipologia ?? "",
            SuperficiePisosM2 = superficiePisosM2,
            SuperficieSubterraneoM2 = superficieSubterraneoM2,
            VentaTotalClp = breakdown.TotalVentaUf,
            EquiposClp = breakdown.EquiposUf,
            UfM2ConEquipos = superficieTotal > 0m ? breakdown.TotalVentaUf / superficieTotal : 0m,
            UfM2SinEquipos = superficieTotal > 0m ? ventaSinEquipos / superficieTotal : 0m
        };

        await repository.SaveAsync(row);
        return row;
    }

    public Task<BenchmarkStats> GetStatsAsync(string tipologia) => repository.GetStatsAsync(tipologia);

    public Task<IReadOnlyList<ProjectBenchmarkRow>> GetHistoryAsync(string tipologia) => repository.GetByTipologiaAsync(tipologia);
}
