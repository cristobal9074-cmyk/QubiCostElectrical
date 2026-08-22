#nullable enable
using System;

namespace QubiCostElectrical;

public sealed record LicenseInfo(string Company, string Rut, DateTime IssuedAt, DateTime ExpiresAt, LicenseStatus Status);
