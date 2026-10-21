using System;
using System.IO;

namespace QubiCostElectrical.Tests.Fixtures;

/// <summary>
/// Compuerta de acceso a proyectos de obra reales (archivos .qce del usuario).
///
/// <para><b>Por qué existe.</b> La suite de tests debe ser hermética: no puede leer ni
/// escribir archivos de cliente que viven fuera del repositorio. Un test anterior
/// (<c>SyncAndVerifyPel039A2026Prueba2</c>) escribía sobre los <c>.qce</c> de obra
/// reales del usuario en cada <c>dotnet test</c>, pisando presupuestos reales. Ese
/// script de calibración vive ahora en <c>tools/LiveProjectCalibration</c> y se ejecuta
/// a mano, nunca desde la suite.</para>
///
/// <para><b>Regla.</b> Todo test que necesite un proyecto real debe pasar por esta
/// compuerta. Por defecto está cerrada: los tests se saltan. Se abre solo si el
/// entorno define <c>QUBI_LIVE_PROJECTS=1</c>, y aun así cada test debe trabajar
/// sobre una <em>copia</em> en directorio temporal, nunca sobre el archivo original.</para>
///
/// <para><b>Defensa en profundidad.</b> <c>tests/ArchitectureTests/NoLivePathGuardTest</c>
/// falla el build si reaparece una ruta absoluta de usuario dentro de <c>tests/</c>.</para>
/// </summary>
public static class LiveProjectGate
{
    /// <summary>Variable de entorno que habilita el acceso a proyectos de obra reales.</summary>
    public const string EnabledVariable = "QUBI_LIVE_PROJECTS";

    /// <summary>Variable de entorno que sobrescribe la carpeta raíz de proyectos de obra.</summary>
    public const string RootVariable = "QUBI_LIVE_PROJECTS_ROOT";

    /// <summary>Variable de entorno que sobrescribe la carpeta raíz de libros Excel de referencia.</summary>
    public const string ExcelRootVariable = "QUBI_LIVE_EXCEL_ROOT";

    private static string DefaultRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Presupuestos Electricos");

    private static string DefaultExcelRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    /// <summary>True cuando el entorno habilitó explícitamente el acceso a proyectos reales.</summary>
    public static bool IsEnabled =>
        string.Equals(Environment.GetEnvironmentVariable(EnabledVariable), "1", StringComparison.Ordinal);

    /// <summary>Carpeta raíz configurada para proyectos de obra reales.</summary>
    public static string Root =>
        Environment.GetEnvironmentVariable(RootVariable) is { Length: > 0 } custom ? custom : DefaultRoot;

    /// <summary>Carpeta raíz configurada para libros Excel de referencia.</summary>
    public static string ExcelRoot =>
        Environment.GetEnvironmentVariable(ExcelRootVariable) is { Length: > 0 } custom ? custom : DefaultExcelRoot;

    /// <summary>
    /// Resuelve la ruta de un proyecto real. Devuelve false si la compuerta está cerrada
    /// o el archivo no existe, de modo que el llamador pueda saltarse el test.
    /// </summary>
    public static bool TryResolveProject(string fileName, out string path)
    {
        path = "";
        if (!IsEnabled || string.IsNullOrWhiteSpace(fileName)) return false;

        string candidate = Path.Combine(Root, fileName);
        if (!File.Exists(candidate)) return false;

        path = candidate;
        return true;
    }

    /// <summary>
    /// Resuelve la ruta de un libro Excel de referencia. Devuelve false si la compuerta
    /// está cerrada o el archivo no existe.
    /// </summary>
    public static bool TryResolveExcel(string fileName, out string path)
    {
        path = "";
        if (!IsEnabled || string.IsNullOrWhiteSpace(fileName)) return false;

        string candidate = Path.Combine(ExcelRoot, fileName);
        if (!File.Exists(candidate)) return false;

        path = candidate;
        return true;
    }

    /// <summary>
    /// Copia un proyecto real a un directorio temporal y devuelve la ruta de la copia.
    /// Toda prueba que modifique datos debe operar sobre la copia: el original es intocable.
    /// </summary>
    public static bool TryCopyProjectToTemp(string fileName, out string tempCopyPath, out string tempDir)
    {
        tempCopyPath = "";
        tempDir = "";

        if (!TryResolveProject(fileName, out string source)) return false;

        tempDir = Path.Combine(Path.GetTempPath(), "qubi_live_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        tempCopyPath = Path.Combine(tempDir, fileName);
        File.Copy(source, tempCopyPath);
        return true;
    }
}
