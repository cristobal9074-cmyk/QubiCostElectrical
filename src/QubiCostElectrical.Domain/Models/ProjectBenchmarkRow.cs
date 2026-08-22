using System;

namespace QubiCostElectrical;

/// <summary>
/// Ficha histórica de un proyecto cerrado, usada para comparar UF/m² contra la cartera propia
/// de la empresa — equivalente al bloque "DISEÑO DEL EDIFICIO / Valor UF/m²" del Excel de costos.
/// </summary>
public sealed class ProjectBenchmarkRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ProjectName { get; set; } = "";
    public string Tipologia { get; set; } = "";
    public decimal SuperficiePisosM2 { get; set; }
    public decimal SuperficieSubterraneoM2 { get; set; }
    public decimal VentaTotalClp { get; set; }
    public decimal EquiposClp { get; set; }
    public decimal UfM2ConEquipos { get; set; }
    public decimal UfM2SinEquipos { get; set; }
    public DateTime FechaCierre { get; set; } = AppClock.Now;

    public decimal SuperficieTotalM2 => SuperficiePisosM2 + SuperficieSubterraneoM2;
}

/// <summary>Estadística agregada de UF/m² para una tipología, calculada sobre la cartera cerrada.</summary>
public sealed class BenchmarkStats
{
    public string Tipologia { get; set; } = "";
    public int Muestras { get; set; }
    public decimal PromedioUfM2 { get; set; }
    public decimal MinUfM2 { get; set; }
    public decimal MaxUfM2 { get; set; }
}

public interface IProjectBenchmarkRepository
{
    Task SaveAsync(ProjectBenchmarkRow row);
    Task<IReadOnlyList<ProjectBenchmarkRow>> GetByTipologiaAsync(string tipologia);
    Task<IReadOnlyList<ProjectBenchmarkRow>> GetAllAsync();
    Task<BenchmarkStats> GetStatsAsync(string tipologia);
}
