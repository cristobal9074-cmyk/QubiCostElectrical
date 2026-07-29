using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

// Regresión: un sector puede referenciar el valor de un sector ANTERIOR de la misma fila por su
// nombre de encabezado (ej. sector "S2" con fórmula "=s1*2"), igual que una fórmula de Excel que
// referencia una celda a su izquierda. Antes, todos los sectores se evaluaban primero y recién
// después se publicaban como variables, así que ninguno podía ver el valor de otro.
public sealed class CubicacionTakeoffQueryServiceCrossSectorTests
{
    private static (BudgetProjectSession session, CubicacionRow row) CreateRowWithHeaders(string[] headers, string[] expressions)
    {
        var session = new BudgetProjectSession();
        session.NewProject("cross-sector", "Tester");

        var itemId = "partida-cross-sector";
        session.Itemizado.Add(new ItemizadoRow
        {
            Id = itemId,
            Item = "1.1",
            Descripcion = "Partida de prueba",
            Unidad = "UN",
            Cantidad = 1m
        });
        session.QtyHeadersByItem[itemId] = headers;

        var row = new CubicacionRow
        {
            Id = System.Guid.NewGuid().ToString("N"),
            ItemId = itemId,
            Codigo = "MAT-001",
            Descripcion = "Material de prueba",
            Unidad = "UN",
            Area = "General",
            QtyExpressions = expressions
        };
        session.Cubicacion.Add(row);
        return (session, row);
    }

    [Fact]
    public void EvaluateStoredExpressions_SectorReferenciaSectorAnterior_UsaSuValor()
    {
        var (session, row) = CreateRowWithHeaders(
            headers: new[] { "S1", "S2" },
            expressions: new[] { "10", "s1*2" });

        var svc = new CubicacionTakeoffQueryService();
        decimal total = svc.EvaluateStoredExpressions(session, row, qtyColumnCount: 2);

        // S1 = 10, S2 = s1*2 = 20 → total = 30
        Assert.Equal(30m, total);
    }

    [Fact]
    public void EvaluateStoredExpressions_EncabezadoConGuion_SeNormalizaComoVariable()
    {
        var (session, row) = CreateRowWithHeaders(
            headers: new[] { "TA-P1", "TA-P2" },
            expressions: new[] { "5", "ta_p1+1" });

        var svc = new CubicacionTakeoffQueryService();
        decimal total = svc.EvaluateStoredExpressions(session, row, qtyColumnCount: 2);

        // TA-P1 = 5, TA-P2 = ta_p1+1 = 6 → total = 11
        Assert.Equal(11m, total);
    }

    [Fact]
    public void EvaluateStoredExpressions_SectorReferenciaUnoPosterior_LeeCero()
    {
        var (session, row) = CreateRowWithHeaders(
            headers: new[] { "S1", "S2" },
            expressions: new[] { "s2+1", "10" });

        var svc = new CubicacionTakeoffQueryService();
        decimal total = svc.EvaluateStoredExpressions(session, row, qtyColumnCount: 2);

        // S1 = "s2+1" referencia un sector POSTERIOR que aún no fue calculado en este
        // recorrido: la variable "s2" no está definida todavía, la fórmula falla y S1 no
        // aporta al total (igual que un #NAME? en Excel, mostrado como HasError en la fila).
        // S2 = 10 → total = 10.
        Assert.Equal(10m, total);
    }
}
