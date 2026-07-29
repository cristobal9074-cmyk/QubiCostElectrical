namespace QubiCostElectrical;

public sealed class UserSession
{
    public UserAccount Account { get; init; } = null!;
    public Guid Token { get; init; } = Guid.NewGuid();
    public DateTime LoginTime { get; init; } = AppClock.Now;
    public bool IsValid { get; set; } = true;

    public string DisplayName => Account.DisplayName;
    public string Email => Account.Email;
    public UserRole Role => Account.Role;
    public string Initials => BuildInitials(Account.DisplayName);

    private static string BuildInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "US";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return parts[0].Length >= 2 ? parts[0][..2].ToUpperInvariant() : parts[0].ToUpperInvariant();
        return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
    }
}
