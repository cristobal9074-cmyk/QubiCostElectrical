using System;
using System.Collections.Generic;
using System.Text.Json;

namespace QubiCostElectrical;

public static class ApuTemplateSeeder
{
    private const string SentinelId = "__seeded_v1";

    public static void SeedIfNeeded(IApuTemplateStore store)
    {
        var existing = store.GetAll();
        if (existing.Any(t => t.Id == SentinelId)) return;

        foreach (var t in BuildTemplates())
            store.Save(t);

        store.Save(new ApuTemplateRow
        {
            Id = SentinelId,
            Nombre = "__seeded_v1",
            Categoria = "__system",
            CreadoEn = DateTime.UtcNow,
            ActualizadoEn = DateTime.UtcNow
        });
    }

    private static IEnumerable<ApuTemplateRow> BuildTemplates()
    {
        var now = DateTime.UtcNow;

        yield return T("apu-cond-2-5", "Conductor Cu 2.5mm² THHN", "Conductores", "ml", now,
            Mat("COND-2.5", "Conductor Cu 2.5mm² THHN", "ml", 1.05m),
            Mo("Electricista", 0.05m));

        yield return T("apu-cond-4", "Conductor Cu 4mm² THHN", "Conductores", "ml", now,
            Mat("COND-4", "Conductor Cu 4mm² THHN", "ml", 1.05m),
            Mo("Electricista", 0.06m));

        yield return T("apu-cond-6", "Conductor Cu 6mm² THHN", "Conductores", "ml", now,
            Mat("COND-6", "Conductor Cu 6mm² THHN", "ml", 1.05m),
            Mo("Electricista", 0.07m));

        yield return T("apu-cond-10", "Conductor Cu 10mm² THHN", "Conductores", "ml", now,
            Mat("COND-10", "Conductor Cu 10mm² THHN", "ml", 1.05m),
            Mo("Electricista", 0.08m));

        yield return T("apu-tubo-pvc-34", "Tubería PVC 3/4\"", "Tuberías", "ml", now,
            Mat("TUBO-PVC-34", "Tubo PVC conduit 3/4\"", "ml", 1.03m),
            Mat("UNION-PVC-34", "Unión PVC 3/4\"", "UN", 0.20m),
            Mo("Electricista", 0.04m));

        yield return T("apu-tubo-pvc-1", "Tubería PVC 1\"", "Tuberías", "ml", now,
            Mat("TUBO-PVC-1", "Tubo PVC conduit 1\"", "ml", 1.03m),
            Mat("UNION-PVC-1", "Unión PVC 1\"", "UN", 0.20m),
            Mo("Electricista", 0.05m));

        yield return T("apu-tomac-doble", "Tomacorriente doble", "Dispositivos", "UN", now,
            Mat("TOMAC-2P", "Tomacorriente doble 16A", "UN", 1m),
            Mat("CAJA-2X4", "Caja rectangular 2x4\"", "UN", 1m),
            Mat("PLACA-2P", "Placa tomacorriente", "UN", 1m),
            Mo("Electricista", 0.50m));

        yield return T("apu-interrup-simple", "Interruptor simple", "Dispositivos", "UN", now,
            Mat("INTER-1P", "Interruptor simple 10A", "UN", 1m),
            Mat("CAJA-2X4", "Caja rectangular 2x4\"", "UN", 1m),
            Mat("PLACA-1P", "Placa interruptor simple", "UN", 1m),
            Mo("Electricista", 0.50m));

        yield return T("apu-interrup-doble", "Interruptor doble", "Dispositivos", "UN", now,
            Mat("INTER-2P", "Interruptor doble 10A", "UN", 1m),
            Mat("CAJA-2X4", "Caja rectangular 2x4\"", "UN", 1m),
            Mat("PLACA-2P-I", "Placa interruptor doble", "UN", 1m),
            Mo("Electricista", 0.60m));

        yield return T("apu-luminaria-led-sob", "Luminaria LED sobreponer", "Luminarias", "UN", now,
            Mat("LUM-LED-SOB", "Luminaria LED sobreponer 18W", "UN", 1m),
            Mo("Electricista", 0.50m));

        yield return T("apu-luminaria-emp", "Luminaria LED empotrada", "Luminarias", "UN", now,
            Mat("LUM-LED-EMP", "Downlight LED empotrado 12W", "UN", 1m),
            Mo("Electricista", 0.60m));

        yield return T("apu-tablero-12", "Tablero eléctrico 12 circuitos", "Tableros", "UN", now,
            Mat("TAB-12C", "Tablero eléctrico 12 circuitos", "UN", 1m),
            Mo("Electricista", 4.00m),
            Mo("Ayudante eléctrico", 2.00m));

        yield return T("apu-breaker-1p", "Breaker 1P hasta 30A", "Tableros", "UN", now,
            Mat("BRK-1P", "Breaker 1P 10-30A", "UN", 1m),
            Mo("Electricista", 0.30m));

        yield return T("apu-puesta-tierra", "Puesta a tierra con varilla", "Puesta a Tierra", "GL", now,
            Mat("VARILLA-PT", "Varilla copperweld 5/8\"x1.8m", "UN", 1m),
            Mat("COND-PT", "Conductor Cu desnudo 16mm²", "ml", 5m),
            Mat("CONECTOR-PT", "Conector varilla a conductor", "UN", 1m),
            Mo("Electricista", 2.00m));
    }

    private static ApuTemplateRow T(string id, string nombre, string categoria, string unidad, DateTime now, params ApuRecursoRow[] recursos) =>
        new()
        {
            Id = id,
            Nombre = nombre,
            Categoria = categoria,
            Unidad = unidad,
            JsonRecursos = JsonSerializer.Serialize(recursos),
            CreadoEn = now,
            ActualizadoEn = now
        };

    private static ApuRecursoRow Mat(string codigo, string desc, string unidad, decimal rend) =>
        new() { Tipo = ApuTipoRecurso.MAT, CodigoMaterial = codigo, Descripcion = desc, Unidad = unidad, Rendimiento = rend };

    private static ApuRecursoRow Mo(string desc, decimal hh) =>
        new() { Tipo = ApuTipoRecurso.MO, Descripcion = desc, Unidad = "HH", Rendimiento = hh };
}
