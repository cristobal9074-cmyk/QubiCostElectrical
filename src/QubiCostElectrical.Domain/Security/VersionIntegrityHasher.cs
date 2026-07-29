using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace QubiCostElectrical;

public static class VersionIntegrityHasher
{
    public static string CalculateHash(BudgetVersionRow version)
    {
        if (version == null) return string.Empty;

        var sb = new StringBuilder();
        sb.Append(version.Codigo ?? "").Append("|")
          .Append(version.Nombre ?? "").Append("|")
          .Append(version.MontoVentaUf.ToString("F6")).Append("|");

        var snapshot = version.Snapshot ?? new BudgetVersionData();

        // 1. Itemizado relevante (ordenado por ID)
        var items = (snapshot.Itemizado ?? new List<ItemizadoRow>())
            .Where(x => x != null)
            .OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var item in items)
        {
            sb.Append(item.Id ?? "").Append(":")
              .Append(item.Item ?? "").Append(":")
              .Append(item.Descripcion ?? "").Append(":")
              .Append(item.Cantidad.ToString("F6")).Append(":")
              .Append(item.EsTitulo ? "1" : "0").Append(":")
              .Append(item.NivelJerarquico).Append("|");
        }

        // 2. Cubicaciones relevantes (ordenadas por ItemId y código)
        var cubs = (snapshot.Cubicacion ?? new List<CubicacionRow>())
            .Where(x => x != null)
            .OrderBy(x => x.ItemId ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Codigo ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Descripcion ?? "", StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var cub in cubs)
        {
            sb.Append(cub.ItemId ?? "").Append(":")
              .Append(cub.Codigo ?? "").Append(":")
              .Append(cub.Descripcion ?? "").Append(":")
              .Append(cub.Cantidad.ToString("F6")).Append(":")
              .Append(cub.MatClp.ToString("F6")).Append(":")
              .Append(cub.MoClp.ToString("F6")).Append("|");
        }

        // 3. APUs relevantes (ordenados por ItemId)
        var apus = (snapshot.Apus ?? new List<ApuRow>())
            .Where(x => x != null)
            .OrderBy(x => x.ItemId ?? "", StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var apu in apus)
        {
            sb.Append(apu.ItemId ?? "").Append(":")
              .Append(apu.PrecioUnitUf.ToString("F6")).Append(":")
              .Append(apu.CostoMaterialUf.ToString("F6")).Append(":")
              .Append(apu.CostoMoUf.ToString("F6")).Append(":")
              .Append(apu.CostoEquipoUf.ToString("F6")).Append("|");

            var resources = (apu.Recursos ?? new List<ApuRecursoRow>())
                .Where(r => r != null)
                .OrderBy(r => r.Codigo ?? "", StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var r in resources)
            {
                sb.Append(r.Codigo ?? "").Append(":")
                  .Append(r.Rendimiento.ToString("F6")).Append(":")
                  .Append(r.PrecioUf.ToString("F6")).Append("|");
            }
        }

        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes);
    }
}
