#nullable enable
using System;

namespace QubiCostElectrical.Infrastructure;

public sealed record QclicPayload(string Company, string Rut, DateTime IssuedAt, DateTime ExpiresAt);
