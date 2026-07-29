using System;

namespace QubiCostElectrical;

public sealed record PaymentCertificateId(string Value)
{
    public static PaymentCertificateId New() => new(Guid.NewGuid().ToString("N"));
    public static PaymentCertificateId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record PaymentCertificateLineId(string Value)
{
    public static PaymentCertificateLineId New() => new(Guid.NewGuid().ToString("N"));
    public static PaymentCertificateLineId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record ContractAdvanceId(string Value)
{
    public static ContractAdvanceId New() => new(Guid.NewGuid().ToString("N"));
    public static ContractAdvanceId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record ContractRetentionId(string Value)
{
    public static ContractRetentionId New() => new(Guid.NewGuid().ToString("N"));
    public static ContractRetentionId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record PaymentObservationId(string Value)
{
    public static PaymentObservationId New() => new(Guid.NewGuid().ToString("N"));
    public static PaymentObservationId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record PaymentCertificateRevisionId(string Value)
{
    public static PaymentCertificateRevisionId New() => new(Guid.NewGuid().ToString("N"));
    public static PaymentCertificateRevisionId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}
