using System;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

// Referencia VIVA a CUALQUIER celda de sector (fila + columna): parado en una casilla, un token
// "F{fila}C{columna}" apunta a la celda exacta de otro recurso de la partida en cualquier
// sector — se recalcula sola porque se re-evalúa la expresión cruda de esa otra celda en cada
// pasada, no un valor congelado. Cubre también los dos riesgos reales de evaluar entre celdas:
// ciclos (A referencia a B que referencia a A) y anidamiento excesivo.
public sealed class CubicacionTakeoffQueryServiceRowReferenceTests
{
    private static BudgetProjectSession CreatePartidaWithRows(string itemId, params (string codigo, string[] expressions)[] rows)
    {
        var session = new BudgetProjectSession();
        session.NewProject("row-reference", "Tester");
        session.Itemizado.Add(new ItemizadoRow { Id = itemId, Item = "1.1", Descripcion = "Partida", Unidad = "UN", Cantidad = 1m });

        foreach (var (codigo, expressions) in rows)
        {
            session.Cubicacion.Add(new CubicacionRow
            {
                Id = Guid.NewGuid().ToString("N"),
                ItemId = itemId,
                Codigo = codigo,
                Descripcion = $"Material {codigo}",
                Unidad = "UN",
                Area = "General",
                QtyExpressions = expressions
            });
        }

        return session;
    }

    [Fact]
    public void SectorConReferenciaAOtraFila_UsaElValorDeEsaCeldaExacta()
    {
        var session = CreatePartidaWithRows("P1",
            ("MAT-A", new[] { "100" }),
            ("MAT-B", new[] { "f1c1+50" }));

        var svc = new CubicacionTakeoffQueryService();
        decimal totalRowB = svc.EvaluateStoredExpressions(session, session.Cubicacion[1], qtyColumnCount: 1);

        // F1C1 = fila 1, columna 1 (MAT-A) = 100 → 100+50 = 150
        Assert.Equal(150m, totalRowB);
    }

    [Fact]
    public void ReferenciaACualquierColumna_NoSoloLaMisma()
    {
        // MAT-A: columna 1 = 10, columna 2 = 20. MAT-B referencia la columna 2 de MAT-A desde
        // su propia columna 1 — cruzando fila Y columna, no solo la misma columna.
        var session = CreatePartidaWithRows("P1",
            ("MAT-A", new[] { "10", "20" }),
            ("MAT-B", new[] { "f1c2+5" }));

        var svc = new CubicacionTakeoffQueryService();
        decimal totalRowB = svc.EvaluateStoredExpressions(session, session.Cubicacion[1], qtyColumnCount: 2);

        // F1C2 = fila 1, columna 2 = 20 → 20+5 = 25 (la columna 2 de MAT-B está vacía)
        Assert.Equal(25m, totalRowB);
    }

    [Fact]
    public void SectorConReferencia_SeRecalculaSolo_SiLaOtraCeldaCambia()
    {
        var session = CreatePartidaWithRows("P1",
            ("MAT-A", new[] { "100" }),
            ("MAT-B", new[] { "f1c1*2" }));

        var svc = new CubicacionTakeoffQueryService();
        decimal before = svc.EvaluateStoredExpressions(session, session.Cubicacion[1], qtyColumnCount: 1);
        Assert.Equal(200m, before);

        // Cambia la celda referenciada, SIN tocar la fórmula de la fila B — como es una
        // referencia viva (no un valor copiado), el resultado debe reflejar el cambio.
        session.Cubicacion[0].QtyExpressions = new[] { "300" };
        decimal after = svc.EvaluateStoredExpressions(session, session.Cubicacion[1], qtyColumnCount: 1);
        Assert.Equal(600m, after);
    }

    [Fact]
    public void ReferenciaCircularEntreDosCeldas_NoCuelgaYDaCero()
    {
        // Fila 1 referencia a la fila 2, que a su vez referencia a la fila 1 — un ciclo real.
        var session = CreatePartidaWithRows("P1",
            ("MAT-A", new[] { "f2c1+1" }),
            ("MAT-B", new[] { "f1c1+1" }));

        var svc = new CubicacionTakeoffQueryService();

        // No debe lanzar StackOverflow ni colgarse; el ciclo se corta leyendo 0 en el punto
        // de reentrada.
        decimal totalA = svc.EvaluateStoredExpressions(session, session.Cubicacion[0], qtyColumnCount: 1);
        decimal totalB = svc.EvaluateStoredExpressions(session, session.Cubicacion[1], qtyColumnCount: 1);

        Assert.True(totalA is 1m or 2m);
        Assert.True(totalB is 1m or 2m);
    }

    [Fact]
    public void ReferenciaACeldaVacia_LeeCero()
    {
        var session = CreatePartidaWithRows("P1",
            ("MAT-A", new[] { "" }),
            ("MAT-B", new[] { "f1c1+10" }));

        var svc = new CubicacionTakeoffQueryService();
        decimal total = svc.EvaluateStoredExpressions(session, session.Cubicacion[1], qtyColumnCount: 1);

        Assert.Equal(10m, total);
    }

    [Fact]
    public void CadenaDeReferenciasLarga_DentroDelLimite_SeResuelveCorrectamente()
    {
        // F1C1 = 1, F2C1 = f1c1+1 = 2, F3C1 = f2c1+1 = 3, F4C1 = f3c1+1 = 4 — encadenamiento
        // normal, no circular.
        var session = CreatePartidaWithRows("P1",
            ("MAT-1", new[] { "1" }),
            ("MAT-2", new[] { "f1c1+1" }),
            ("MAT-3", new[] { "f2c1+1" }),
            ("MAT-4", new[] { "f3c1+1" }));

        var svc = new CubicacionTakeoffQueryService();
        decimal total = svc.EvaluateStoredExpressions(session, session.Cubicacion[3], qtyColumnCount: 1);

        Assert.Equal(4m, total);
    }

    [Fact]
    public void ExpresionSinReferencias_NoSeVeAfectadaPorElMecanismo()
    {
        var session = CreatePartidaWithRows("P1", ("MAT-A", new[] { "2+3" }));

        var svc = new CubicacionTakeoffQueryService();
        decimal total = svc.EvaluateStoredExpressions(session, session.Cubicacion[0], qtyColumnCount: 1);

        Assert.Equal(5m, total);
    }
}
