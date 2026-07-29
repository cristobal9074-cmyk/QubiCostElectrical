using System;
using System.Collections.Generic;
using System.Text.Json;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Templates;

public sealed class ApuTemplateItemVm : ViewModelBase
{
    public ApuTemplateItemVm(ApuTemplateRow row)
    {
        Source = row;
        RecursoCount = TryDeserializeCount(row.JsonRecursos);
    }

    public ApuTemplateRow Source { get; }
    public string Nombre => Source.Nombre;
    public string Categoria => Source.Categoria;
    public string Descripcion => Source.Descripcion;
    public string Unidad => Source.Unidad;
    public int RecursoCount { get; }
    public DateTime ActualizadoEn => Source.ActualizadoEn;
    public string ActualizadoLabel => Source.ActualizadoEn.ToString("dd/MM/yyyy");

    private static int TryDeserializeCount(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;
        try
        {
            var rows = JsonSerializer.Deserialize<List<ApuRecursoRow>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return rows?.Count ?? 0;
        }
        catch { return 0; }
    }
}
