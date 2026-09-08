using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Lógica pura del banco de precios: normalización de códigos, fusión de precios por
/// antigüedad y cálculo de qué precios del banco deberían actualizar un proyecto.
/// No conoce SQLite ni UI, por lo que es totalmente testeable.
/// </summary>
public sealed class PriceBankService
{
    /// <summary>Normaliza un código para usarlo como clave del banco (trim + mayúsculas).</summary>
    public string NormalizeCode(string? codigo)
        => (codigo ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>Construye una entrada de banco a partir de un material de proyecto.</summary>
    public PriceBankEntry FromMaterial(MaterialRow material, string proyectoOrigen = "")
    {
        if (material == null) throw new ArgumentNullException(nameof(material));
        return new PriceBankEntry
        {
            Codigo = NormalizeCode(material.Codigo),
            Descripcion = material.Descripcion ?? "",
            Unidad = material.Unidad ?? "",
            Categoria = material.Categoria ?? "",
            MatClp = material.MatClp,
            MoClp = material.MoClp,
            MatUf = material.MatUf,
            MoUf = material.MoUf,
            MonedaPrecio = string.IsNullOrWhiteSpace(material.MonedaPrecio) ? "CLP" : material.MonedaPrecio,
            Proveedor = material.Proveedor ?? "",
            Marca = material.Marca ?? "",
            FechaPrecio = material.FechaPrecio == default ? DateTime.Today : material.FechaPrecio,
            OrigenPrecio = string.IsNullOrWhiteSpace(material.OrigenPrecio) ? "Banco de precios" : material.OrigenPrecio,
            ProyectoOrigen = proyectoOrigen ?? "",
            ActualizadoEn = AppClock.Now
        };
    }

    /// <summary>
    /// El precio entrante reemplaza al existente solo si su fecha de precio es estrictamente
    /// más reciente. Empates y precios más viejos conservan el banco (evita pisar con datos rancios).
    /// </summary>
    public bool IsNewer(PriceBankEntry incoming, PriceBankEntry? existing)
    {
        if (incoming == null) return false;
        if (existing == null) return true;
        return incoming.FechaPrecio.Date > existing.FechaPrecio.Date;
    }

    /// <summary>
    /// Devuelve la entrada que debe quedar en el banco al combinar la existente con la entrante.
    /// </summary>
    public PriceBankEntry Merge(PriceBankEntry? existing, PriceBankEntry incoming)
    {
        if (incoming == null) throw new ArgumentNullException(nameof(incoming));
        if (existing == null) return incoming;
        if (!IsNewer(incoming, existing)) return existing;

        var merged = incoming.Clone();
        // Conserva descripción/categoría del banco si la entrante viene vacía.
        if (string.IsNullOrWhiteSpace(merged.Descripcion)) merged.Descripcion = existing.Descripcion;
        if (string.IsNullOrWhiteSpace(merged.Categoria)) merged.Categoria = existing.Categoria;
        if (string.IsNullOrWhiteSpace(merged.Unidad)) merged.Unidad = existing.Unidad;
        return merged;
    }

    /// <summary>
    /// Deduplica una lista de materiales por código, conservando el precio más reciente de cada uno.
    /// </summary>
    public IReadOnlyList<PriceBankEntry> Consolidate(IEnumerable<MaterialRow>? materiales, string proyectoOrigen = "")
    {
        var byCode = new Dictionary<string, PriceBankEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var material in materiales ?? Enumerable.Empty<MaterialRow>())
        {
            if (material == null) continue;
            string code = NormalizeCode(material.Codigo);
            if (string.IsNullOrEmpty(code)) continue;

            var incoming = FromMaterial(material, proyectoOrigen);
            byCode[code] = byCode.TryGetValue(code, out var existing) ? Merge(existing, incoming) : incoming;
        }
        return byCode.Values.ToList();
    }

