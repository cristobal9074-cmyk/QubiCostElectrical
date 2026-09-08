using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

internal sealed partial class CubicacionRowMutationService
{
    public void AddRow()
    {
        var selectedPartida = parent.SelectedPartida;
        if (!parent.CanEdit || selectedPartida == null || !selectedPartida.EsPartida) return;

        undo.PushUndoPoint(session, $"Agregar recurso");

        var row = new CubicacionRow
        {
            Id = Guid.NewGuid().ToString("N"),
            ItemId = selectedPartida.Id,
            Tipo = "MAT",
            Area = "General"
        };
        session.Cubicacion.Add(row);

        var vm = parent.CreateRowVm(row);
        parent.Rows.Add(vm);
        parent.SelectedRow = vm;
        parent.HasPendingUndoForEdit = false;
        parent.MarkDirty();
        parent.NotifyIsEmptyChanged();
        parent.NotifyCubicacionSummaryChanged();
        parent.UpdatePartidaCount(selectedPartida);
        RefreshRowNumbers();
    }

    public void DeleteSelectedRows()
    {
        if (!parent.CanEdit)
            return;
        var toDelete = parent.Rows.Where(r => r.IsSelected).ToList();
        if (toDelete.Count == 0 && parent.SelectedRow != null)
            toDelete.Add(parent.SelectedRow);

        toDelete = toDelete.Where(r => r.IsSessionBacked || r.HasContent).Distinct().ToList();
        if (toDelete.Count == 0)
            return;

        string msg = toDelete.Count == 1
            ? $"¿Eliminar el recurso '{toDelete[0].Descripcion}'?"
            : $"¿Eliminar {toDelete.Count} recursos seleccionados?";

        if (!WpfDialogService.Confirm("Eliminar recursos", msg, isWarning: true))
            return;

        undo.PushUndoPoint(session, $"Eliminar {toDelete.Count} recurso(s)");

        foreach (var vm in toDelete)
        {
            if (vm.IsSessionBacked)
                cubicacionPresenter.DeleteRows([vm.Id]);

            parent.Rows.Remove(vm);
        }

        EnsurePaddingRows();

        if (ReferenceEquals(parent.SelectedRow, null) || toDelete.Contains(parent.SelectedRow))
            parent.SelectedRow = parent.Rows.FirstOrDefault();

        parent.HasPendingUndoForEdit = false;
        parent.MarkDirty();
        parent.ScheduleRefreshTotals();
        parent.NotifyIsEmptyChanged();
        parent.NotifyCubicacionSummaryChanged();
        parent.UpdatePartidaCount(parent.SelectedPartida);
        RefreshRowNumbers();
    }

    public void DuplicateSelectedRow()
    {
        if (!parent.CanEdit)
            return;
        var src = parent.Rows.FirstOrDefault(r => r.IsSelected) ?? parent.SelectedRow;
        var selectedPartida = parent.SelectedPartida;
        if (src == null || selectedPartida == null) return;
        // Una fila placeholder (casilla vacía, nunca confirmada) no está en
        // session.Cubicacion: IndexOf devolvía -1 y el clon terminaba insertado en la
        // posición 0 de TODO el proyecto (no de esta partida) — un recurso fantasma
        // desubicado. Nada que duplicar si la fila origen está vacía.
        if (!src.IsSessionBacked)
            return;

        undo.PushUndoPoint(session, $"Duplicar {src.Codigo}");

        var clone = cubSvc.CloneRowForItem(src.Source, selectedPartida.Id);
        int idx = session.Cubicacion.IndexOf(src.Source);
        session.Cubicacion.Insert(idx + 1, clone);

        var cloneVm = parent.CreateRowVm(clone);
        parent.InitializeSectorRow(cloneVm);
        int vmIdx = parent.Rows.IndexOf(src);
        parent.Rows.Insert(vmIdx + 1, cloneVm);
        parent.SelectedRow = cloneVm;
        parent.HasPendingUndoForEdit = false;
        parent.MarkDirty();
        parent.ScheduleRefreshTotals();
        parent.NotifyCubicacionSummaryChanged();
        parent.UpdatePartidaCount(selectedPartida);
        RefreshRowNumbers();
    }

