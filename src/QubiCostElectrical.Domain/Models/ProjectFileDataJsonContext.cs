using System.Text.Json.Serialization;

namespace QubiCostElectrical;

/// <summary>
/// Contexto de serialización generado en tiempo de compilación para el snapshot de
/// Undo/Redo (<see cref="BudgetProjectSession.CreateSnapshot"/>). El deep-clone vía JSON
/// con <see cref="System.Text.Json.JsonSerializer"/> reflexivo (sin este contexto) tarda
/// ~3s en un proyecto real de miles de recursos porque resuelve metadata por reflexión en
/// cada llamada; con el generador de código, esa metadata se genera en compilación y el
/// (de)serializado usa acceso directo a propiedades — mismo resultado, mucho más rápido.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(ProjectFileData))]
public sealed partial class ProjectFileDataJsonContext : JsonSerializerContext
{
}
