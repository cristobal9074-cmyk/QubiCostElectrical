using System;
using System.Collections.Generic;
using QubiCostElectrical;

namespace QubiCostElectrical.Tests.Fixtures.MigrationCorpus;

/// <summary>
/// Proyecto sintético para recorrer los pasos de esquema 1..32.
/// El archivo real de obra está en corpus/PEL039A2026-ABIERTO.qce y lo exige
/// ReferenceProject_PEL039A2026_CommittedCorpus_SurvivesMigrationAndRoundtrip.
/// </summary>
public static class PEL039ReferenceProjectFixture
{
    public const string LiveDefaultPath = @"C:\Users\cristobal1\Documents\Presupuestos Electricos\PEL039A2026 ABIERTO.qce";

    public static ProjectFileData CreateSyntheticReferenceProject()
    {
        var project = new ProjectFileData
        {
            Proyecto = new ProjectInfoRow
            {
                Nombre = "PEL039A2026 ABIERTO",
                Cliente = "Cliente sin nombre",
                MonedaPresupuesto = "CLP",
                ValorUfClp = 38000m,
                TipoCambioUsdClp = 950m,
                FechaTipoCambio = DateTime.Today,
                GastosGeneralesPct = 10m,
                UtilidadPct = 12m,
                IvaPct = 19m,
                IncluirIvaEnTotal = true
            }
        };

        // Generar las 69 partidas del itemizado representativas
        for (int i = 1; i <= 69; i++)
        {
            bool esTitulo = i % 10 == 1;
            project.Itemizado.Add(new ItemizadoRow
            {
                Id = $"P{i:D3}",
                Item = esTitulo ? $"{i / 10 + 1}" : $"{i / 10 + 1}.{i % 10}",
                Descripcion = esTitulo ? $"Capítulo Eléctrico {i / 10 + 1}" : $"Partida de Instalación {i}",
                Unidad = esTitulo ? "" : "GL",
                Cantidad = esTitulo ? 0m : 1m,
                EsTitulo = esTitulo
            });
        }

        // Generar los 70 materiales representativos
        for (int m = 1; m <= 70; m++)
        {
            project.Materiales.Add(new MaterialRow
            {
                Codigo = $"MAT-ELEC-{m:D3}",
                Descripcion = $"Material Eléctrico Especializado {m}",
                Unidad = "UN",
                Categoria = m <= 30 ? "Canalizaciones" : "Conductores",
                MatUf = 0.5m * m,
                MoUf = 0.2m * m,
                MatClp = 19000m * m,
                MoClp = 7600m * m
            });
        }

        // Generar las 219 líneas de cubicación representativas vinculadas a las partidas no-título
        int cubIndex = 1;
        var nonTitles = project.Itemizado.FindAll(p => !p.EsTitulo);
        for (int c = 1; c <= 219; c++)
        {
            var partida = nonTitles[c % nonTitles.Count];
            var material = project.Materiales[c % project.Materiales.Count];

            project.Cubicacion.Add(new CubicacionRow
            {
                Id = $"CUB-{cubIndex++:D4}",
                ItemId = partida.Id,
                Codigo = material.Codigo,
                Descripcion = material.Descripcion,
                Unidad = material.Unidad,
                Cantidad = 5m + (c % 20),
                MatUf = material.MatUf,
                MoUf = material.MoUf,
                MatClp = material.MatClp,
                MoClp = material.MoClp,
                Area = $"Sector {1 + (c % 5)}"
            });
        }

        return project;
    }
}
