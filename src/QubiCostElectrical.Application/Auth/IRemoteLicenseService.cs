namespace QubiCostElectrical;

public sealed record RemoteLicenseVerificationResult(
    bool IsValid,
    string? DisplayName,
    string? ExpiresAt,
    string? ErrorMessage,
    string? Reason);

public interface IRemoteLicenseService
{
    Task<RemoteLicenseVerificationResult> VerifyLicenseAsync(string email, string password, CancellationToken cancellationToken = default);
}
