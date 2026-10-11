namespace QubiCostElectrical;

/// <summary>
/// Scope canónico de dirty-tracking. Reemplaza strings sueltos en call sites nuevos;
/// <see cref="Parse"/> preserva aliases históricos de <c>NormalizeScope</c>.
/// </summary>
public sealed class ProjectDirtyScope : IEquatable<ProjectDirtyScope>
{
    private ProjectDirtyScope(string key, ProjectSessionChangeKind changeKind, bool tracksDirty = true)
    {
        Key = key;
        ChangeKind = changeKind;
        TracksDirty = tracksDirty;
    }

    public string Key { get; }
    public ProjectSessionChangeKind ChangeKind { get; }
    public bool TracksDirty { get; }

    public static ProjectDirtyScope ProjectInfo { get; } = new("project_info", ProjectSessionChangeKind.ProjectInfo);
    public static ProjectDirtyScope Itemizado { get; } = new("itemizado", ProjectSessionChangeKind.Itemizado);
    public static ProjectDirtyScope Materiales { get; } = new("materiales", ProjectSessionChangeKind.Materiales);
    public static ProjectDirtyScope Cubicacion { get; } = new("cubicacion", ProjectSessionChangeKind.Cubicacion);
    public static ProjectDirtyScope CubicacionSc { get; } = new("cubicacion_sc", ProjectSessionChangeKind.Unknown);
    public static ProjectDirtyScope Plantillas { get; } = new("plantillas", ProjectSessionChangeKind.Plantillas);
    public static ProjectDirtyScope ConjuntosElectricos { get; } = new("conjuntos_electricos", ProjectSessionChangeKind.Unknown);
    public static ProjectDirtyScope Apu { get; } = new("apu", ProjectSessionChangeKind.Apu);
    public static ProjectDirtyScope OrdenesCambio { get; } = new("ordenes_cambio", ProjectSessionChangeKind.OrdenesCambio);
    public static ProjectDirtyScope Versiones { get; } = new("versiones", ProjectSessionChangeKind.Versiones);
    public static ProjectDirtyScope QtyHeaders { get; } = new("qty_headers", ProjectSessionChangeKind.QtyHeaders);
    public static ProjectDirtyScope Zonas { get; } = new("zonas", ProjectSessionChangeKind.Zonas);
    public static ProjectDirtyScope Solicitudes { get; } = new("solicitudes", ProjectSessionChangeKind.Unknown);
    public static ProjectDirtyScope Epp { get; } = new("epp", ProjectSessionChangeKind.Unknown);
    public static ProjectDirtyScope ClosingPolicy { get; } = new("closing_policy", ProjectSessionChangeKind.Unknown);
    public static ProjectDirtyScope GastosOperacionales { get; } = new("gastos_operacionales", ProjectSessionChangeKind.Unknown);
    public static ProjectDirtyScope NegotiatedDiscounts { get; } = new("negotiated_discounts", ProjectSessionChangeKind.Unknown);
    public static ProjectDirtyScope StaffingPlan { get; } = new("staffing_plan", ProjectSessionChangeKind.Unknown);
    public static ProjectDirtyScope EstadosPago { get; } = new("estados_pago", ProjectSessionChangeKind.Unknown);
    public static ProjectDirtyScope Saved { get; } = new("saved", ProjectSessionChangeKind.Saved, tracksDirty: false);
    public static ProjectDirtyScope Reset { get; } = new("reset", ProjectSessionChangeKind.Reset, tracksDirty: false);

    private static readonly Dictionary<string, ProjectDirtyScope> ByKey =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [ProjectInfo.Key] = ProjectInfo,
            [Itemizado.Key] = Itemizado,
            [Materiales.Key] = Materiales,
            [Cubicacion.Key] = Cubicacion,
            [CubicacionSc.Key] = CubicacionSc,
            [Plantillas.Key] = Plantillas,
            [ConjuntosElectricos.Key] = ConjuntosElectricos,
            [Apu.Key] = Apu,
            [OrdenesCambio.Key] = OrdenesCambio,
            [Versiones.Key] = Versiones,
            [QtyHeaders.Key] = QtyHeaders,
            [Zonas.Key] = Zonas,
            [Solicitudes.Key] = Solicitudes,
            [Epp.Key] = Epp,
            [ClosingPolicy.Key] = ClosingPolicy,
            [GastosOperacionales.Key] = GastosOperacionales,
            [NegotiatedDiscounts.Key] = NegotiatedDiscounts,
            [StaffingPlan.Key] = StaffingPlan,
            [EstadosPago.Key] = EstadosPago,
            [Saved.Key] = Saved,
            [Reset.Key] = Reset,
        };

    public static ProjectDirtyScope Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ProjectInfo;

        var trimmed = raw.Trim().ToLowerInvariant();
        var canonical = trimmed switch
        {
            "plantillas" or "template" or "templates" => "plantillas",
            "conjuntos" or "conjuntos_electricos" => "conjuntos_electricos",
            "apu" or "apus" or "hoja_costo" => "apu",
            "ordenes" or "ordenes_cambio" or "change_orders" => "ordenes_cambio",
            "version" or "versions" or "versiones" => "versiones",
            "qty" or "qty_headers" or "cantidad_headers" => "qty_headers",
            "calc" or "calculadora" => "calculadora",
            "material" or "materiales" => "materiales",
            "item" or "itemizado" => "itemizado",
            "cubicacion" or "cubicación" => "cubicacion",
            "cubicacion_sc" or "c.sc" or "csc" => "cubicacion_sc",
            "reset" => "reset",
            "saved" => "saved",
            "project" or "proyecto" or "project_info" => "project_info",
            "epp" => "epp",
            "solicitudes" => "solicitudes",
            "closing_policy" => "closing_policy",
            "zonas" => "zonas",
            "gastos_operacionales" or "go" => "gastos_operacionales",
            "negotiated_discounts" or "descuentos" => "negotiated_discounts",
            "staffing_plan" or "staffing" => "staffing_plan",
            "estados_pago" or "estados de pago" => "estados_pago",
            _ => trimmed
        };

        if (ByKey.TryGetValue(canonical, out var known))
            return known;

        return new ProjectDirtyScope(canonical, ProjectSessionChangeKind.Unknown);
    }

    public static bool TryParse(string? raw, out ProjectDirtyScope scope)
    {
        scope = Parse(raw);
        return true;
    }

    public bool Equals(ProjectDirtyScope? other) =>
        other is not null && string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => obj is ProjectDirtyScope other && Equals(other);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Key);

    public override string ToString() => Key;
}