    /// <summary>
    /// Calcula qué materiales del proyecto deberían actualizarse porque el banco tiene un precio
    /// más reciente. No muta el proyecto: devuelve la lista de cambios propuestos para que la UI
    /// los confirme y aplique.
    /// </summary>
    public IReadOnlyList<PriceBankPriceUpdate> BuildProjectUpdates(
        IEnumerable<MaterialRow>? projectMaterials,
        IReadOnlyDictionary<string, PriceBankEntry>? bankByCode,
        decimal valorUfProyecto = 0m)
    {
        var updates = new List<PriceBankPriceUpdate>();
        if (bankByCode == null || bankByCode.Count == 0) return updates;

        foreach (var material in projectMaterials ?? Enumerable.Empty<MaterialRow>())
        {
            if (material == null) continue;
            string code = NormalizeCode(material.Codigo);
            if (string.IsNullOrEmpty(code)) continue;
            if (!bankByCode.TryGetValue(code, out var bank)) continue;

            // Si los precios (CLP y UF) y la mano de obra son idénticos, no hay cambio.
            if (bank.MatUf == material.MatUf && bank.MoUf == material.MoUf && bank.MatClp == material.MatClp && bank.MoClp == material.MoClp)
                continue;

            // El banco manda si su precio es más reciente, o si la fecha es igual pero los precios/MO difieren,
            // o si el proyecto tiene precio/MO en cero (incompleto) pero el banco tiene valores.
            bool esMasNuevoOIgual = bank.FechaPrecio.Date >= (material.FechaPrecio == default ? DateTime.MinValue : material.FechaPrecio.Date);
            bool proyectoVacio = (material.MatClp == 0m && material.MoClp == 0m && material.MatUf == 0m && material.MoUf == 0m);
            bool moIncompleta = material.MoClp == 0m && material.MoUf == 0m && (bank.MoClp > 0m || bank.MoUf > 0m);
            bool proyectoIncompleto = proyectoVacio || moIncompleta;

            if (!esMasNuevoOIgual && !proyectoIncompleto)
                continue;

            decimal ufNuevo = material.UfUsadaClp > 0m ? material.UfUsadaClp : valorUfProyecto;

            // Migración perezosa a nivel de dominio: una entrada del banco cargada antes de este
            // cambio (o creada directo sin pasar por la UI, que migra al vuelo) puede traer
            // MatUf/MoUf pero MatClp/MoClp en 0. Se deriva el CLP desde la UF del banco para que
            // el precio aplicado al proyecto no quede en 0.
            decimal matClpNuevo = ResolveBankMatClp(bank, ufNuevo);
            decimal moClpNuevo = ResolveBankMoClp(bank, ufNuevo);

            updates.Add(new PriceBankPriceUpdate(
                code,
                material.Descripcion ?? "",
                material.MatUf,
                material.MoUf,
                bank.MatUf,
                bank.MoUf,
                material.FechaPrecio == default ? DateTime.MinValue : material.FechaPrecio,
                bank.FechaPrecio,
                material.Proveedor ?? "",
                bank.Proveedor ?? "",
                string.IsNullOrWhiteSpace(material.MonedaPrecio) ? Moneda.UF : material.MonedaPrecio,
                string.IsNullOrWhiteSpace(bank.MonedaPrecio) ? Moneda.UF : bank.MonedaPrecio,
                material.UfUsadaClp,
                ufNuevo,
                matClpNuevo,
                moClpNuevo,
                material.MatClp,
                material.MoClp));
        }
        return updates;
    }

    /// <summary>CLP del banco: MatClp manda. Si solo hay MatUf/MoUf (entrada legacy UF),
    /// convierte ×ValorUF. Si el banco ya es CLP (MatClp o MoClp &gt; 0) y falta un campo,
    /// usa <see cref="PriceConversion.LegacyUfFieldToClp"/> sin el bug MO=1→×UF.</summary>
    private static decimal ResolveBankMatClp(PriceBankEntry bank, decimal valorUfClp)
    {
        if (bank.MatClp > 0m) return bank.MatClp;
        if (bank.MatUf <= 0m) return 0m;
        if (bank.MoClp > 0m)
            return PriceConversion.LegacyUfFieldToClp(bank.MatUf, valorUfClp, isMo: false);
        return PriceConversion.UfToClp(bank.MatUf, valorUfClp);
    }

    private static decimal ResolveBankMoClp(PriceBankEntry bank, decimal valorUfClp)
    {
        if (bank.MoClp > 0m) return bank.MoClp;
        if (bank.MoUf <= 0m) return 0m;
        if (bank.MatClp > 0m)
            return PriceConversion.LegacyUfFieldToClp(bank.MoUf, valorUfClp, isMo: true);
        return PriceConversion.UfToClp(bank.MoUf, valorUfClp);
    }
}
