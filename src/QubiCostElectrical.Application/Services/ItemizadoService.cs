using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed record ItemizadoTreeRow(ItemizadoRow Source, string DisplayDescription, int Nivel);
public sealed record ItemizadoEditableCell(
    string Id,
    string Item,
    string Descripcion,
    string Unidad,
    decimal Cantidad);

public sealed class ItemizadoService : IItemizadoService
{
    public IReadOnlyList<ItemizadoTreeRow> FilterForCubicacion(IEnumerable<ItemizadoRow> itemizado, string filter, string itemFilter)
    {
        string f = (filter ?? "").Trim().ToUpperInvariant();
        string itemF = (itemFilter ?? "").Trim();

        return (itemizado ?? Enumerable.Empty<ItemizadoRow>())
            .Where(x =>
                (string.IsNullOrWhiteSpace(f) ||
                    (x.Item ?? "").ToUpperInvariant().Contains(f) ||
                    (x.Descripcion ?? "").ToUpperInvariant().Contains(f) ||
                    (x.Unidad ?? "").ToUpperInvariant().Contains(f)) &&
                (string.IsNullOrWhiteSpace(itemF) ||
                    x.Item == itemF ||
                    (x.Item ?? "").StartsWith(itemF + ".", StringComparison.Ordinal)))
            .Select(x => new ItemizadoTreeRow(x, BuildTreeDescription(x), Math.Max(0, (x.Item ?? "").Split('.').Length - 1)))
            .ToList();
    }

    public int CountPartidas(IEnumerable<ItemizadoRow> itemizado) => itemizado.Count(x => !x.EsTitulo);

    public int CountTitulos(IEnumerable<ItemizadoRow> itemizado) => itemizado.Count(x => x.EsTitulo);

    public string FirstEditableItemId(IEnumerable<ItemizadoRow> itemizado) => itemizado.FirstOrDefault(x => !x.EsTitulo)?.Id ?? "";

    public ItemizadoMutationResult AddBelow(List<ItemizadoRow> itemizado, int selectedIndex, string desc, string unidad)
    {
        if (itemizado.Count == 0)
        {
            var title = ItemizadoHierarchyRules.CreateDefaultTitleRow(1);
            itemizado.Add(title);
            var partida = ItemizadoHierarchyRules.CreatePartidaRow(itemizado, title.Id, desc, unidad);
            itemizado.Add(partida);
            return new ItemizadoMutationResult(1, Array.Empty<string>());
        }

        int row = selectedIndex < 0 ? itemizado.Count - 1 : Math.Min(selectedIndex, itemizado.Count - 1);
        var selected = itemizado[Math.Max(0, row)];

        string parent = "";
        if (selected.EsTitulo)
        {
            parent = ItemizadoHierarchyRules.NormalizeTitleId(selected.Id);
        }
        else if (!string.IsNullOrWhiteSpace(selected.ParentId))
        {
            parent = selected.ParentId;
        }
        else
        {
            for (int i = row; i >= 0; i--)
            {
                if (itemizado[i].EsTitulo)
                {
                    parent = itemizado[i].Id;
                    break;
                }
            }
            if (string.IsNullOrWhiteSpace(parent))
                parent = ParentOf(selected.Id, itemizado);
        }

        int pos = Math.Min(row + 1, itemizado.Count);

        var nueva = ItemizadoHierarchyRules.CreatePartidaRow(itemizado, parent, desc, unidad);
        itemizado.Insert(pos, nueva);
        return new ItemizadoMutationResult(pos, Array.Empty<string>());
    }

