namespace QubiCostElectrical;

public sealed class UserAccount
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Email { get; init; } = "";
    public string PinHash { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public UserRole Role { get; init; } = UserRole.Estimator;
    public bool IsActive { get; init; } = true;
    public DateTime CreatedAt { get; init; } = AppClock.Now;
    public DateTime? LastLoginAt { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
}
