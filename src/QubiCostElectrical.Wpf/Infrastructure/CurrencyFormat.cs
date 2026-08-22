namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>Formato de monto en pesos usado en las pantallas de costo ("$ 1.234.567").
/// No requiere heredar de ViewModelBase — algunas filas de grilla (p. ej. MoDirectaRowVm) son
/// objetos inmutables simples sin base de ViewModel.</summary>
public static class CurrencyFormat
{
    public static string Clp(decimal amount) => $"$ {amount:N0}";
}