    public void MoveSelectedRow(int direction)
    {
        if (!parent.CanEdit)
            return;
        var src = parent.SelectedRow;
        var selectedPartida = parent.SelectedPartida;
        if (src == null || selectedPartida == null) return;

        int vmIdx = parent.Rows.IndexOf(src);
        int newVmIdx = vmIdx + direction;
        if (newVmIdx < 0 || newVmIdx >= parent.Rows.Count) return;

        undo.PushUndoPoint(session, $"Mover {src.Codigo}");

        // Intercambiar posiciones solo entre índices globales que pertenecen a esta partida:
        // AddRow siempre agrega al final de session.Cubicacion, por lo que las filas de una
        // misma partida no son necesariamente contiguas en la lista global. Intercambiar con
        // el vecino global directo (en vez del vecino dentro de la partida) podía no mover nada
        // en términos del orden persistido y revertirse silenciosamente al recargar.
        var partidaIndices = new List<int>();
        for (int i = 0; i < session.Cubicacion.Count; i++)
        {
            if (string.Equals(session.Cubicacion[i].ItemId, selectedPartida.Id, StringComparison.OrdinalIgnoreCase))
                partidaIndices.Add(i);
        }
        int posInPartida = partidaIndices.IndexOf(session.Cubicacion.IndexOf(src.Source));
        int newPosInPartida = posInPartida + direction;
        if (posInPartida < 0 || newPosInPartida < 0 || newPosInPartida >= partidaIndices.Count) return;

        int sessionIdx = partidaIndices[posInPartida];
        int swapWithIdx = partidaIndices[newPosInPartida];
        (session.Cubicacion[sessionIdx], session.Cubicacion[swapWithIdx]) =
            (session.Cubicacion[swapWithIdx], session.Cubicacion[sessionIdx]);

        parent.Rows.RemoveAt(vmIdx);
        parent.Rows.Insert(newVmIdx, src);
        // SelectedRow ya apuntaba a src (misma referencia): el setter no vuelve a disparar
        // RightPanel.LoadRow por igualdad de referencia, así que se refresca explícitamente
        // (relevante para el comentario de celda, cuya posición visual pudo cambiar).
        parent.SelectedRow = src;
        parent.RightPanel.LoadRow(src);
        parent.HasPendingUndoForEdit = false;
        parent.MarkDirty();
        RefreshRowNumbers();
    }

    public bool CanMoveRow(int direction)
    {
        if (parent.SelectedRow == null) return false;
        int idx = parent.Rows.IndexOf(parent.SelectedRow);
        return direction == -1 ? idx > 0 : idx < parent.Rows.Count - 1;
    }

    // Corrige el precio de varias filas de una sola vez (p. ej. tras pegar códigos con un
    // precio desactualizado): resincroniza MAT UF/MO UF desde el catálogo del proyecto o el
    // Banco de Precios, usando el código que cada fila YA tiene — nunca cambia el código ni
    // la descripción, así que es seguro aunque las filas seleccionadas sean de materiales
    // distintos.
    public void RefreshPricesForSelectedRows()
    {
        if (!parent.CanEdit) return;

        var targets = parent.Rows.Where(r => r.IsSelected && !string.IsNullOrWhiteSpace(r.Codigo)).ToList();
        if (targets.Count == 0 && parent.SelectedRow != null && !string.IsNullOrWhiteSpace(parent.SelectedRow.Codigo))
            targets.Add(parent.SelectedRow);
        if (targets.Count == 0)
        {
            // El botón se habilita con solo tener una fila seleccionada, aunque esté vacía;
            // sin este aviso, clickearlo en una fila sin código no hacía nada visible.
            ShellNotificationService.Instance.ShowWarning(
                "Actualizar precio", "La fila seleccionada no tiene código.");
            return;
        }

        undo.PushUndoPoint(session, $"Actualizar precio de {targets.Count} recurso(s)");

        int updated = 0;
        foreach (var row in targets)
        {
            var material = session.Materiales.FirstOrDefault(m => CodeHelper.Match(m.Codigo, row.Codigo))
                ?? ResolveFromPriceBank(row.Codigo);
            if (material == null) continue;

            row.SetPricesSilently(material.MatClp, material.MoClp);
            // Ver comentario en CubicacionRowMutationService.ApplyMaterial: sin sincronizar, el
            // próximo CreateSnapshot() revertiría este precio actualizado usando la composición
            // normalizada vieja.
            session.SyncCubicacionRowComposition(row.Source);
            updated++;
        }

        if (updated == 0)
        {
            ShellNotificationService.Instance.ShowWarning(
                "Actualizar precio", "Ninguno de los códigos seleccionados se encontró en el catálogo o el banco de precios.");
            return;
        }

        parent.ScheduleRefreshTotals();
        parent.ValidateRows();
        parent.MarkDirty();
        parent.NotifyCubicacionSummaryChanged();
        ShellNotificationService.Instance.ShowSuccess(
            "Precios actualizados",
            $"{updated} recurso(s) actualizados desde el catálogo.",
            actionLabel: "Deshacer",
            action: () => ShellNotificationService.RequestUndo?.Invoke());
    }

