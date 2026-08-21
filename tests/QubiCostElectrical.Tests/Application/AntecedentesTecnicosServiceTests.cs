using System;
using System.Collections.Generic;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class AntecedentesTecnicosServiceTests
{
    private static readonly AntecedentesTecnicosService Svc = new();

    [Fact]
    public void GetVigenteRevision_ReturnsLatestVigente()
    {
        var rows = new List<AntecedenteTecnicoRow>
        {
            new() { Tipo = AntecedenteTecnicoTipo.Plano, Codigo = "E-01", Revision = "A", EsVigente = false },
            new() { Tipo = AntecedenteTecnicoTipo.Plano, Codigo = "E-01", Revision = "B", EsVigente = true }
        };

        Assert.Equal("B", Svc.GetVigenteRevision(rows, "E-01"));
    }

    [Fact]
    public void FindMedicionesSinRegistro_FlagsUnknownPlanoOrRevision()
    {
        var antecedentes = new List<AntecedenteTecnicoRow>
        {
            new() { Tipo = AntecedenteTecnicoTipo.Plano, Codigo = "E-01", Revision = "B", EsVigente = true }
        };
        var mediciones = new List<MeasurementLineRow>
        {
            new() { Plano = "E-01", RevisionPlano = "A" },
            new() { Plano = "E-99", RevisionPlano = "0" }
        };

        var issues = Svc.FindMedicionesSinRegistro(mediciones, antecedentes);
        Assert.Equal(2, issues.Count);
    }

    [Fact]
    public void BuildExclusionsDraft_UsesMarkedRows()
    {
        var rows = new List<AntecedenteTecnicoRow>
        {
            new() { Tipo = AntecedenteTecnicoTipo.Aclaracion, Codigo = "EX-1", EsExclusion = true, Descripcion = "No incluye empalmes" }
        };

        string text = Svc.BuildExclusionsDraft(rows);
        Assert.Contains("empalmes", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CountConsultasAbiertas_CountsOnlyPendingConsultas()
    {
        var rows = new List<AntecedenteTecnicoRow>
        {
            new() { Tipo = AntecedenteTecnicoTipo.Consulta, EsPendienteMandante = true },
            new() { Tipo = AntecedenteTecnicoTipo.Consulta, EsPendienteMandante = false },
            new() { Tipo = AntecedenteTecnicoTipo.Plano, EsPendienteMandante = true }
        };

        Assert.Equal(1, Svc.CountConsultasAbiertas(rows));
    }
}
