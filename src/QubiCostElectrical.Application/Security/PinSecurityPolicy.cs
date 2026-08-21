namespace QubiCostElectrical;

/// <summary>Política de PIN configurable por instalación (Preferencias globales).</summary>
public sealed class PinSecurityPolicy
{
    public int MinLength { get; set; } = 4;
    public int MaxLength { get; set; } = 6;
    public bool BlockSequences { get; set; } = true;
    public bool BlockRepeats { get; set; } = true;

    public static PinSecurityPolicy Default { get; } = new();

    public static bool IsCompliant(string pin, PinSecurityPolicy? policy)
    {
        policy ??= Default;
        if (string.IsNullOrWhiteSpace(pin))
            return false;
        if (pin.Any(ch => !char.IsDigit(ch)))
            return false;
        if (pin.Length < policy.MinLength || pin.Length > policy.MaxLength)
            return false;

        if (policy.BlockRepeats && pin.All(ch => ch == pin[0]))
            return false;

        if (!policy.BlockSequences)
            return true;

        bool ascending = true;
        bool descending = true;
        for (int i = 1; i < pin.Length; i++)
        {
            ascending &= pin[i] == pin[i - 1] + 1;
            descending &= pin[i] == pin[i - 1] - 1;
        }

        return !(ascending || descending);
    }
}
