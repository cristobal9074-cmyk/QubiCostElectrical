using System;

namespace QubiCostElectrical.Wpf.Features.Versions;

/// <summary>
/// Opción para el selector de versión de destino (Versión B) en el comparador de revisiones.
/// Permite seleccionar el presupuesto en curso (Estado actual) o cualquier versión congelada.
/// </summary>
public sealed class ComparisonTargetOption
{
    public bool IsCurrentState { get; }
    public VersionRowVm? Version { get; }
    public string Codigo { get; }
    public string Nombre { get; }
    public string DisplayText { get; }
    public string SubText { get; }

    public static ComparisonTargetOption CreateCurrentState() =>
        new(
            isCurrentState: true,
            version: null,
            codigo: "ACTUAL",
            nombre: "Estado actual (en curso)",
            displayText: "● Estado actual (en curso)",
            subText: "Presupuesto activo en edición");

    public static ComparisonTargetOption FromVersion(VersionRowVm version) =>
        new(
            isCurrentState: false,
            version: version,
            codigo: version.Codigo,
            nombre: version.Nombre,
            displayText: $"{version.Codigo} — {version.Nombre}",
            subText: $"{version.FechaModificacion:dd/MM/yyyy HH:mm} · {version.CreadoPor}");

    private ComparisonTargetOption(
        bool isCurrentState,
        VersionRowVm? version,
        string codigo,
        string nombre,
        string displayText,
        string subText)
    {
        IsCurrentState = isCurrentState;
        Version = version;
        Codigo = codigo;
        Nombre = nombre;
        DisplayText = displayText;
        SubText = subText;
    }

    public override string ToString() => DisplayText;
}