    public void ApplyMaterial(MaterialRow? material)
    {
        if (!parent.CanEdit || material == null || parent.SelectedRow == null) return;
        undo.PushUndoPoint(session, $"Aplicar material {material.Codigo}");
        // Si la fila era un placeholder (isSessionBacked=false, típico al aplicar material
        // en una fila nueva con Enter), antes se confiaba en que un evento posterior de la
        // grilla la agregara a session.Cubicacion. Con Enter esa confirmación no siempre
        // llega de inmediato (se suprime el commit nativo para cerrar el autocompletado
        // sin mover de celda) — asegurarla aquí evita perder el recurso si el usuario
        // cierra el proyecto antes de que la fila se confirme por otra vía.
        EnsureRowInSession(parent.SelectedRow);
        // La sugerencia puede venir del Banco de Precios (no del catálogo del proyecto);
        // copiarla al proyecto para que quede disponible de aquí en adelante.
        EnsureMaterialInProjectCatalog(material);
        cubSvc.ApplyMaterial(parent.SelectedRow.Source, material, parent.SelectedRow.ValorUfClp);
        parent.SelectedRow.Codigo = material.Codigo;
        parent.SelectedRow.Descripcion = material.Descripcion;
        parent.SelectedRow.Unidad = material.Unidad;
        parent.SelectedRow.RefreshDisplayProperties();
        parent.SelectedRow.Tipo = string.Equals(material.Categoria, "Equipo", StringComparison.OrdinalIgnoreCase) ? "EQ" : "MAT";
        parent.AutocompleteVisible = false;
        parent.RightPanel.LoadRow(parent.SelectedRow);
        parent.ScheduleRefreshTotals();
        parent.ValidateRow(parent.SelectedRow);
        parent.MarkDirty();
        parent.NotifyCubicacionSummaryChanged();
        // Ver comentario en CubicacionPersistenceService.OnRowEdited: sin esto, aplicar un
        // material por autocompletado solo vive en la fila legacy — el próximo CreateSnapshot()
        // (Undo o Guardar) la revierte usando la composición normalizada vieja. Bug reportado
        // por el usuario: agregar un código (vía autocompletado), cambiar de ítem, y al volver
        // el código había desaparecido.
        session.SyncCubicacionRowComposition(parent.SelectedRow.Source);
    }

