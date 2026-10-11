namespace QubiCostElectrical;

public static class CodeHelper
{
    public static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "";
        var trimmed = code.Trim().ToUpperInvariant();
        while (trimmed.Contains("  ")) trimmed = trimmed.Replace("  ", " ");
        return trimmed;
    }

    public static bool Match(string? a, string? b) => Normalize(a) == Normalize(b);
}

public sealed class MaterialRow
{
    public string Codigo { get; set; } = "";
    public string CodigoNormalizado => CodeHelper.Normalize(Codigo);
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = "";
    public string Categoria { get; set; } = "";
    public string Subcategoria { get; set; } = "";
    public string Subcategoria2 { get; set; } = "";
    public string TipoInstalacion { get; set; } = "";
    public string EspecificacionTecnica { get; set; } = "";
    // Fuente del precio: SIEMPRE en pesos chilenos — es lo único que se escribe a mano en la
    // lista de precios (columna "Precio CLP" / "MO"). MatUf/MoUf quedan como valor DERIVADO
    // (RecalcularUf), y son los que sigue consumiendo el resto del motor (APU, presupuesto,
    // resumen, itemizado) sin cambios.
    public decimal MatClp { get; set; }
    public decimal MoClp { get; set; }
    /// <summary>CLP de material para cálculos. No usa UF reales como pesos (ver CubicacionRow).</summary>
    public decimal MatClpConFallback => MatClp > 0m ? MatClp : (MatUf > 1000m ? MatUf : 0m);
    /// <summary>CLP de MO para cálculos. No usa UF reales como pesos (ver CubicacionRow).</summary>
    public decimal MoClpConFallback => MoClp > 0m ? MoClp : (MoUf > 15m ? MoUf : 0m);
    public decimal MatUf { get; set; }
    public decimal MoUf { get; set; }
    public string MonedaPrecio { get; set; } = Moneda.CLP;
    public decimal UfUsadaClp { get; set; }

    /// <summary>Recalcula MatUf/MoUf desde MatClp/MoClp con el valor de UF vigente.</summary>
    public void RecalcularUf(decimal valorUfClp)
    {
        MatUf = PriceConversion.ClpToUf(MatClp, valorUfClp);
        MoUf = PriceConversion.ClpToUf(MoClp, valorUfClp);
        if (valorUfClp > 0m) UfUsadaClp = valorUfClp;
    }

    /// <summary>Vigente si el precio tiene menos de 6 meses; si no, hay que revisarlo.</summary>
    public bool RequiereRevisionPrecio =>
        FechaPrecio != default && (DateTime.Today - FechaPrecio.Date).Days >= PriceConversion.VigenteDiasUmbral;
    public string Proveedor { get; set; } = "";
    public string Marca { get; set; } = "";
    public string EquivalenciaKey { get; set; } = "";
    public string OrigenPrecio { get; set; } = "Lista de precios";
    public DateTime FechaPrecio { get; set; } = DateTime.Today;
    [System.Text.Json.Serialization.JsonIgnore]
    public int PrecioAntiguedadDias => FechaPrecio == default ? 0 : Math.Max(0, (DateTime.Today - FechaPrecio.Date).Days);
    public string UsuarioPrecio { get; set; } = "";
    public DateTime UltimaActualizacionPrecio { get; set; } = AppClock.Now;
    public string EstadoPrecio { get; set; } = EstadoRecurso.OK;
    public PrecioCongeladoRow? PrecioCongelado { get; set; }

    /// <summary>Rendimiento de instalación en unidades por día-hombre (columna REND de LISTA-PR). 0 = sin rendimiento.</summary>
    public decimal RendimientoDh { get; set; }

    /// <summary>Rendimiento por día-pareja (columna DIA x PAREJA de LISTA-PR). Informativo; 0 = no aplica.</summary>
    public decimal DiaXPareja { get; set; }

    /// <summary>Fórmula de M.O.: vacío = M.O. fija (MoClp); <see cref="MaterialMoResolver.MoFormulaPorcentaje"/> = Material × MoPorcentajeMaterial × FA.</summary>
    public string MoFormulaTipo { get; set; } = "";

    /// <summary>Porcentaje de M.O. sobre el material como número (2.5 = 2,5%). Solo aplica con fórmula "Porcentaje".</summary>
    public decimal MoPorcentajeMaterial { get; set; } = 2.5m;
}
