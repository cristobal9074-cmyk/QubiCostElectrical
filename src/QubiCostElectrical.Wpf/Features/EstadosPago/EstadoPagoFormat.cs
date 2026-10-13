using System;

namespace QubiCostElectrical.Wpf.Features.EstadosPago;

/// <summary>Textos y formatos compartidos de la pantalla Estados de pago.</summary>
public static class EstadoPagoFormat
{
    public static readonly string[] MesesNombres =
        { "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };

    public static readonly string[] MesesLargos =
        { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };

    public static readonly string[] MesesCortos =
        { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };

    public static (string Label, string Kind) EstadoBadge(string estado) => estado switch
    {
        EstadoPagoEst.Borrador => ("Borrador", "Info"),
        EstadoPagoEst.Revision => ("Revisión", "Warn"),
        EstadoPagoEst.Pago => ("Pago", "Ok"),
        _ => ("Pendiente", "Muted")
    };

    public static string Uf(decimal valor) => $"{valor:N3} UF";

    /// <summary>Monto en pesos chilenos (proyectos con MonedaPresupuesto = CLP).</summary>
    public static string Clp(decimal valor) =>
        $"$ {Math.Round(valor, 0, MidpointRounding.AwayFromZero):N0}";

    public static string Pct(decimal valor) => $"{valor:N1} %";

    public static string PctConfig(decimal valor) => $"{valor:0.##} %";
}
