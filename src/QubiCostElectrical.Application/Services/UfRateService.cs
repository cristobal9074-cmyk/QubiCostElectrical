using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

/// <summary>UF vigente en Chile: valor en CLP y fecha de la serie recibida.</summary>
public sealed record UfRateResult(decimal ValorClp, DateTime Fecha);

/// <summary>
/// Obtiene la UF del día en Chile. Fuente: mindicador.cl (serie del Banco Central de Chile),
/// único endpoint público sin credenciales. Timeout 10 s; nunca lanza por red dentro de Parse.
/// </summary>
public sealed class UfRateService
{
    private const string EndpointUrl = "https://mindicador.cl/api/uf";

    private static readonly HttpClient SharedClient = CreateSharedClient();

    private readonly HttpClient http;

    public UfRateService() : this(SharedClient) { }

    public UfRateService(HttpClient http) => this.http = http;

    /// <summary>Descarga la serie de UF y devuelve la vigente. Lanza HttpRequestException/TaskCanceledException sin conexión.</summary>
    public async Task<UfRateResult> GetUfHoyAsync(CancellationToken cancellationToken = default)
    {
        string json = await http.GetStringAsync(EndpointUrl, cancellationToken).ConfigureAwait(false);
        return Parse(json);
    }

    /// <summary>
    /// Parsea la respuesta JSON del endpoint de UF. Tolera las tres formas conocidas:
    /// arreglo en "uf" (endpoint individual), arreglo en "serie" (endpoint global) o raíz-array.
    /// </summary>
    public static UfRateResult Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        JsonElement serie;
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("uf", out var ufProp) && ufProp.ValueKind == JsonValueKind.Array)
        {
            serie = ufProp;
        }
        else if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("uf", out var ufObj) && ufObj.ValueKind == JsonValueKind.Object)
        {
            if (!TryReadValor(ufObj, out var unico) || unico <= 0m)
                throw new InvalidDataException("La respuesta no incluye un valor de UF válido.");
            DateTime fechaUnica = ReadFecha(ufObj);
            return new UfRateResult(unico, fechaUnica == default ? DateTime.Today : fechaUnica);
        }
        else if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("serie", out var serieProp) && serieProp.ValueKind == JsonValueKind.Array)
        {
            serie = serieProp;
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            serie = root;
        }
        else
        {
            throw new InvalidDataException("Respuesta sin datos de UF (formato inesperado).");
        }

        decimal valor = 0m;
        DateTime fecha = default;
        foreach (var item in serie.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (!TryReadValor(item, out var candidato) || candidato <= 0m) continue;
            DateTime candidatoFecha = ReadFecha(item);
            if (valor <= 0m || candidatoFecha > fecha)
            {
                valor = candidato;
                fecha = candidatoFecha;
            }
        }

        if (valor <= 0m)
            throw new InvalidDataException("La respuesta no incluye un valor de UF válido.");

        if (fecha == default)
            fecha = DateTime.Today;

        return new UfRateResult(valor, fecha);
    }

    private static bool TryReadValor(JsonElement item, out decimal valor)
    {
        valor = 0m;
        if (!item.TryGetProperty("valor", out var v)) return false;

        if (v.ValueKind == JsonValueKind.Number)
            return v.TryGetDecimal(out valor);

        if (v.ValueKind != JsonValueKind.String) return false;

        string? text = v.GetString();
        if (string.IsNullOrWhiteSpace(text)) return false;

        text = text.Trim();
        if (text.Contains('.') && text.Contains(','))
            text = text.Replace(".", "").Replace(",", ".");
        else
            text = text.Replace(",", ".");

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out valor);
    }

    private static DateTime ReadFecha(JsonElement item)
    {
        if (!item.TryGetProperty("fecha", out var f) || f.ValueKind != JsonValueKind.String)
            return default;

        string? text = f.GetString();
        if (string.IsNullOrWhiteSpace(text)) return default;

        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d)
            ? d
            : default;
    }

    private static HttpClient CreateSharedClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("QubiCostElectrical");
        return client;
    }
}