    public ItemizadoMutationResult AddTitle(List<ItemizadoRow> itemizado, int selectedIndex = -1)
    {
        int nextTitle = 1;
        foreach (var item in itemizado.Where(x => x.EsTitulo))
        {
            string raw = (item.Item ?? "").Replace(",", ".").Trim();
            if (decimal.TryParse(raw, out var dec))
            {
                int n = (int)Math.Truncate(dec);
                if (n >= nextTitle) nextTitle = n + 1;
            }
        }

        var row = new ItemizadoRow
        {
            Id = $"{nextTitle}.0",
            Item = $"{nextTitle},0",
            WBSCode = $"{nextTitle}.0",
            Descripcion = "NUEVO TÍTULO",
            Unidad = Unidades.GL,
            Cantidad = 0,
            EsTitulo = true,
            NivelJerarquico = 1,
            ParentId = ""
        };

        // Sin fila de referencia: se agrega al final (comportamiento previo).
        if (selectedIndex < 0 || selectedIndex >= itemizado.Count)
        {
            itemizado.Add(row);
            return new ItemizadoMutationResult(itemizado.Count - 1, Array.Empty<string>());
        }

        // Con fila de referencia: se inserta justo debajo de donde el usuario hizo clic.
        int pos = Math.Min(selectedIndex + 1, itemizado.Count);
        itemizado.Insert(pos, row);
        return new ItemizadoMutationResult(pos, Array.Empty<string>());
    }

    public ItemizadoMutationResult Move(List<ItemizadoRow> itemizado, int selectedIndex, int direction)
        => ItemizadoHierarchyMove.Move(itemizado, selectedIndex, direction);

    public ItemizadoMutationResult DuplicateAt(List<ItemizadoRow> itemizado, int selectedIndex)
    {
        if (itemizado == null || selectedIndex < 0 || selectedIndex >= itemizado.Count)
            return new ItemizadoMutationResult(-1, Array.Empty<string>());

        var source = itemizado[selectedIndex];
        if (source.EsTitulo)
            return new ItemizadoMutationResult(selectedIndex, Array.Empty<string>());

        string parent = ParentOf(source.Id, itemizado);
        int pos = Math.Min(selectedIndex + 1, itemizado.Count);
        string uniqueItem = ItemizadoHierarchyRules.NextUniqueDisplayItemCode(itemizado, source.Item ?? "");
        itemizado.Insert(pos, new ItemizadoRow
        {
            Id = NextChildId(itemizado, parent),
            Item = uniqueItem,
            WBSCode = ItemizadoHierarchyRules.NormalizeIdFromItem(uniqueItem),
            ParentId = parent,
            NivelJerarquico = 2,
            Descripcion = string.IsNullOrWhiteSpace(source.Descripcion) ? "Copia" : source.Descripcion.Trim() + " (copia)",
            Unidad = string.IsNullOrWhiteSpace(source.Unidad) ? Unidades.GL : source.Unidad,
            Cantidad = source.Cantidad,
            EsTitulo = false
        });
        return new ItemizadoMutationResult(pos, Array.Empty<string>());
    }

