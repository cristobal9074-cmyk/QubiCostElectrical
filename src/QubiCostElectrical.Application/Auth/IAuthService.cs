namespace QubiCostElectrical;

public enum AuthError
{
    InvalidCredentials,
    AccountLocked,
    AccountInactive,
    PinTooShort,
    UserNotFound,
    SessionExpired,
    TokenInvalid
}

public enum AuthAdminError
{
    InvalidInput,
    WeakPin,
    EmailAlreadyExists,
    UserNotFound,
    CannotDeactivateSelf
}

public sealed record AuthUserSummary(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    int FailedAttempts,
    DateTime? LockedUntil);

public interface IAuthService
{
    Task<Result<UserSession, AuthError>> LoginAsync(string email, string pin, bool remember);
    void Logout();
    Task<UserSession?> TryRestoreSessionAsync();
    bool IsFirstUser();
    Task<Result<UserSession, AuthError>> CreateFirstUserAsync(string email, string pin, string displayName);
    Task<IReadOnlyList<AuthUserSummary>> GetUsersAsync();
    Task<Result<AuthUserSummary, AuthAdminError>> CreateUserAsync(string email, string pin, string displayName, UserRole role);
    Task<Result<AuthUserSummary, AuthAdminError>> CreateUserAsync(string email, string pin, string displayName, UserRole role, string? actorEmail);
    Task<Result<AuthUserSummary, AuthAdminError>> SetUserRoleAsync(Guid userId, UserRole role);
    Task<Result<AuthUserSummary, AuthAdminError>> SetUserRoleAsync(Guid userId, UserRole role, string? actorEmail);
    Task<Result<AuthUserSummary, AuthAdminError>> SetUserActiveAsync(Guid userId, bool isActive, string? currentUserEmail);
    Task<Result<AuthUserSummary, AuthAdminError>> ResetUserPinAsync(Guid userId, string newPin);
    Task<Result<AuthUserSummary, AuthAdminError>> ResetUserPinAsync(Guid userId, string newPin, string? actorEmail);
    Task<Result<AuthUserSummary, AuthAdminError>> UnlockUserAsync(Guid userId, string? actorEmail);
    Task<IReadOnlyList<AuthAdminAuditEntry>> GetAdminAuditAsync(int limit = 50);
}
