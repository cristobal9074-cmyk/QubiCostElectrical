#nullable enable
using System;

namespace QubiCostElectrical;

public sealed record AutoSaveResult(bool Success, string? Path, DateTime? SavedAtUtc, string? ErrorMessage);