    public void EnsureRowInSession(CubicacionRowVm row)
    {
        if (row.IsSessionBacked)
            return;

        // .Contains() escaneaba TODA session.Cubicacion (todas las partidas del proyecto, no
        // solo la activa) en cada primera edición de una fila nueva — el momento exacto de
        // "escribir el primer código" que se sentía lento. Es seguro saltarlo: todo otro lugar
        // que agrega a session.Cubicacion construye el CubicacionRowVm con IsSessionBacked=true
        // en el mismo paso, así que si llegamos acá con IsSessionBacked=false, row.Source
        // nunca pudo haber sido agregado antes.
        //
        // Insertar en la posición correcta (no siempre al final): el orden de session.Cubicacion
        // debe coincidir con el orden VISUAL que ve el usuario en la grilla (columna "#"), del
        // que dependen las referencias entre casillas por número de fila (F{fila}C{columna} —
        // ver EvaluateSafeWithRowReferences). Si una fila visual de más abajo se editaba primero
        // que una de más arriba, antes quedaba "antes" en la lista persistida aunque se viera
        // después en pantalla, y una referencia F2 terminaba apuntando a la fila equivocada.
        int visualIndex = parent.Rows.IndexOf(row);
        int insertAt = session.Cubicacion.Count;
        if (visualIndex >= 0)
        {
            for (int i = visualIndex + 1; i < parent.Rows.Count; i++)
            {
                if (!parent.Rows[i].IsSessionBacked) continue;
                int idx = session.Cubicacion.IndexOf(parent.Rows[i].Source);
                if (idx >= 0) { insertAt = idx; break; }
            }
        }

        session.Cubicacion.Insert(insertAt, row.Source);
        row.IsSessionBacked = true;
    }

    // El Banco de Precios es un catálogo compartido entre proyectos; cada proyecto
    // mantiene su propia copia en session.Materiales. Un código que existe en el banco
    // pero nunca se copió a este proyecto se marca "no existe" en la validación aunque
    // el usuario lo vea a diario en la pantalla Precios. Para que escribir/pegar un
    // código conocido del banco funcione sin pasos extra, lo copiamos al catálogo del
    // proyecto en el momento en que se usa por primera vez en esta partida.
    public void TryAutoResolveFromPriceBank(CubicacionRowVm row)
    {
        if (string.IsNullOrWhiteSpace(row.Codigo))
            return;

        // Antes, si el material YA estaba en el catálogo del proyecto (p. ej. porque otra
        // fila ya lo trajo del banco antes), el método salía de inmediato sin completar
        // Descripción/UD/precio de ESTA fila — solo funcionaba la primera vez que se
        // escribía el código en toda la partida. Ahora se busca primero en el proyecto y
        // se cae al banco solo si no está, pero SIEMPRE se completa la fila si hay match.
        parent.GetValidationMaterialesIndex().TryGetValue(row.Codigo, out var material);
        var bankMaterial = ResolveFromPriceBank(row.Codigo);
        if (bankMaterial != null)
        {
            if (material == null)
            {
                material = bankMaterial;
                EnsureMaterialInProjectCatalog(material);
            }
            else
            {
                // Si los precios o la mano de obra del banco difieren de los del proyecto,
                // actualizamos el material en el catálogo local del proyecto Y la fila.
                if (material.MatClp != bankMaterial.MatClp || material.MoClp != bankMaterial.MoClp)
                {
                    material.MatClp = bankMaterial.MatClp;
                    material.MoClp = bankMaterial.MoClp;
                    material.FechaPrecio = bankMaterial.FechaPrecio;
                    material.Proveedor = bankMaterial.Proveedor;
                    material.MonedaPrecio = bankMaterial.MonedaPrecio;
                    material.RecalcularUf(session.ProjectInfo.ValorUfClp);
                    session.MarkDirty(ProjectDirtyScope.Materiales);
                    row.SetPricesSilently(material.MatClp, material.MoClp);
                    return;
                }
            }
        }

        if (material == null)
            return;

        // Solo completar campos que el usuario dejó vacíos/en cero: no pisar datos
        // que ya haya escrito a mano en la fila.
        if (string.IsNullOrWhiteSpace(row.Descripcion))
            row.Descripcion = material.Descripcion;
        if (string.IsNullOrWhiteSpace(row.Unidad))
            row.Unidad = material.Unidad;
        decimal targetMat = row.MatClp == 0m ? material.MatClp : row.MatClp;
        decimal targetMo = row.MoClp == 0m ? material.MoClp : row.MoClp;
        row.SetPricesSilently(targetMat, targetMo);
    }

