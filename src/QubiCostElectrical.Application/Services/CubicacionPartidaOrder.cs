using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

/// <summary>
/// Orden de las filas de una partida dentro de la lista global de cubicación.
/// Mover, insertar y pegar usan la posición dentro de la partida. Nunca el vecino
/// inmediato de la lista global: entre dos filas de la misma partida puede haber
/// filas de otra.
/// </summary>
public static class CubicacionPartidaOrder
{
    public static int Position(IList<CubicacionRow> rows, string? itemId, CubicacionRow row)
    {
        if (rows == null || row == null)
            return -1;

        int pos = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (!Belongs(rows[i], itemId))
                continue;
            if (ReferenceEquals(rows[i], row) || SameId(rows[i], row))
                return pos;
            pos++;
        }

        return -1;
    }

    public static bool CanMove(IList<CubicacionRow> rows, string? itemId, CubicacionRow row, int direction)
    {
        if (direction == 0)
            return false;
        int pos = Position(rows, itemId, row);
        if (pos < 0)
            return false;
        int next = pos + (direction < 0 ? -1 : 1);
        return next >= 0 && next < Count(rows, itemId);
    }

    public static bool TryMove(IList<CubicacionRow> rows, string? itemId, CubicacionRow row, int direction)
    {
        if (rows == null || !CanMove(rows, itemId, row, direction))
            return false;

        int step = direction < 0 ? -1 : 1;
        int pos = Position(rows, itemId, row);
        int from = -1;
        int to = -1;
        int seen = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (!Belongs(rows[i], itemId))
                continue;
            if (seen == pos)
                from = i;
            if (seen == pos + step)
                to = i;
            seen++;
        }

        if (from < 0 || to < 0)
            return false;

        (rows[from], rows[to]) = (rows[to], rows[from]);
        return true;
    }

    /// <summary>
    /// Índice global para que la fila quede en <paramref name="partidaIndex"/>
    /// (0 = primera de la partida). Un índice mayor o igual al largo la deja al final de la partida.
    /// </summary>
    public static int GlobalIndexForSlot(IList<CubicacionRow> rows, string? itemId, int partidaIndex)
    {
        if (rows == null || rows.Count == 0)
            return 0;

        int seen = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (!Belongs(rows[i], itemId))
                continue;
            if (seen == partidaIndex)
                return i;
            seen++;
        }

        if (seen == 0)
            return rows.Count;

        for (int i = rows.Count - 1; i >= 0; i--)
        {
            if (Belongs(rows[i], itemId))
                return i + 1;
        }

        return rows.Count;
    }

    public static void InsertAtSlot(List<CubicacionRow> rows, string? itemId, CubicacionRow row, int partidaIndex)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(row);
        int at = GlobalIndexForSlot(rows, itemId, partidaIndex);
        if (at < 0 || at > rows.Count)
            rows.Add(row);
        else
            rows.Insert(at, row);
    }

    private static int Count(IList<CubicacionRow> rows, string? itemId)
    {
        int count = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (Belongs(rows[i], itemId))
                count++;
        }

        return count;
    }

    private static bool Belongs(CubicacionRow row, string? itemId)
        => string.Equals(row.ItemId, itemId ?? "", StringComparison.OrdinalIgnoreCase);

    private static bool SameId(CubicacionRow left, CubicacionRow right)
        => !string.IsNullOrWhiteSpace(left.Id)
           && string.Equals(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
}
