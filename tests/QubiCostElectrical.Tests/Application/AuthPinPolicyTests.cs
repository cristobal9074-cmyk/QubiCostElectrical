using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class AuthPinPolicyTests
{
    [Theory]
    [InlineData("1234", false)]
    [InlineData("4321", false)]
    [InlineData("1111", false)]
    [InlineData("12a4", false)]
    [InlineData("2580", true)]
    [InlineData("9071", true)]
    public void IsPinPolicyCompliant_validates_expected_patterns(string pin, bool expected)
    {
        Assert.Equal(expected, AuthService.IsPinPolicyCompliant(pin));
    }

    [Fact]
    public void PinSecurityPolicy_respects_custom_length_and_sequence_flags()
    {
        var relaxed = new PinSecurityPolicy
        {
            MinLength = 8,
            MaxLength = 8,
            BlockSequences = false,
            BlockRepeats = true
        };

        Assert.True(PinSecurityPolicy.IsCompliant("12345678", relaxed));
        Assert.False(PinSecurityPolicy.IsCompliant("11111111", relaxed));
        Assert.False(PinSecurityPolicy.IsCompliant("2580", relaxed));
    }
}
