using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Linq;

namespace QubiCostElectrical.Infrastructure;

public sealed class AuthService : IAuthService, IDisposable
{
    private readonly SqliteAuthStore _store;
    private UserSession? _currentSession;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private static readonly TimeSpan TokenExpiration = TimeSpan.FromDays(30);

    private string SessionTokenPath => Path.Combine(ProductPaths.GetLocalAppDataRoot(), ".session_token");

    public AuthService()
    {
        _store = new SqliteAuthStore();
    }

    internal AuthService(SqliteAuthStore store)
    {
        _store = store;
    }

    public Task<Result<UserSession, AuthError>> LoginAsync(string email, string pin, bool remember)
    {
        var user = _store.FindByEmail(email);
        if (user == null)
            return Task.FromResult(Result<UserSession, AuthError>.Failure(AuthError.UserNotFound));

        if (!user.IsActive)
            return Task.FromResult(Result<UserSession, AuthError>.Failure(AuthError.AccountInactive));

        if (user.LockedUntil.HasValue && user.LockedUntil.Value > AppClock.Now)
            return Task.FromResult(Result<UserSession, AuthError>.Failure(AuthError.AccountLocked));

        if (!PinHasher.Verify(pin, user.PinHash))
        {
            _store.RecordFailedAttempt(email);
            return Task.FromResult(Result<UserSession, AuthError>.Failure(AuthError.InvalidCredentials));
        }

        _store.ResetFailedAttempts(email);
        _store.UpdateLastLogin(email);

        user.LastLoginAt = AppClock.Now;
        user.FailedAttempts = 0;
        user.LockedUntil = null;

        var session = new UserSession { Account = user };
        _currentSession = session;

        if (remember)
            PersistSessionToken(session);

        return Task.FromResult(Result<UserSession, AuthError>.Success(session));
    }

    public void Logout()
    {
        _currentSession = null;
        DeleteSessionToken();
    }

