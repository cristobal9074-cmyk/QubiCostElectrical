using System;
using System.Linq;

namespace QubiCostElectrical;

public sealed class ProjectPriceFreezeService
{
    public bool IsFrozen(ProjectInfoRow? proyecto) => proyecto?.PreciosCongelados == true;

    public bool BlocksAutomaticPriceSync(ProjectInfoRow? proyecto) => IsFrozen(proyecto);

    public void Freeze(ProjectFileData data, string usuario)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        var proyecto = data.Proyecto ??= new ProjectInfoRow();
        var now = AppClock.Now;

        proyecto.PreciosCongelados = true;
        proyecto.FechaCongelamientoPrecios = now;
        proyecto.UsuarioCongelamientoPrecios = usuario ?? "";
        proyecto.ValorUfClpCongelado = proyecto.ValorUfClp;
        proyecto.TipoCambioUsdClpCongelado = proyecto.TipoCambioUsdClp;
        proyecto.FechaTipoCambioCongelado = proyecto.FechaTipoCambio;

        foreach (var material in data.Materiales.Where(m => !string.IsNullOrWhiteSpace(m.Codigo)))
            material.PrecioCongelado = SnapshotMaterial(material, proyecto, now);
    }

    public void Unfreeze(ProjectInfoRow proyecto)
    {
        if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
        proyecto.PreciosCongelados = false;
    }

    public PrecioCongeladoRow SnapshotMaterial(MaterialRow material, ProjectInfoRow proyecto, DateTime? congeladoEn = null)
    {
        if (material == null) throw new ArgumentNullException(nameof(material));
        if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));

        decimal uf = material.UfUsadaClp > 0m ? material.UfUsadaClp : proyecto.ValorUfClp;
        decimal matClp = ResolveMaterialClp(material, uf, isMo: false);
        decimal moClp = ResolveMaterialClp(material, uf, isMo: true);

        return new PrecioCongeladoRow
        {
            MatClp = matClp,
            MoClp = moClp,
            MatUf = PriceConversion.ClpToUf(matClp, uf),
            MoUf = PriceConversion.ClpToUf(moClp, uf),
            Proveedor = material.Proveedor ?? "",
            FechaPrecio = material.FechaPrecio == default ? DateTime.Today : material.FechaPrecio,
            MonedaPrecio = Moneda.CLP,
            UfUsadaClp = uf,
            TipoCambioUsdClp = proyecto.TipoCambioUsdClp,
            CongeladoEn = congeladoEn ?? AppClock.Now
        };
    }

    private static decimal ResolveMaterialClp(MaterialRow material, decimal valorUfClp, bool isMo)
    {
        decimal clp = isMo ? material.MoClpConFallback : material.MatClpConFallback;
        if (clp > 0m) return clp;

        decimal ufLegacy = isMo ? material.MoUf : material.MatUf;
        if (ufLegacy <= 0m) return 0m;
        if (valorUfClp > 0m)
            return PriceConversion.UfToClp(ufLegacy, valorUfClp);
        return 0m;
    }
}