    private MaterialRow? ResolveFromPriceBank(string codigo)
    {
        var entry = priceBankStore.GetByCodigo(codigo);
        return entry == null ? null : CubicacionViewModel.FromBankEntry(entry);
    }

    private void EnsureMaterialInProjectCatalog(MaterialRow material)
    {
        // Antes escaneaba TODO session.Materiales (.Any) cada vez que se aplicaba un
        // código — con un catálogo grande, esto era justo lo que hacía sentir "pegado" el
        // momento de confirmar un código. Reusa el mismo índice cacheado que ya usa la
        // validación (O(1) en vez de O(materiales)).
        if (parent.GetValidationMaterialesIndex().ContainsKey(material.Codigo))
            return;

        session.Materiales.Add(material);
        session.MarkDirty(ProjectDirtyScope.Materiales);
        parent.InvalidateAutocompleteMaterialIndex();
        parent.ValidationMaterialesIndex = null;
    }

    public void InsertRow()
    {
        var selectedPartida = parent.SelectedPartida;
        if (!parent.CanEdit || selectedPartida == null || !selectedPartida.EsPartida) return;

        var src = parent.SelectedRow;
        if (src == null)
        {
            AddRow();
            return;
        }

        undo.PushUndoPoint(session, $"Insertar recurso");

        var row = new CubicacionRow
        {
            Id = Guid.NewGuid().ToString("N"),
            ItemId = selectedPartida.Id,
            Tipo = "MAT",
            Area = "General"
        };

        // Encontrar índice en la lista de cubicación del proyecto
        int sessionIdx = -1;
        if (src.IsSessionBacked)
        {
            sessionIdx = session.Cubicacion.IndexOf(src.Source);
        }
        else
        {
            // Fila no persistida, insertar después de la última fila activa de esta partida
            var lastRow = session.Cubicacion.LastOrDefault(x => string.Equals(x.ItemId, selectedPartida.Id, StringComparison.OrdinalIgnoreCase));
            if (lastRow != null)
                sessionIdx = session.Cubicacion.IndexOf(lastRow) + 1;
        }

        if (sessionIdx >= 0 && sessionIdx <= session.Cubicacion.Count)
        {
            session.Cubicacion.Insert(sessionIdx, row);
        }
        else
        {
            session.Cubicacion.Add(row);
        }

        var vm = parent.CreateRowVm(row, isSessionBacked: true);
        parent.InitializeSectorRow(vm);

        int vmIdx = parent.Rows.IndexOf(src);
        if (vmIdx >= 0)
        {
            parent.Rows.Insert(vmIdx, vm);
        }
        else
        {
            parent.Rows.Add(vm);
        }

        parent.SelectedRow = vm;
        parent.HasPendingUndoForEdit = false;
        parent.MarkDirty();
        parent.NotifyIsEmptyChanged();
        parent.NotifyCubicacionSummaryChanged();
        parent.UpdatePartidaCount(selectedPartida);
        RefreshRowNumbers();
    }

    private void EnsurePaddingRows()
    {
        var selectedPartida = parent.SelectedPartida;
        if (selectedPartida == null || !selectedPartida.EsPartida)
            return;

        while (parent.Rows.Count < CubicacionViewModel.MinVisibleRows)
        {
            var row = new CubicacionRow
            {
                Id = Guid.NewGuid().ToString("N"),
                ItemId = selectedPartida.Id,
                Tipo = "MAT",
                Area = "General"
            };

            var vm = parent.CreateRowVm(row, isSessionBacked: false);
            parent.InitializeSectorRow(vm);
            parent.Rows.Add(vm);
        }

        RefreshRowNumbers();
    }

    /// <summary>
    /// Asigna el correlativo 1, 2, 3... a TODAS las filas visibles (reales y placeholder)
    /// para que la columna # nunca quede en blanco, independientemente de eliminaciones.
    /// </summary>
    public void RefreshRowNumbers()
    {
        int counter = 1;
        foreach (var row in parent.Rows)
            row.RowNumber = counter++;
    }
}