    public Task<UserSession?> TryRestoreSessionAsync()
    {
        try
        {
            var tokenPath = SessionTokenPath;
            if (!File.Exists(tokenPath))
                return Task.FromResult<UserSession?>(null);

            var encrypted = File.ReadAllBytes(tokenPath);
            var json = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);

            var tokenData = JsonSerializer.Deserialize<SessionTokenData>(json, JsonOpts);
            if (tokenData == null)
            {
                DeleteSessionToken();
                return Task.FromResult<UserSession?>(null);
            }

            if (AppClock.Now > tokenData.ExpiresAt)
            {
                DeleteSessionToken();
                return Task.FromResult<UserSession?>(null);
            }

            var user = _store.FindByEmail(tokenData.Email);
            if (user == null || !user.IsActive)
            {
                DeleteSessionToken();
                return Task.FromResult<UserSession?>(null);
            }

            var session = new UserSession { Account = user };
            _currentSession = session;
            return Task.FromResult<UserSession?>(session);
        }
        catch
        {
            DeleteSessionToken();
            return Task.FromResult<UserSession?>(null);
        }
    }

    public bool IsFirstUser()
    {
        return !_store.AnyUsersExist();
    }

    public Task<Result<UserSession, AuthError>> CreateFirstUserAsync(string email, string pin, string displayName)
        => CreateFirstUserAsync(email, pin, displayName, false);

    public Task<Result<UserSession, AuthError>> CreateFirstUserAsync(string email, string pin, string displayName, bool allowLicenseCredential)
    {
        if (!IsFirstUser())
            return Task.FromResult(Result<UserSession, AuthError>.Failure(AuthError.TokenInvalid));

        if (!ValidatePin(pin, allowLicenseCredential))
            return Task.FromResult(Result<UserSession, AuthError>.Failure(AuthError.PinTooShort));

        var user = new UserAccount
        {
            Email = email,
            PinHash = PinHasher.Hash(pin),
            DisplayName = displayName,
            Role = UserRole.Admin
        };

        _store.CreateUser(user);
        _store.AppendAdminAudit(email, "CreateFirstUser", email, "Cuenta administrador inicial creada.");

        var session = new UserSession { Account = user };
        _currentSession = session;
        return Task.FromResult(Result<UserSession, AuthError>.Success(session));
    }

    public Task<IReadOnlyList<AuthUserSummary>> GetUsersAsync()
    {
        var users = _store.GetAllUsers()
            .Select(ToSummary)
            .ToList();
        return Task.FromResult<IReadOnlyList<AuthUserSummary>>(users);
    }

    public Task<IReadOnlyList<AuthAdminAuditEntry>> GetAdminAuditAsync(int limit = 50)
    {
        return Task.FromResult(_store.GetRecentAdminAudit(limit));
    }

    public Task<Result<AuthUserSummary, AuthAdminError>> CreateUserAsync(string email, string pin, string displayName, UserRole role)
        => CreateUserAsync(email, pin, displayName, role, null);

    public Task<Result<AuthUserSummary, AuthAdminError>> CreateUserAsync(string email, string pin, string displayName, UserRole role, string? actorEmail)
        => CreateUserAsync(email, pin, displayName, role, actorEmail, false);

    public Task<Result<AuthUserSummary, AuthAdminError>> CreateUserAsync(string email, string pin, string displayName, UserRole role, string? actorEmail, bool allowLicenseCredential)
    {
        email = (email ?? "").Trim().ToLowerInvariant();
        displayName = (displayName ?? "").Trim();
        pin = pin ?? "";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(displayName))
            return Task.FromResult(Result<AuthUserSummary, AuthAdminError>.Failure(AuthAdminError.InvalidInput));
        if (!ValidatePin(pin, allowLicenseCredential))
            return Task.FromResult(Result<AuthUserSummary, AuthAdminError>.Failure(
                PinSecurityPolicy.IsCompliant(pin, LoadPinPolicy()) ? AuthAdminError.InvalidInput : AuthAdminError.WeakPin));
        if (_store.EmailExists(email))
            return Task.FromResult(Result<AuthUserSummary, AuthAdminError>.Failure(AuthAdminError.EmailAlreadyExists));

        var user = new UserAccount
        {
            Email = email,
            DisplayName = displayName,
            Role = role,
            PinHash = PinHasher.Hash(pin),
            IsActive = true
        };
        _store.CreateUser(user);
        LogAdmin(actorEmail, "CreateUser", email, $"Rol {role}.");
        return Task.FromResult(Result<AuthUserSummary, AuthAdminError>.Success(ToSummary(user)));
    }

    public Task<Result<AuthUserSummary, AuthAdminError>> SetUserRoleAsync(Guid userId, UserRole role)
        => SetUserRoleAsync(userId, role, null);

    public Task<Result<AuthUserSummary, AuthAdminError>> SetUserRoleAsync(Guid userId, UserRole role, string? actorEmail)
    {
        var existing = _store.FindById(userId);
        if (existing == null)
            return Task.FromResult(Result<AuthUserSummary, AuthAdminError>.Failure(AuthAdminError.UserNotFound));

        if (!_store.UpdateUserRole(userId, role))
            return Task.FromResult(Result<AuthUserSummary, AuthAdminError>.Failure(AuthAdminError.UserNotFound));

        var updated = _store.FindById(userId);
        if (updated == null)
            return Task.FromResult(Result<AuthUserSummary, AuthAdminError>.Failure(AuthAdminError.UserNotFound));

        LogAdmin(actorEmail, "SetUserRole", existing.Email, $"Nuevo rol: {role}.");
        return Task.FromResult(Result<AuthUserSummary, AuthAdminError>.Success(ToSummary(updated)));
    }

    public Task<Result<AuthUserSummary, AuthAdminError>> SetUserActiveAsync(Guid userId, bool isActive, string? currentUserEmail)
    {
        var existing = _store.FindById(userId);
        if (existing == null)
            return Task.FromResult(Result<AuthUserSummary, AuthAdminError>.Failure(AuthAdminError.UserNotFound));

        if (!isActive && !string.IsNullOrWhiteSpace(currentUserEmail)
            && string.Equals(existing.Email, currentUserEmail.Trim(), StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(Result<AuthUserSummary, AuthAdminError>.Failure(AuthAdminError.CannotDeactivateSelf));

        if (!_store.UpdateUserActive(userId, isActive))
            return Task.FromResult(Result<AuthUserSummary, AuthAdminError>.Failure(AuthAdminError.UserNotFound));

        var updated = _store.FindById(userId);
        if (updated == null)
            return Task.FromResult(Result<AuthUserSummary, AuthAdminError>.Failure(AuthAdminError.UserNotFound));

        LogAdmin(currentUserEmail, isActive ? "ActivateUser" : "DeactivateUser", existing.Email, isActive ? "Cuenta activada." : "Cuenta desactivada.");
        return Task.FromResult(Result<AuthUserSummary, AuthAdminError>.Success(ToSummary(updated)));
    }

    public Task<Result<AuthUserSummary, AuthAdminError>> ResetUserPinAsync(Guid userId, string newPin)
        => ResetUserPinAsync(userId, newPin, null, false);

    public Task<Result<AuthUserSummary, AuthAdminError>> ResetUserPinAsync(Guid userId, string newPin, string? actorEmail)
        => ResetUserPinAsync(userId, newPin, actorEmail, false);

    public Task<Result<AuthUserSummary, AuthAdminError>> ResetUserPinAsync(Guid userId, string newPin, string? actorEmail, bool allowLicenseCredential)
    {
        var existing = _store.FindById(userId);
        if (existing == null)
            return Task.FromResult(Result<AuthUserSummary, AuthAdminError>.Failure(AuthAdminError.UserNotFound));

        if (!ValidatePin(newPin, allowLicenseCredential))
            return Task.FromResult(Result<AuthUserSummary, AuthAdminError>.Failure(
                string.IsNullOrWhiteSpace(newPin) ? AuthAdminError.InvalidInput : AuthAdminError.WeakPin));

        var pinHash = PinHasher.Hash(newPin);
        if (!_store.UpdateUserPin(userId, pinHash))
            return Task.FromResult(Result<AuthUserSummary, AuthAdminError>.Failure(AuthAdminError.UserNotFound));

        var updated = _store.FindById(userId);
        if (updated == null)
            return Task.FromResult(Result<AuthUserSummary, AuthAdminError>.Failure(AuthAdminError.UserNotFound));

        LogAdmin(actorEmail, "ResetPin", existing.Email, "PIN restablecido por administrador.");
        return Task.FromResult(Result<AuthUserSummary, AuthAdminError>.Success(ToSummary(updated)));
    }

    public Task<Result<AuthUserSummary, AuthAdminError>> UnlockUserAsync(Guid userId, string? actorEmail)
    {
        var existing = _store.FindById(userId);
        if (existing == null)
            return Task.FromResult(Result<AuthUserSummary, AuthAdminError>.Failure(AuthAdminError.UserNotFound));

        if (!_store.UnlockUser(userId))
            return Task.FromResult(Result<AuthUserSummary, AuthAdminError>.Failure(AuthAdminError.UserNotFound));

        var updated = _store.FindById(userId);
        if (updated == null)
            return Task.FromResult(Result<AuthUserSummary, AuthAdminError>.Failure(AuthAdminError.UserNotFound));

        LogAdmin(actorEmail, "UnlockUser", existing.Email, "Cuenta desbloqueada por administrador.");
        return Task.FromResult(Result<AuthUserSummary, AuthAdminError>.Success(ToSummary(updated)));
    }

    private static AuthUserSummary ToSummary(UserAccount user) =>
        new(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role,
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt,
            user.FailedAttempts,
            user.LockedUntil);

    private void LogAdmin(string? actorEmail, string action, string targetEmail, string details)
    {
        _store.AppendAdminAudit(actorEmail ?? "sistema", action, targetEmail, details);
    }

    private static PinSecurityPolicy LoadPinPolicy()
    {
        try
        {
            var configured = UserPreferencesStore.Load().PinSecurity;
            if (configured == null)
                return PinSecurityPolicy.Default;

            // La política configurable solo puede endurecer, nunca relajar el piso documentado
            // (4-6 dígitos, sin secuencias ni repeticiones). Lo personalizable vive en las
            // credenciales de licencia (IsCredentialCompliant), no en el PIN local.
            return new PinSecurityPolicy
            {
                MinLength = Math.Max(configured.MinLength, 4),
                MaxLength = Math.Clamp(configured.MaxLength, 4, 6),
                BlockSequences = true,
                BlockRepeats = true
            };
        }
        catch
        {
            return PinSecurityPolicy.Default;
        }
    }

    private static bool ValidatePin(string pin, bool allowLicenseCredential)
    {
        if (allowLicenseCredential)
            return PinSecurityPolicy.IsCredentialCompliant(pin);

        var policy = LoadPinPolicy();
        if (!PinSecurityPolicy.IsCompliant(pin, policy))
            return false;
        return pin.Length >= policy.MinLength && pin.Length <= policy.MaxLength;
    }

    public static bool IsPinPolicyCompliant(string pin)
        => PinSecurityPolicy.IsCompliant(pin, PinSecurityPolicy.Default);

    private void PersistSessionToken(UserSession session)
    {
        try
        {
            var data = new SessionTokenData
            {
                Email = session.Email,
                ExpiresAt = AppClock.Now.Add(TokenExpiration)
            };

            var json = JsonSerializer.Serialize(data, JsonOpts);
            var encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(json),
                null,
                DataProtectionScope.CurrentUser);

            var dir = Path.GetDirectoryName(SessionTokenPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(SessionTokenPath, encrypted);
        }
        catch
        {
        }
    }

    private void DeleteSessionToken()
    {
        try
        {
            if (File.Exists(SessionTokenPath))
                File.Delete(SessionTokenPath);
        }
        catch
        {
        }
    }

    private sealed class SessionTokenData
    {
        public string Email { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
    }

    public void Dispose()
    {
        _store.Dispose();
    }
}
