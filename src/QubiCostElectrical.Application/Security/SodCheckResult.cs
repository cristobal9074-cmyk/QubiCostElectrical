namespace QubiCostElectrical;

public sealed class SodCheckResult
{
    public bool IsBlocked { get; }
    public string Reason { get; }

    private SodCheckResult(bool isBlocked, string reason)
    {
        IsBlocked = isBlocked;
        Reason = reason;
    }

    public static SodCheckResult Allowed => new(false, "");
    public static SodCheckResult Blocked(string reason) => new(true, reason ?? "");
}
