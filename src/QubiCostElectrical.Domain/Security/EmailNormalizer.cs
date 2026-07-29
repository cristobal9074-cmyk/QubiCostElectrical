using System;

namespace QubiCostElectrical;

public static class EmailNormalizer
{
    public static string Normalize(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "";
        return email.Trim().ToLowerInvariant();
    }

    public static bool EqualsNormalized(string? a, string? b) =>
        string.Equals(Normalize(a), Normalize(b), StringComparison.Ordinal);

    public static bool IsValid(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Contains('@');
}
