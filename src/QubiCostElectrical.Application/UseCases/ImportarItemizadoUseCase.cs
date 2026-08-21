using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

/// <summary>
/// Resultado de importar un itemizado desde una tabla Excel ya parseada.
/// Error == "" significa éxito.
/// </summary>
public sealed record ImportarItemizadoResult(
    IReadOnlyList<ItemizadoRow> Rows,
    string Error);

/// <summary>
/// Caso de uso: convierte una tabla (List of rows de strings) en una lista de ItemizadoRow.
/// Recibe la tabla ya leída por la capa de infraestructura (ReadFirstXlsxSheet).
/// Requiere encabezados reconocibles; devuelve un error descriptivo si no los encuentra.
/// Sin dependencias de UI ni de archivos — se puede testear directamente.
/// </summary>
public sealed class ImportarItemizadoUseCase
{
    public ImportarItemizadoResult Execute(List<List<string>> table)
    {
        if (table == null || table.Count == 0)
            return Fail("El Excel no tiene datos.");

        var (headerRow, extraHeaderRow) = ItemizadoImportParser.FindHeaderRowRange(table, "itemizado");
        if (headerRow < 0)
            return Fail("No encontré encabezados válidos. El Excel debe tener columnas: ITEM, DESCRIPCIÓN, UNIDAD, CANTIDAD.");

        var headers = table[headerRow];
        // Cuando hay 2 filas de encabezado, las columnas UNIDAD/CANT pueden estar en la fila extra
        var extraHeaders = extraHeaderRow >= 0 ? table[extraHeaderRow] : null;
        int cItem   = ItemizadoImportParser.FindColumn(headers, extraHeaders, "ITEM", "ÍTEM", "NITEM", "NUMEROITEM", "PARTIDA");
        int cDesc   = ItemizadoImportParser.FindColumn(headers, extraHeaders, "DESCRIPCION", "DESCRIPCIÓN", "DETALLE", "NOMBRE", "PARTIDADESCRIPCION");
        int cUnidad = ItemizadoImportParser.FindColumn(headers, extraHeaders, "UNIDAD", "UD", "UN", "UND", "UNID", "UNIDADES");
        int cCant   = ItemizadoImportParser.FindColumn(headers, extraHeaders, "CANTIDAD", "CANT", "QTY", "CANTIDADITEM", "CANTIDADES", "NUEVASCANTIDADES");

        if (cDesc < 0)
            return Fail("No encontré columna DESCRIPCIÓN en el Excel.");

        // dataStart es la fila siguiente al último encabezado (puede ser 1 o 2 filas)
        int lastHeaderRow = extraHeaderRow >= 0 ? extraHeaderRow : headerRow;
        int dataStartRow = ItemizadoImportParser.FindDataStartRow(table, lastHeaderRow, cItem, cDesc);
        (cUnidad, cCant) = ItemizadoImportParser.InferOptionalColumns(table, dataStartRow, cItem, cDesc, cUnidad, cCant);

        var rows = BuildRows(table, dataStartRow, cItem, cDesc, cUnidad, cCant);
        if (rows.Count == 0)
            return Fail("El Excel tiene encabezados pero no contiene filas de datos.");

        string? limitError = ItemizadoImportValidation.ValidateRowCount(rows.Count);
        if (limitError != null)
            return Fail(limitError);

        return new ImportarItemizadoResult(rows, "");
    }

    public ImportarItemizadoResult Execute(
        List<List<string>> table,
        int headerRow,
        int cItem,
        int cDesc,
        int cUnidad,
        int cCant,
        int dataStartRow)
    {
        if (table == null || table.Count == 0)
            return Fail("El Excel no tiene datos.");

        if (cDesc < 0)
            return Fail("La columna de Descripción es requerida para la importación.");

        var rows = BuildRows(table, dataStartRow, cItem, cDesc, cUnidad, cCant);
        if (rows.Count == 0)
            return Fail("El Excel no contiene filas de datos válidas con el mapeo seleccionado.");

        string? limitError = ItemizadoImportValidation.ValidateRowCount(rows.Count);
        if (limitError != null)
            return Fail(limitError);

        return new ImportarItemizadoResult(rows, "");
    }

    // ─── Construcción de filas ────────────────────────────────────────────

    private static List<ItemizadoRow> BuildRows(
        List<List<string>> table, int dataStart,
        int cItem, int cDesc, int cUnidad, int cCant)
    {
        var rows = new List<ItemizadoRow>();
        string currentParentId = "";
        int childSeq = 1;
        int fallbackTitle = 1;

        for (int i = dataStart; i < table.Count; i++)
        {
            var row = table[i];
            string itemCode = ItemizadoImportParser.GetCell(row, cItem).Trim();
            string desc     = ItemizadoImportParser.GetCell(row, cDesc).Trim();
            string unidad   = ItemizadoImportParser.GetCell(row, cUnidad).Trim();
            string cantText = ItemizadoImportParser.GetCell(row, cCant).Trim();

            if (string.IsNullOrWhiteSpace(itemCode) && string.IsNullOrWhiteSpace(desc))
                continue;

            // Ignorar filas que repiten el encabezado (desc == uno de los alias de descripción)
            string descKey = ItemizadoImportParser.HeaderKey(desc);
            if (descKey is "DESCRIPCION" or "DETALLE" or "NOMBRE" or "PARTIDADESCRIPCION")
                continue;

            if (string.IsNullOrWhiteSpace(desc))
                desc = itemCode;

            // No inventar unidad: si no viene, se deja vacía para que el usuario la complete
            // (antes se forzaba "GL" para cualquier unidad vacía, lo cual modifica los datos del Excel)

            bool esTitulo = ItemizadoImportParser.IsImportedTitleCode(itemCode);

            // Cantidad vacía = 0 (no 1); se respeta el valor exacto del Excel
            decimal cant = string.IsNullOrWhiteSpace(cantText)
                ? 0m
                : ItemizadoImportParser.ParseDecimal(cantText);
            if (!esTitulo)
                cant = ItemizadoHierarchyRules.NormalizePartidaQuantity(cant);

            ItemizadoImportRowBuilder.AppendRow(
                rows,
                itemCode,
                desc,
                unidad,
                cant,
                esTitulo,
                ref currentParentId,
                ref childSeq,
                ref fallbackTitle);
        }

        return rows;
    }

    private static ImportarItemizadoResult Fail(string message) =>
        new(Array.Empty<ItemizadoRow>(), message);
}
