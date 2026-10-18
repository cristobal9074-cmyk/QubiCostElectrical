using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Apu;

/// <summary>
/// Coordina la persistencia, aplicación y generación de plantillas APU y conjuntos eléctricos
/// vinculados a la partida y recursos del espacio de trabajo APU.
/// </summary>
public sealed class ApuTemplateCoordinator
{
    private readonly IApuTemplateStore? templateStore;
    private readonly BudgetProjectSession session;
    private readonly WpfUndoService undo;
    private readonly Func<string, bool> confirmOverwriteTemplate;
    private readonly Action<string> setTemplateStoreStatusText;

    public ApuTemplateCoordinator(
        IApuTemplateStore? templateStore,
        BudgetProjectSession session,
        WpfUndoService undo,
        Func<string, bool> confirmOverwriteTemplate,
        Action<string> setTemplateStoreStatusText)
    {
        this.templateStore = templateStore;
        this.session = session;
        this.undo = undo;
        this.confirmOverwriteTemplate = confirmOverwriteTemplate;
        this.setTemplateStoreStatusText = setTemplateStoreStatusText;
    }

    public void GuardarComoPlantilla(ApuRow? currentApu, ApuPartidaVm? selectedPartida, bool canEdit)
    {
        if (!canEdit || currentApu == null || templateStore == null) return;
        var nombre = WpfDialogService.PromptText("Guardar plantilla",
            "Nombre de la plantilla:",
            selectedPartida?.Descripcion ?? "Nueva plantilla");
        if (string.IsNullOrWhiteSpace(nombre)) return;

        var json = JsonSerializer.Serialize(currentApu.Recursos,
            new JsonSerializerOptions { WriteIndented = false });
        var row = new ApuTemplateRow
        {
            Nombre = nombre.Trim(),
            Categoria = "APU",
            Descripcion = selectedPartida?.Descripcion ?? "",
            Unidad = selectedPartida?.Unidad ?? "",
            JsonRecursos = json,
        };
        templateStore.Save(row);
        WpfDialogService.Info("Plantilla guardada", $"Plantilla '{row.Nombre}' guardada correctamente.");
    }

    public void AplicarPlantilla(
        ApuRow? currentApu,
        ApuPartidaVm? selectedPartida,
        ObservableCollection<ApuRecursoVm> recursos,
        Func<ApuRecursoRow, ApuRecursoVm> createRecursoVm,
        Action onRowEdited,
        bool canEdit)
    {
        if (!canEdit || currentApu == null || templateStore == null) return;
        var dialog = new ApuTemplatePickerDialog(templateStore);
        if (dialog.ShowDialog() != true || dialog.SelectedTemplate == null) return;

        ApplyTemplate(dialog.SelectedTemplate, dialog.Reemplazar, currentApu, selectedPartida, recursos, createRecursoVm, onRowEdited, canEdit);
    }

    public bool ApplyTemplate(
        ApuTemplateRow template,
        bool reemplazar,
        ApuRow? currentApu,
        ApuPartidaVm? selectedPartida,
        ObservableCollection<ApuRecursoVm> recursos,
        Func<ApuRecursoRow, ApuRecursoVm> createRecursoVm,
        Action onRowEdited,
        bool canEdit)
    {
        if (!canEdit || currentApu == null) return false;

        List<ApuRecursoRow>? listaRecursos = null;
        try
        {
            listaRecursos = JsonSerializer.Deserialize<List<ApuRecursoRow>>(template.JsonRecursos ?? "[]",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            setTemplateStoreStatusText("La plantilla seleccionada no se pudo leer. Use otra plantilla o vuelva a exportarla.");
            WpfAppHost.Logger?.Warning("ApuWorkspace", "No se pudo deserializar la plantilla APU seleccionada.", ex);
        }

        if (listaRecursos == null || listaRecursos.Count == 0)
        {
            WpfDialogService.Info("Aplicar plantilla", "La plantilla no tiene recursos.");
            return false;
        }

        if (reemplazar && currentApu.Recursos.Count > 0)
        {
            var partidaDesc = selectedPartida?.Descripcion ?? currentApu.Descripcion ?? "la partida actual";
            var confirmMsg = $"El APU de '{partidaDesc}' ya contiene {currentApu.Recursos.Count} recurso(s) configurado(s).\n\n¿Está seguro de que desea reemplazarlos y eliminarlos definitivamente para aplicar la plantilla '{template.Nombre}'?";
            if (!confirmOverwriteTemplate(confirmMsg))
                return false;
        }

        undo.PushUndoPoint(session, "Aplicar plantilla APU");
        if (reemplazar)
        {
            currentApu.Recursos.Clear();
            recursos.Clear();
        }

        foreach (var r in listaRecursos)
        {
            r.Id = Guid.NewGuid().ToString("N");
            currentApu.Recursos.Add(r);
            recursos.Add(createRecursoVm(r));
        }
        onRowEdited();
        return true;
    }

    public void CrearConjunto(ApuRow? currentApu, ApuPartidaVm? selectedPartida, bool canEdit)
    {
        if (!canEdit || currentApu == null || currentApu.Recursos.Count == 0) return;

        var nombre = WpfDialogService.PromptText("Crear conjunto",
            "Nombre del conjunto:",
            selectedPartida?.Descripcion ?? "Nuevo conjunto");
        if (string.IsNullOrWhiteSpace(nombre)) return;

        var conjunto = new ConjuntoElectricoRow
        {
            Codigo = $"CE-{DateTime.Now:yyMMdd-HHmm}",
            Nombre = nombre.Trim(),
            Descripcion = selectedPartida?.Descripcion ?? "",
            Unidad = selectedPartida?.Unidad ?? "UN",
            TipoConjunto = ConjuntoTipo.General,
        };

        foreach (var r in currentApu.Recursos)
        {
            conjunto.Recursos.Add(new ConjuntoRecursoRow
            {
                Tipo = r.Tipo,
                Codigo = r.CodigoMaterial,
                Descripcion = r.Descripcion,
                Unidad = r.Unidad,
                Rendimiento = r.Rendimiento,
                Factor = 1m,
                PrecioUf = r.PrecioUf,
                ValorHHUf = r.ValorHHUf,
                MermaPct = r.MermaPct,
            });
        }

        session.ConjuntosElectricos.Add(conjunto);
        session.MarkDirty(ProjectDirtyScope.ConjuntosElectricos);
        bool guardadoGlobal = ConjuntoTemplateStore.TrySave(conjunto);
        WpfDialogService.Info("Conjunto creado",
            $"Conjunto '{conjunto.Nombre}' creado con {conjunto.Recursos.Count} recursos." +
            (guardadoGlobal
                ? "\nGuardado en la biblioteca global: visible en todos los proyectos."
                : "\nNo se pudo guardar en la biblioteca global (solo en este proyecto)."));
    }
}
