#nullable enable
namespace QubiCostElectrical;

public sealed record RecoveryResult(bool IsSuccess, string? ErrorId, string? ErrorMessage, string? BackupPath);
