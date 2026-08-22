#nullable enable
using System;

namespace QubiCostElectrical;

public sealed record RecoveryCandidate(string ProjectPath, string SnapshotPath, DateTime CreatedUtc, long SizeBytes, string Sha256, bool IsValid, string? ErrorMessage);
