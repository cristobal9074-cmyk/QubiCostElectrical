using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

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
        CubicacionPartidaOrder.InsertAtSlot(session.Cubicacion, selectedPartida.Id, row, int.MaxValue);

        var vm = parent.CreateRowVm(row);
        parent.InitializeSectorRow(vm);
        parent.Rows.Add(vm);
        AlignVisibleRowsToPartidaOrder();
        parent.SelectedRow = vm;
        parent.HasPendingUndoForEdit = false;
        parent.MarkDirty();
        parent.NotifyIsEmptyChanged();
        parent.NotifyCubicacionSummaryChanged();
        parent.UpdatePartidaCount(selectedPartida);
        RefreshRowNumbers();
        parent.NotifyRowAdded(row.Id);
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

        var clearedSlaves = CubicacionSlaveQuantityRules.ClearWithoutMaster(
            session.Cubicacion,
            parent.SelectedPartida?.Id,
            toDelete.Where(vm => vm.IsSessionBacked).Select(vm => vm.Source).ToList());

        foreach (var vm in toDelete)
        {
            if (vm.IsSessionBacked)
                cubicacionPresenter.DeleteRows([vm.Id]);

            parent.Rows.Remove(vm);
        }

        foreach (var source in clearedSlaves)
        {
            var slaveVm = parent.Rows.FirstOrDefault(r => ReferenceEquals(r.Source, source) || string.Equals(r.Id, source.Id, StringComparison.OrdinalIgnoreCase));
            if (slaveVm == null)
                continue;
            slaveVm.Cantidad = 0m;
            slaveVm.LoadSectorWindow(0);
            slaveVm.RefreshDisplayProperties();
            session.SyncCubicacionRowComposition(slaveVm.Source);
            parent.ValidateRow(slaveVm);
        }

        EnsurePaddingRows();

        if (ReferenceEquals(parent.SelectedRow, null) || toDelete.Contains(parent.SelectedRow))
            parent.SelectedRow = parent.Rows.FirstOrDefault();

        parent.HasPendingUndoForEdit = false;
        AlignVisibleRowsToPartidaOrder();
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
        if (parent.Rows.Count(r => r.IsSelected) > 1)
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

        int pos = CubicacionPartidaOrder.Position(session.Cubicacion, selectedPartida.Id, src.Source);
        if (pos < 0)
            return;

        undo.PushUndoPoint(session, $"Duplicar {src.Codigo}");

        var clone = cubSvc.CloneRowForItem(src.Source, selectedPartida.Id);
        CubicacionPartidaOrder.InsertAtSlot(session.Cubicacion, selectedPartida.Id, clone, pos + 1);

        var cloneVm = parent.CreateRowVm(clone);
        parent.InitializeSectorRow(cloneVm);
        parent.Rows.Add(cloneVm);
        AlignVisibleRowsToPartidaOrder();
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

        if (!src.IsSessionBacked || !CubicacionPartidaOrder.CanMove(session.Cubicacion, selectedPartida.Id, src.Source, direction))
            return;

        undo.PushUndoPoint(session, direction < 0 ? $"Subir {src.Codigo}" : $"Bajar {src.Codigo}");
        CubicacionPartidaOrder.TryMove(session.Cubicacion, selectedPartida.Id, src.Source, direction);

        AlignVisibleRowsToPartidaOrder();
        // SelectedRow ya apuntaba a src (misma referencia): el setter no vuelve a disparar
        // RightPanel.LoadRow por igualdad de referencia, así que se refresca explícitamente.
        parent.SelectedRow = src;
        parent.RightPanel.LoadRow(src);
        parent.HasPendingUndoForEdit = false;
        parent.MarkDirty();
        parent.SaveStatusText = direction < 0
            ? "Fila subida dentro de la partida."
            : "Fila bajada dentro de la partida.";
        RefreshRowNumbers();
    }

    public bool CanMoveRow(int direction)
    {
        var src = parent.SelectedRow;
        var selectedPartida = parent.SelectedPartida;
        if (src == null || !src.IsSessionBacked || selectedPartida == null)
            return false;
        return CubicacionPartidaOrder.CanMove(session.Cubicacion, selectedPartida.Id, src.Source, direction);
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

    /// <summary>
    /// Baja a la cubicación el material recién creado en la lista de precios: el alta se usa justamente
    /// cuando falta un material en la partida que se está levantando. Sin partida editable seleccionada,
    /// el material ya quedó en el catálogo del proyecto y solo se avisa por qué no se pudo asignar.
    /// </summary>
    public void AplicarMaterialCreado(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return;

        MaterialRow? material = session.Materiales.FirstOrDefault(m => CodeHelper.Match(m.Codigo, codigo));
        if (material == null) return;

        // El catálogo creció fuera de la cubicación (la lista de precios escribe en session.Materiales
        // directamente): los índices cacheados de validación y autocompletado quedaron viejos. Sin
        // invalidarlos el código nuevo no se ofrece al escribir, y EnsureMaterialInProjectCatalog lo
        // agregaría por segunda vez al no encontrarlo en el índice.
        parent.InvalidateAutocompleteMaterialIndex();
        parent.ValidationMaterialesIndex = null;

        if (!parent.CanEdit || parent.SelectedPartida?.EsPartida != true || parent.SelectedRow == null)
        {
            ShellNotificationService.Instance.ShowWarning(
                "Material creado en el proyecto",
                $"'{codigo}' quedó en la lista de precios. Selecciona una partida de la cubicación para asignarlo.");
            return;
        }

        var targetRow = parent.SelectedRow;
        ApplyMaterial(material);

        string partida = parent.SelectedPartida?.Codigo ?? "la partida";
        ShellNotificationService.Instance.ShowSuccess(
            "Material en la cubicación",
            $"'{material.Codigo}' quedó asignado a {partida}.",
            actionLabel: "Deshacer",
            action: () => ShellNotificationService.RequestUndo?.Invoke());

        parent.NotifyFocusCubicacionZone(targetRow);
    }

    /// <summary>
    /// Escribe la cantidad en la casilla de la zona indicada. La columna CANT es calculada (suma de
    /// las casillas), así que escribir la casilla es lo que deja la fila con cantidad — y dispara el
    /// mismo camino que teclear en la grilla: recálculo, validación, propagación a filas esclavo,
    /// totales y undo (el punto de undo ya lo dejó ApplyMaterial, así que "Deshacer" revierte el
    /// material y su casilla juntos).
    /// </summary>
    public void AplicarCasillaDeCubicacion(string? codigo, int offset, string cantidad)
    {
        var row = parent.SelectedRow;
        if (!parent.CanEdit || row == null || string.IsNullOrWhiteSpace(cantidad))
            return;
        if (!string.IsNullOrWhiteSpace(codigo) && !CodeHelper.Match(row.Codigo, codigo))
            return;

        // ApplyMaterial ya dejó el punto de undo de la acción; marcarlo como pendiente evita que la
        // casilla empuje OTRO punto y que "Deshacer" revierta solo la cantidad dejando el material.
        parent.HasPendingUndoForEdit = true;
        parent.SetSectorExpressionDirect(row, offset, cantidad);
        row.RefreshDisplayProperties();
        parent.RightPanel.LoadRow(row);
    }

    public void ApplyMaterial(MaterialRow? material)
    {
        if (!parent.CanEdit || material == null || parent.SelectedRow == null) return;
        undo.PushUndoPoint(session, $"Asignar material {material.Codigo}");
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
        cubSvc.ApplyMaterial(parent.SelectedRow.Source, material, parent.SelectedRow.ValorUfClp, session.ProjectInfo);
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
        // La posición es el número de filas ya persistidas que se ven antes que esta,
        // no el índice del vecino en la lista global del proyecto.
        int visualIndex = parent.Rows.IndexOf(row);
        int slot = 0;
        if (visualIndex > 0)
        {
            for (int i = 0; i < visualIndex; i++)
            {
                if (parent.Rows[i].IsSessionBacked)
                    slot++;
            }
        }

        CubicacionPartidaOrder.InsertAtSlot(session.Cubicacion, row.Source.ItemId, row.Source, slot);
        row.IsSessionBacked = true;

        if (!string.IsNullOrWhiteSpace(row.Source.ItemId))
        {
            var itemizadoRow = session.Itemizado.FirstOrDefault(x =>
                string.Equals(x.Id, row.Source.ItemId, StringComparison.OrdinalIgnoreCase));
            if (itemizadoRow != null && itemizadoRow.EsTitulo)
            {
                itemizadoRow.EsTitulo = false;
                if (itemizadoRow.Cantidad <= 0m) itemizadoRow.Cantidad = 1m;
                if (string.IsNullOrWhiteSpace(itemizadoRow.Unidad)) itemizadoRow.Unidad = Unidades.GL;
                session.MarkDirty(ProjectDirtyScope.Itemizado);
            }
        }
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
                if (material.MatClp != bankMaterial.MatClp || material.MoClp != bankMaterial.MoClp
                    || !string.Equals(material.MoFormulaTipo, bankMaterial.MoFormulaTipo, StringComparison.OrdinalIgnoreCase)
                    || material.MoPorcentajeMaterial != bankMaterial.MoPorcentajeMaterial)
                {
                    material.MatClp = bankMaterial.MatClp;
                    material.MoClp = bankMaterial.MoClp;
                    material.MoFactorBase = bankMaterial.MoFactorBase;
                    material.MoFormulaTipo = bankMaterial.MoFormulaTipo;
                    material.MoPorcentajeMaterial = bankMaterial.MoPorcentajeMaterial;
                    material.FechaPrecio = bankMaterial.FechaPrecio;
                    material.Proveedor = bankMaterial.Proveedor;
                    material.MonedaPrecio = bankMaterial.MonedaPrecio;
                    material.RecalcularUf(session.ProjectInfo.ValorUfClp);
                    session.MarkDirty(ProjectDirtyScope.Materiales);
                    // M.O. efectiva (rendimiento/porcentaje/fija) — nunca el M.O. crudo del banco.
                    row.SetPricesSilently(material.MatClp, MaterialMoResolver.MoClpEfectiva(material, session.ProjectInfo));
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
        // Efectiva = misma precedencia que el motor (rendimiento > porcentaje > directa); si el
        // material es directo devuelve exactamente material.MoClp (comportamiento histórico).
        decimal targetMo = row.MoClp == 0m
            ? MaterialMoResolver.MoClpEfectiva(material, session.ProjectInfo)
            : row.MoClp;
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

        int slot = src.IsSessionBacked
            ? CubicacionPartidaOrder.Position(session.Cubicacion, selectedPartida.Id, src.Source)
            : int.MaxValue;
        if (slot < 0)
            slot = int.MaxValue;
        CubicacionPartidaOrder.InsertAtSlot(session.Cubicacion, selectedPartida.Id, row, slot);

        var vm = parent.CreateRowVm(row, isSessionBacked: true);
        parent.InitializeSectorRow(vm);
        parent.Rows.Add(vm);
        AlignVisibleRowsToPartidaOrder();
        parent.SelectedRow = vm;
        parent.HasPendingUndoForEdit = false;
        parent.MarkDirty();
        parent.NotifyIsEmptyChanged();
        parent.NotifyCubicacionSummaryChanged();
        parent.UpdatePartidaCount(selectedPartida);
        RefreshRowNumbers();
    }

    /// <summary>
    /// Deja la grilla en el mismo orden que la partida dentro de session.Cubicacion.
    /// Las casillas vacías quedan al final y no entran en la sesión.
    /// </summary>
    public void AlignVisibleRowsToPartidaOrder()
    {
        var selectedPartida = parent.SelectedPartida;
        if (selectedPartida == null || !selectedPartida.EsPartida)
            return;

        var selected = parent.SelectedRow;
        var ordered = CubicacionItemIndex.FilterRows(session.Cubicacion, selectedPartida.Id);
        var backed = new Dictionary<string, CubicacionRowVm>(StringComparer.OrdinalIgnoreCase);
        var padding = new List<CubicacionRowVm>();
        foreach (var vm in parent.Rows)
        {
            if (vm.IsSessionBacked && !string.IsNullOrWhiteSpace(vm.Id))
                backed[vm.Id] = vm;
            else
                padding.Add(vm);
        }

        parent.Rows.Clear();
        foreach (var src in ordered)
        {
            if (!backed.TryGetValue(src.Id ?? "", out var vm))
            {
                vm = parent.CreateRowVm(src);
                parent.InitializeSectorRow(vm);
            }

            parent.Rows.Add(vm);
        }

        foreach (var pad in padding)
            parent.Rows.Add(pad);

        EnsurePaddingRows();
        if (selected != null && parent.Rows.Contains(selected))
            parent.SelectedRow = selected;
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
