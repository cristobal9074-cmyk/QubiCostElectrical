using System.Text.Json;

namespace QubiCostElectrical;

public sealed class TplRecursoSnapshot
{
    public string RecursoId { get; set; } = "";
    public bool EsMaestro { get; set; }
    public string MaestroRecursoId { get; set; } = "";
    public decimal Factor { get; set; } = 1m;
    public decimal Rendimiento { get; set; } = 1m;
    public string TipoCalculo { get; set; } = "Factor";
    public decimal Espaciamiento { get; set; } = 1m;
    public string Formula { get; set; } = "";
    public string FormulaOperator { get; set; } = "=";
    public decimal Minimo { get; set; }

    public static TplRecursoSnapshot FromRecurso(ConjuntoRecursoRow r, IReadOnlyDictionary<string, decimal>? parametros)
    {
        decimal factor = r.Factor <= 0m ? 1m : r.Factor;
        if (parametros != null && parametros.TryGetValue("factor", out decimal gf) && gf > 0m)
            factor *= gf;
        return new()
        {
            RecursoId = r.Id,
            EsMaestro = r.EsMaestro,
            MaestroRecursoId = r.MaestroRecursoId ?? "",
            Factor = factor,
            Rendimiento = r.Rendimiento,
            TipoCalculo = r.TipoCalculo.ToString(),
            Espaciamiento = r.Espaciamiento,
            Formula = r.Formula ?? "",
            FormulaOperator = r.FormulaOperator ?? "=",
            Minimo = r.Minimo
        };
    }

    public string ToJson() => JsonSerializer.Serialize(this);
    public static TplRecursoSnapshot? FromJson(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<TplRecursoSnapshot>(json);

    public decimal ComputeQuantity(decimal baseQty)
    {
        if (TipoCalculo == "Espaciamiento" && Espaciamiento > 0m)
        {
            decimal raw = (decimal)Math.Ceiling((double)(baseQty / Espaciamiento));
            return Minimo > 0m && raw < Minimo ? Minimo : raw;
        }
        decimal rend = Rendimiento <= 0m ? 1m : Rendimiento;
        decimal effective = baseQty * rend;
        decimal f = Factor <= 0m ? 1m : Factor;
        decimal raw2 = FormulaOperator switch
        {
            "/" => effective / f,
            "+" => effective + f,
            "-" => effective - f,
            _ => effective * f
        };
        return Minimo > 0m && raw2 < Minimo ? Minimo : raw2;
    }

    public decimal ComputeQuantityForZone(decimal zoneBaseQty)
    {
        if (TipoCalculo == "Espaciamiento" && Espaciamiento > 0m)
        {
            decimal raw = (decimal)Math.Ceiling((double)(zoneBaseQty / Espaciamiento));
            return Minimo > 0m && raw < Minimo ? Minimo : raw;
        }
        // Si hay formula generada por FormulaOperator (MAESTRO / n, MAESTRO * n, etc.)
        if (!string.IsNullOrWhiteSpace(Formula) && FormulaOperator != "=")
        {
            var vars = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["MAESTRO"] = zoneBaseQty };
            try { return FormulaEvaluator.Evaluate(Formula, vars); }
            catch { }
        }
        decimal rend = Rendimiento <= 0m ? 1m : Rendimiento;
        decimal effective = zoneBaseQty * rend;
        decimal f = Factor <= 0m ? 1m : Factor;
        decimal raw2 = FormulaOperator switch
        {
            "/" => effective / f,
            "+" => effective + f,
            "-" => effective - f,
            _ => effective * f
        };
        return Minimo > 0m && raw2 < Minimo ? Minimo : raw2;
    }

    public decimal ComputeFormula(IReadOnlyDictionary<string, decimal> variables)
    {
        if (string.IsNullOrWhiteSpace(Formula))
            return 0m;
        try { return FormulaEvaluator.Evaluate(Formula, variables); }
        catch { return variables.GetValueOrDefault("MAESTRO", variables.Values.FirstOrDefault()); }
    }
}