    public ItemizadoMutationResult DeleteAt(List<ItemizadoRow> itemizado, List<CubicacionRow> cubicacion, Dictionary<string, string[]> qtyHeadersByItem, int selectedIndex)
    {
        if (selectedIndex < 0 || selectedIndex >= itemizado.Count)
            return new ItemizadoMutationResult(-1, Array.Empty<string>());

        var x = itemizado[selectedIndex];
        var removedIds = new List<string>();

        if (x.EsTitulo)
        {
            string parent = ItemizadoHierarchyRules.NormalizeTitleId(x.Id);
            string chapter = parent.Split('.')[0];
            removedIds.AddRange(itemizado.Where(i =>
                    string.Equals(i.Id, parent, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(i.Id, chapter, StringComparison.OrdinalIgnoreCase)
                    || ItemizadoHierarchyRules.MatchesRemovedId(i.Id, parent)
                    || (!i.EsTitulo && (i.Id ?? "").StartsWith(chapter + ".", StringComparison.Ordinal)))
                .Select(i => i.Id));
            itemizado.RemoveAll(i =>
                removedIds.Contains(i.Id, StringComparer.OrdinalIgnoreCase));
        }
        else
        {
            removedIds.Add(x.Id);
            itemizado.RemoveAt(selectedIndex);
        }

        foreach (string id in removedIds)
        {
            cubicacion.RemoveAll(i => ItemizadoHierarchyRules.MatchesRemovedId(i.ItemId, id));
            var keysToRemove = qtyHeadersByItem.Keys
                .Where(k => ItemizadoHierarchyRules.MatchesRemovedId(k, id))
                .ToList();
            foreach (string key in keysToRemove)
                qtyHeadersByItem.Remove(key);
        }

        int newIndex = itemizado.Count == 0 ? -1 : Math.Min(selectedIndex, itemizado.Count - 1);
        return new ItemizadoMutationResult(newIndex, removedIds);
    }

    public void Clear(List<ItemizadoRow> itemizado, List<CubicacionRow> cubicacion, Dictionary<string, string[]> qtyHeadersByItem)
    {
        itemizado.Clear();
        cubicacion.Clear();
        qtyHeadersByItem.Clear();
    }

    public void ReplaceAll(
        List<ItemizadoRow> itemizado,
        List<CubicacionRow> cubicacion,
        Dictionary<string, string[]> qtyHeadersByItem,
        IEnumerable<ItemizadoRow> newRows)
    {
        var existingMap = itemizado
            .Where(i => !string.IsNullOrWhiteSpace(i.Item))
            .ToLookup(i => i.Item.Trim(), StringComparer.OrdinalIgnoreCase);

        Clear(itemizado, cubicacion, qtyHeadersByItem);

        foreach (var row in newRows)
        {
            if (!string.IsNullOrWhiteSpace(row.Item) && existingMap.Contains(row.Item.Trim()))
            {
                var existing = existingMap[row.Item.Trim()].First();
                row.PartidaId = existing.PartidaId;
                row.Id = existing.Id;
            }
            itemizado.Add(row);
        }
    }

    public void SyncEditableRows(List<ItemizadoRow> itemizado, IReadOnlyList<ItemizadoEditableCell> cells)
    {
        if (itemizado == null || cells == null || cells.Count == 0)
            return;

        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            if (string.IsNullOrWhiteSpace(cell.Descripcion) && string.IsNullOrWhiteSpace(cell.Item))
                continue;

            if (i >= itemizado.Count)
            {
                bool esTitulo = !string.IsNullOrWhiteSpace(cell.Item) && !cell.Item.Contains('.');
                string safeId = string.IsNullOrWhiteSpace(cell.Id)
                    ? (esTitulo ? cell.Item.Replace(",", ".") : $"0.0.{i + 1:00000}")
                    : cell.Id;

                itemizado.Add(new ItemizadoRow { Id = safeId, EsTitulo = esTitulo });
            }

            itemizado[i].Id = string.IsNullOrWhiteSpace(cell.Id) ? itemizado[i].Id : cell.Id;
            itemizado[i].Item = (cell.Item ?? "").Trim();
            itemizado[i].Descripcion = (cell.Descripcion ?? "").Trim();
            itemizado[i].Unidad = string.IsNullOrWhiteSpace(cell.Unidad) ? "GL" : cell.Unidad.Trim();
            itemizado[i].Cantidad = cell.Cantidad;
        }
    }

    public void ApplyAssistedPartidaDetails(List<ItemizadoRow> itemizado, int rowIndex, string code, string description, string unit)
    {
        if (itemizado == null || rowIndex < 0 || rowIndex >= itemizado.Count)
            return;

        itemizado[rowIndex].Item = code;
        itemizado[rowIndex].Unidad = unit;
        itemizado[rowIndex].Descripcion = description;
    }

    public string ParentOf(string id, IReadOnlyList<ItemizadoRow>? rows = null)
        => ItemizadoHierarchyRules.ParentIdOf(id, rows);

    public string NextChildId(IEnumerable<ItemizadoRow> itemizado, string parent)
        => ItemizadoHierarchyRules.NextChildId(itemizado, parent);

    private static string BuildTreeDescription(ItemizadoRow x)
    {
        int nivel = Math.Max(0, (x.Item ?? "").Split('.').Length - 1);
        string prefix;
        if (x.EsTitulo)
            prefix = nivel <= 1 ? "▾  ▭  " : "▸  ▭  ";
        else
            prefix = nivel >= 2 ? "     ◦  " : "  ◦  ";

        return prefix + (x.Descripcion ?? "");
    }
}
