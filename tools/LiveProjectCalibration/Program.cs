using System.Globalization;
using QubiCostElectrical;
using QubiCostElectrical.Infrastructure;

namespace QubiCostElectrical.Tools.LiveProjectCalibration;

/// <summary>
/// Calibración de un proyecto de obra real contra el presupuesto de referencia.
///
/// <para><b>Por qué existe.</b> Esta lógica vivía dentro de la suite de tests como
/// <c>FactorAjusteMoServiceTests.SyncAndVerifyPel039A2026Prueba2</c>, y eso la convertía
/// en un destructor silencioso: cada <c>dotnet test</c> reescribía los <c>.qce</c> de
/// obra reales del usuario (borrando gastos operacionales, recalculando M.O. y forzando
/// el índice M.O./FA). Un test nunca debe escribir fuera de su directorio temporal.</para>
///
/// <para><b>Uso.</b> Es una herramienta manual, fuera de la suite. Por defecto hace
/// <em>dry-run</em>: imprime exactamente qué cambiaría y no toca nada. Para escribir hay
/// que pasar <c>--apply</c>, y la herramienta deja un <c>.precalibration</c> del
/// archivo destino antes de sobrescribirlo.</para>
///
/// <para><b>Ejemplo.</b></para>
/// <code>
/// dotnet run --project tools\LiveProjectCalibration -- ^
///   --source  "C:\...\PEL039A2026 PRUEBA NUMERO 2.backup.qce" ^
///   --target  "C:\...\PEL039A2026 PRUEBA NUMERO 2.qce" ^
///   --index   62.80 --apply
/// </code>
/// </summary>
internal static class Program
{
    private const decimal DefaultIndiceMoFa = 62.80m;
    private const decimal DefaultMoDirectaClp = 171_990_000m;

    private static int Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        string? source = ReadOption(args, "--source");
        string? target = ReadOption(args, "--target");
        bool apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
        decimal indiceMoFa = ReadDecimalOption(args, "--index") ?? DefaultIndiceMoFa;
        decimal moDirectaClp = ReadDecimalOption(args, "--mo-directa") ?? DefaultMoDirectaClp;

        if (string.IsNullOrWhiteSpace(target) || !File.Exists(target))
        {
            Console.Error.WriteLine($"Error: --target debe apuntar a un .qce existente. Recibido: '{target}'");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
        {
            Console.Error.WriteLine($"Error: --source debe apuntar a un .qce existente. Recibido: '{source}'");
            return 2;
        }

        var data = SQLiteProjectStore.LoadSnapshot(source);

        Console.WriteLine("=== Calibración de proyecto de obra ===");
        Console.WriteLine($"  Origen  : {source}");
        Console.WriteLine($"  Destino : {target}");
        Console.WriteLine($"  Índice M.O./FA : {indiceMoFa}");
        Console.WriteLine($"  M.O. directa   : {moDirectaClp:N0} CLP");
        Console.WriteLine($"  Modo    : {(apply ? "APLICAR (escribe)" : "DRY-RUN (no escribe)")}");
        Console.WriteLine();

        var before = Describe(data);
        Apply(data, indiceMoFa, moDirectaClp);
        var after = Describe(data);

        Console.WriteLine("--- Gastos operacionales ---");
        Console.WriteLine($"  antes: {before.GastosOperacionales}");
        Console.WriteLine($"  despues: {after.GastosOperacionales}");
        Console.WriteLine("--- Proyecto ---");
        Console.WriteLine($"  MoTotalClp antes: {before.MoTotalClp}  despues: {after.MoTotalClp}");
        Console.WriteLine($"  MoDirectaClp antes: {before.MoDirectaClp}  despues: {after.MoDirectaClp}");
        Console.WriteLine($"  IndiceMoFa antes: {before.IndiceMoFa}  despues: {after.IndiceMoFa}");
        Console.WriteLine($"  Materiales: {before.Materiales}  Cubicacion: {before.Cubicacion}");
        Console.WriteLine();

        if (!apply)
        {
            Console.WriteLine("Dry-run. No se escribió nada. Agrega --apply para escribir.");
            return 0;
        }

        string preCalibration = target + ".precalibration";
        try
        {
            File.Copy(target, preCalibration, overwrite: true);
            Console.WriteLine($"Copia previa: {preCalibration}");
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"No se pudo crear la copia previa: {ex.Message}. Se aborta.");
            return 3;
        }

