namespace QubiCostElectrical;

public sealed record AuthorizationResult
{
    public bool IsAllowed { get; init; }
    public AuthorizationDenialCode? DenialCode { get; init; }
    public string UserMessage { get; init; } = "";
    public string RequiredPermission { get; init; } = "";
    public string AppliedPolicy { get; init; } = "";

    public static AuthorizationResult Allowed() => new() { IsAllowed = true };

    public static AuthorizationResult Denied(
        AuthorizationDenialCode code,
        string message,
        string permission = "",
        string policy = "") => new()
        {
            IsAllowed = false,
            DenialCode = code,
            UserMessage = message,
            RequiredPermission = permission,
            AppliedPolicy = policy
        };
}
