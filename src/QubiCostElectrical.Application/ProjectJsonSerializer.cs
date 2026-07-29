using System.Text.Json;

namespace QubiCostElectrical;

public static class ProjectJsonSerializer
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}
