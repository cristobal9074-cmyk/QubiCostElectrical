using System.Net;
using System.Text.Json;
using Xunit;

namespace QubiCostElectrical.Tests;

public sealed class UfRateServiceTests
{
    [Fact]
    public void Parse_ClaveUf_DevuelveVigenteDeSerie()
    {
        string json = """
            {
              "version": "1.0.0",
              "autor": "mindicador.cl",
              "fecha": "2026-10-03T10:00:00.000Z",
              "uf": [
                { "fecha": "2026-10-02T04:00:00.000Z", "valor": 41500.35 },
                { "fecha": "2026-10-01T04:00:00.000Z", "valor": 41480.10 }
              ]
            }
            """;

        var uf = UfRateService.Parse(json);

        Assert.Equal(41500.35m, uf.ValorClp);
        Assert.Equal(new DateTime(2026, 10, 2, 4, 0, 0, DateTimeKind.Utc), uf.Fecha);
    }

    [Fact]
    public void Parse_ClaveSerie_EsCompatible()
    {
        string json = """
            { "serie": [ { "fecha": "2026-09-30T04:00:00.000Z", "valor": 41321.77 } ] }
            """;

        var uf = UfRateService.Parse(json);

        Assert.Equal(41321.77m, uf.ValorClp);
        Assert.Equal(new DateTime(2026, 9, 30, 4, 0, 0, DateTimeKind.Utc), uf.Fecha);
    }

    [Fact]
    public void Parse_RaizArray_EsCompatible()
    {
        string json = """[ { "fecha": "2026-09-29T04:00:00.000Z", "valor": 41300 } ]""";

        var uf = UfRateService.Parse(json);

        Assert.Equal(41300m, uf.ValorClp);
    }

    [Fact]
    public void Parse_UfComoObjeto_IndicadorUnico()
    {
        string json = """
            { "uf": { "codigo": "uf", "nombre": "UF", "fecha": "2026-10-02", "valor": 41490.27 } }
            """;

        var uf = UfRateService.Parse(json);

        Assert.Equal(41490.27m, uf.ValorClp);
        Assert.Equal(new DateTime(2026, 10, 2), uf.Fecha);
    }

    [Fact]
    public void Parse_SerieDesordenada_TomaLaFechaMasReciente()
    {
        string json = """
            { "uf": [
                { "fecha": "2026-10-01T04:00:00.000Z", "valor": 41480.10 },
                { "fecha": "2026-10-03T04:00:00.000Z", "valor": 41510.90 }
            ] }
            """;

        var uf = UfRateService.Parse(json);

        Assert.Equal(41510.90m, uf.ValorClp);
        Assert.Equal(new DateTime(2026, 10, 3, 4, 0, 0, DateTimeKind.Utc), uf.Fecha);
    }

    [Theory]
    [InlineData("41500.35", 41500.35)]
    [InlineData("41.500,35", 41500.35)]
    [InlineData("41500,35", 41500.35)]
    public void Parse_ValorComoString_AceptaFormatos(string valorJson, decimal esperado)
    {
        string json = $$"""{ "uf": [ { "valor": "{{valorJson}}" } ] }""";

        var uf = UfRateService.Parse(json);

        Assert.Equal(esperado, uf.ValorClp);
        Assert.Equal(DateTime.Today, uf.Fecha);
    }

    [Fact]
    public void Parse_SinDatos_LanzaInvalidData()
    {
        Assert.Throws<InvalidDataException>(() => UfRateService.Parse("""{ "uf": [] }"""));
        Assert.Throws<InvalidDataException>(() => UfRateService.Parse("""{ "autor": "x" }"""));
    }

    [Fact]
    public void Parse_JsonMalformado_LanzaJsonException()
    {
        Assert.ThrowsAny<JsonException>(() => UfRateService.Parse("{ no es json"));
    }

    [Fact]
    public async Task GetUfHoyAsync_ConHttpClientStub_SinRed()
    {
        string json = """{ "uf": [ { "fecha": "2026-10-02T04:00:00.000Z", "valor": 41500.35 } ] }""";
        var svc = new UfRateService(new HttpClient(new StubHandler(json)));

        var uf = await svc.GetUfHoyAsync();

        Assert.Equal(41500.35m, uf.ValorClp);
    }

    [Fact]
    public async Task GetUfHoyAsync_ErrorHttp_LanzaHttpRequestException()
    {
        var svc = new UfRateService(new HttpClient(new StubHandler("", HttpStatusCode.ServiceUnavailable)));

        await Assert.ThrowsAsync<HttpRequestException>(() => svc.GetUfHoyAsync());
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string json;
        private readonly HttpStatusCode status;

        public StubHandler(string json, HttpStatusCode status = HttpStatusCode.OK)
        {
            this.json = json;
            this.status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
    }
}
