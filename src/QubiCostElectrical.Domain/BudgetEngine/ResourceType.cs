namespace QubiCostElectrical;

public enum ResourceType
{
    Material,
    Labor,
    Equipment,
    Tool,
    Subcontract,
    Transport,
    Auxiliary,
    Other
}

public static class ResourceTypeExtensions
{
    public static ApuTipoRecurso ToApuTipo(this ResourceType type) => type switch
    {
        ResourceType.Material => ApuTipoRecurso.MAT,
        ResourceType.Labor => ApuTipoRecurso.MO,
        ResourceType.Equipment => ApuTipoRecurso.EQ,
        ResourceType.Tool => ApuTipoRecurso.HERR,
        ResourceType.Subcontract => ApuTipoRecurso.SC,
        ResourceType.Transport => ApuTipoRecurso.TR,
        ResourceType.Auxiliary => ApuTipoRecurso.HH,
        ResourceType.Other => ApuTipoRecurso.MAT,
        _ => ApuTipoRecurso.MAT
    };

    public static ResourceType FromApuTipo(this ApuTipoRecurso apu) => apu switch
    {
        ApuTipoRecurso.MAT => ResourceType.Material,
        ApuTipoRecurso.MO => ResourceType.Labor,
        ApuTipoRecurso.EQ => ResourceType.Equipment,
        ApuTipoRecurso.HERR => ResourceType.Tool,
        ApuTipoRecurso.SC => ResourceType.Subcontract,
        ApuTipoRecurso.TR => ResourceType.Transport,
        ApuTipoRecurso.HH => ResourceType.Auxiliary,
        _ => ResourceType.Other
    };
}
