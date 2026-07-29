using System;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class PaymentCertificateValidationTests
{
    private static PaymentCertificate CreateValidCertificate()
    {
        return new PaymentCertificate
        {
            Id = PaymentCertificateId.New(),
            ContractId = ContractId.From("CTR-001"),
            VisibleNumber = "EP-00001",
            PeriodStart = new DateTime(2026, 1, 1),
            PeriodEnd = new DateTime(2026, 1, 31),
            CutOffDate = new DateTime(2026, 1, 25),
            ExchangeRate = 1.0m,
            Presenter = "Juan Pérez",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = "test"
        };
    }

    [Fact]
    public void Validate_ValidCertificate_ReturnsNoErrors()
    {
        var cert = CreateValidCertificate();
        var errors = PaymentCertificateValidation.Validate(cert);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_NullContractId_ReturnsError()
    {
        var cert = CreateValidCertificate();
        cert.ContractId = null!;
        var errors = PaymentCertificateValidation.Validate(cert);

        Assert.Contains(errors, e => e.Contains("contrato", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_PeriodStartDefault_ReturnsError()
    {
        var cert = CreateValidCertificate();
        cert.PeriodStart = default;
        var errors = PaymentCertificateValidation.Validate(cert);

        Assert.Contains(errors, e => e.Contains("inicio", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_PeriodEndDefault_ReturnsError()
    {
        var cert = CreateValidCertificate();
        cert.PeriodEnd = default;
        var errors = PaymentCertificateValidation.Validate(cert);

        Assert.Contains(errors, e => e.Contains("fin", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_PeriodStartAfterPeriodEnd_ReturnsError()
    {
        var cert = CreateValidCertificate();
        cert.PeriodStart = new DateTime(2026, 2, 1);
        cert.PeriodEnd = new DateTime(2026, 1, 31);
        var errors = PaymentCertificateValidation.Validate(cert);

        Assert.Contains(errors, e => e.Contains("inicio no puede ser posterior", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_CutOffDateDefault_ReturnsError()
    {
        var cert = CreateValidCertificate();
        cert.CutOffDate = default;
        var errors = PaymentCertificateValidation.Validate(cert);

        Assert.Contains(errors, e => e.Contains("corte", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ExchangeRateZero_ReturnsError()
    {
        var cert = CreateValidCertificate();
        cert.ExchangeRate = 0;
        var errors = PaymentCertificateValidation.Validate(cert);

        Assert.Contains(errors, e => e.Contains("tipo de cambio", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ExchangeRateNegative_ReturnsError()
    {
        var cert = CreateValidCertificate();
        cert.ExchangeRate = -1m;
        var errors = PaymentCertificateValidation.Validate(cert);

        Assert.Contains(errors, e => e.Contains("tipo de cambio", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_PresenterEmpty_ReturnsError()
    {
        var cert = CreateValidCertificate();
        cert.Presenter = string.Empty;
        var errors = PaymentCertificateValidation.Validate(cert);

        Assert.Contains(errors, e => e.Contains("presentador", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_PresenterWhitespace_ReturnsError()
    {
        var cert = CreateValidCertificate();
        cert.Presenter = "   ";
        var errors = PaymentCertificateValidation.Validate(cert);

        Assert.Contains(errors, e => e.Contains("presentador", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_SubtotalValorizadoNegative_ReturnsError()
    {
        var cert = CreateValidCertificate();
        cert.SubtotalValorizado = -100m;
        var errors = PaymentCertificateValidation.Validate(cert);

        Assert.Contains(errors, e => e.Contains("subtotal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_MontoBrutoNegative_ReturnsError()
    {
        var cert = CreateValidCertificate();
        cert.MontoBruto = -100m;
        var errors = PaymentCertificateValidation.Validate(cert);

        Assert.Contains(errors, e => e.Contains("monto bruto", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_MontoLiquidoNegative_ReturnsError()
    {
        var cert = CreateValidCertificate();
        cert.MontoLiquido = -100m;
        var errors = PaymentCertificateValidation.Validate(cert);

        Assert.Contains(errors, e => e.Contains("monto líquido", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_MontoAcumuladoNegative_ReturnsError()
    {
        var cert = CreateValidCertificate();
        cert.MontoAcumulado = -100m;
        var errors = PaymentCertificateValidation.Validate(cert);

        Assert.Contains(errors, e => e.Contains("monto acumulado", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateLine_ValidLine_ReturnsNoErrors()
    {
        var line = new PaymentCertificateLine
        {
            Id = PaymentCertificateLineId.New(),
            Description = "Instalación de cable",
            Unit = "ML",
            CantidadContractual = 100m,
            PrecioUnitarioContractual = 1000m,
            CantidadPeriodo = 20m,
            CantidadAnterior = 40m,
            CantidadAcumulada = 60m
        };

        var errors = PaymentCertificateValidation.ValidateLine(line);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateLine_EmptyDescription_ReturnsError()
    {
        var line = new PaymentCertificateLine { Description = string.Empty };
        var errors = PaymentCertificateValidation.ValidateLine(line);

        Assert.Contains(errors, e => e.Contains("descripción", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateLine_EmptyUnit_ReturnsError()
    {
        var line = new PaymentCertificateLine
        {
            Description = "Test",
            Unit = string.Empty
        };
        var errors = PaymentCertificateValidation.ValidateLine(line);

        Assert.Contains(errors, e => e.Contains("unidad", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateLine_CantidadContractualNegative_ReturnsError()
    {
        var line = new PaymentCertificateLine
        {
            Description = "Test",
            Unit = "UN",
            CantidadContractual = -1m
        };
        var errors = PaymentCertificateValidation.ValidateLine(line);

        Assert.Contains(errors, e => e.Contains("cantidad contractual", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateLine_PrecioUnitarioNegative_ReturnsError()
    {
        var line = new PaymentCertificateLine
        {
            Description = "Test",
            Unit = "UN",
            PrecioUnitarioContractual = -1m
        };
        var errors = PaymentCertificateValidation.ValidateLine(line);

        Assert.Contains(errors, e => e.Contains("precio unitario", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateLine_CantidadPeriodoNegative_ReturnsError()
    {
        var line = new PaymentCertificateLine
        {
            Description = "Test",
            Unit = "UN",
            CantidadPeriodo = -1m
        };
        var errors = PaymentCertificateValidation.ValidateLine(line);

        Assert.Contains(errors, e => e.Contains("cantidad del período", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateLine_CantidadAnteriorNegative_ReturnsError()
    {
        var line = new PaymentCertificateLine
        {
            Description = "Test",
            Unit = "UN",
            CantidadAnterior = -1m
        };
        var errors = PaymentCertificateValidation.ValidateLine(line);

        Assert.Contains(errors, e => e.Contains("cantidad anterior", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateLine_CantidadAcumuladaExceedsContractual_ReturnsError()
    {
        var line = new PaymentCertificateLine
        {
            Description = "Test",
            Unit = "UN",
            CantidadContractual = 100m,
            CantidadAcumulada = 200m
        };
        var errors = PaymentCertificateValidation.ValidateLine(line);

        Assert.Contains(errors, e => e.Contains("cantidad acumulada", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateAdvance_ValidAdvance_ReturnsNoErrors()
    {
        var advance = new ContractAdvance
        {
            Id = ContractAdvanceId.New(),
            ContractId = ContractId.From("CTR-001"),
            Amount = 10_000m,
            AmortizationPercentage = 10m,
            InitialBalance = 10_000m,
            CurrentBalance = 10_000m,
            TotalAmortized = 0m
        };

        var errors = PaymentCertificateValidation.ValidateAdvance(advance);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateAdvance_AmountZero_ReturnsError()
    {
        var advance = new ContractAdvance { Amount = 0m };
        var errors = PaymentCertificateValidation.ValidateAdvance(advance);

        Assert.Contains(errors, e => e.Contains("monto", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateAdvance_AmountNegative_ReturnsError()
    {
        var advance = new ContractAdvance { Amount = -100m };
        var errors = PaymentCertificateValidation.ValidateAdvance(advance);

        Assert.Contains(errors, e => e.Contains("monto", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateAdvance_AmortizationPercentageNegative_ReturnsError()
    {
        var advance = new ContractAdvance { Amount = 1000m, AmortizationPercentage = -1m };
        var errors = PaymentCertificateValidation.ValidateAdvance(advance);

        Assert.Contains(errors, e => e.Contains("porcentaje", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateAdvance_AmortizationPercentageOver100_ReturnsError()
    {
        var advance = new ContractAdvance { Amount = 1000m, AmortizationPercentage = 101m };
        var errors = PaymentCertificateValidation.ValidateAdvance(advance);

        Assert.Contains(errors, e => e.Contains("porcentaje", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateAdvance_InitialBalanceNegative_ReturnsError()
    {
        var advance = new ContractAdvance { Amount = 1000m, InitialBalance = -1m };
        var errors = PaymentCertificateValidation.ValidateAdvance(advance);

        Assert.Contains(errors, e => e.Contains("saldo inicial", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateAdvance_CurrentBalanceNegative_ReturnsError()
    {
        var advance = new ContractAdvance { Amount = 1000m, CurrentBalance = -1m };
        var errors = PaymentCertificateValidation.ValidateAdvance(advance);

        Assert.Contains(errors, e => e.Contains("saldo actual", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateAdvance_TotalAmortizedNegative_ReturnsError()
    {
        var advance = new ContractAdvance { Amount = 1000m, TotalAmortized = -1m };
        var errors = PaymentCertificateValidation.ValidateAdvance(advance);

        Assert.Contains(errors, e => e.Contains("total amortizado", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateRetention_ValidRetention_ReturnsNoErrors()
    {
        var retention = new ContractRetention
        {
            Id = ContractRetentionId.New(),
            ContractId = ContractId.From("CTR-001"),
            Percentage = 10m,
            FixedAmount = 0m,
            LimitAmount = 0m
        };

        var errors = PaymentCertificateValidation.ValidateRetention(retention);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateRetention_PercentageNegative_ReturnsError()
    {
        var retention = new ContractRetention { Percentage = -1m };
        var errors = PaymentCertificateValidation.ValidateRetention(retention);

        Assert.Contains(errors, e => e.Contains("porcentaje", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateRetention_PercentageOver100_ReturnsError()
    {
        var retention = new ContractRetention { Percentage = 101m };
        var errors = PaymentCertificateValidation.ValidateRetention(retention);

        Assert.Contains(errors, e => e.Contains("porcentaje", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateRetention_FixedAmountNegative_ReturnsError()
    {
        var retention = new ContractRetention { FixedAmount = -1m };
        var errors = PaymentCertificateValidation.ValidateRetention(retention);

        Assert.Contains(errors, e => e.Contains("monto fijo", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateRetention_LimitAmountNegative_ReturnsError()
    {
        var retention = new ContractRetention { LimitAmount = -1m };
        var errors = PaymentCertificateValidation.ValidateRetention(retention);

        Assert.Contains(errors, e => e.Contains("monto límite", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateObservation_ValidObservation_ReturnsNoErrors()
    {
        var observation = new PaymentObservation
        {
            Id = PaymentObservationId.New(),
            Author = "Revisor",
            Comment = "Falta respaldo documental"
        };

        var errors = PaymentCertificateValidation.ValidateObservation(observation);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateObservation_EmptyAuthor_ReturnsError()
    {
        var observation = new PaymentObservation { Author = string.Empty, Comment = "Test" };
        var errors = PaymentCertificateValidation.ValidateObservation(observation);

        Assert.Contains(errors, e => e.Contains("autor", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateObservation_EmptyComment_ReturnsError()
    {
        var observation = new PaymentObservation { Author = "Test", Comment = string.Empty };
        var errors = PaymentCertificateValidation.ValidateObservation(observation);

        Assert.Contains(errors, e => e.Contains("comentario", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_MultipleErrors_ReturnsAll()
    {
        var cert = CreateValidCertificate();
        cert.PeriodStart = default;
        cert.PeriodEnd = default;
        cert.Presenter = string.Empty;

        var errors = PaymentCertificateValidation.Validate(cert);

        Assert.Contains(errors, e => e.Contains("inicio", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("fin", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("presentador", StringComparison.OrdinalIgnoreCase));
    }
}