        SQLiteProjectStore.SaveSnapshot(target, data);
        Console.WriteLine($"Escrito: {target}");
        return 0;
    }

    private static void Apply(ProjectFileData data, decimal indiceMoFa, decimal moDirectaClp)
    {
        data.Proyecto ??= new ProjectInfoRow();
        data.Proyecto.IndiceMoFa = indiceMoFa;
        data.Proyecto.MoDirectaClp = moDirectaClp;
        data.Proyecto.MoTotalClp = moDirectaClp;
        data.Proyecto.GastosGeneralesPct = 5.0m;
        data.Proyecto.UtilidadPct = 8.0m;
        data.Proyecto.ValorAgregadoEquiposPct = 3.0m;
        data.Proyecto.PoliticaCostosExcel = true;
        data.Proyecto.UsarFactoresPresentacionFamilia = true;

        data.GastosOperacionales ??= new List<GastoOperacionalRow>();
        data.GastosOperacionales.Clear();
        data.GastosOperacionales.Add(new GastoOperacionalRow
        {
            Codigo = "GO-001",
            Concepto = "Accesorios Opcionales",
            MontoClp = 2_796_890m
        });
        data.GastosOperacionales.Add(new GastoOperacionalRow
        {
            Codigo = "GO-002",
            Concepto = "Trámites, Documentación y Pruebas",
            MontoClp = 2_550_000m
        });

        foreach (var mat in data.Materiales)
        {
            if (mat.MoFormulaTipo == MaterialMoResolver.MoFormulaPorcentaje)
            {
                decimal pct = mat.MoPorcentajeMaterial > 0 ? mat.MoPorcentajeMaterial : 2.5m;
                mat.MoPorcentajeMaterial = pct;
                mat.MoClp = MaterialMoResolver.MoClpPorPorcentaje(mat.MatClp, pct, indiceMoFa);
            }
            else
            {
                mat.MoFormulaTipo = MaterialMoResolver.MoFormulaFactor;
                if (mat.MoFactorBase <= 0)
                    mat.MoFactorBase = mat.MoClp;
                mat.MoClp = MaterialMoResolver.MoClpPorFactor(mat.MoFactorBase, indiceMoFa);
            }
        }

        var porCodigo = data.Materiales
            .GroupBy(m => m.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in data.Cubicacion)
        {
            if (porCodigo.TryGetValue(row.Codigo, out var mat))
            {
                row.MatClp = mat.MatClp;
                row.MoClp = mat.MoClp;
            }
        }
    }

    private static (int GastosOperacionales, int Materiales, int Cubicacion, string MoTotalClp, string MoDirectaClp, string IndiceMoFa)
        Describe(ProjectFileData data)
    {
        return (
            data.GastosOperacionales?.Count ?? 0,
            data.Materiales?.Count ?? 0,
            data.Cubicacion?.Count ?? 0,
            data.Proyecto?.MoTotalClp.ToString("N0", CultureInfo.InvariantCulture) ?? "(null)",
            data.Proyecto?.MoDirectaClp.ToString("N0", CultureInfo.InvariantCulture) ?? "(null)",
            data.Proyecto?.IndiceMoFa.ToString(CultureInfo.InvariantCulture) ?? "(null)");
    }

    private static string? ReadOption(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        if (index < 0 || index + 1 >= args.Length) return null;
        return args[index + 1];
    }

    private static decimal? ReadDecimalOption(string[] args, string name)
    {
        string? raw = ReadOption(args, name);
        if (raw is null) return null;
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)
            ? value
            : null;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            QubiLiveProjectCalibration

            Recalibra un proyecto de obra (.qce) contra el presupuesto de referencia.
            Reemplaza al antiguo test SyncAndVerifyPel039A2026Prueba2, que escribia sobre
            los .qce reales del usuario en cada `dotnet test`.

            Uso:
              --source <archivo.qce>   Archivo base (respaldo) de donde se lee el snapshot.
              --target <archivo.qce>   Archivo destino a calibrar. Debe existir.
              --index <decimal>        Indice M.O./FA a aplicar. Default 62.80.
              --mo-directa <decimal>   M.O. directa en CLP. Default 171990000.
              --apply                  Escribe en el destino. Sin esta flag solo informa.

            Sin --apply la herramienta es un dry-run: no modifica nada.
            Con --apply deja una copia <destino>.precalibration antes de sobrescribir.
            """);
    }
}
