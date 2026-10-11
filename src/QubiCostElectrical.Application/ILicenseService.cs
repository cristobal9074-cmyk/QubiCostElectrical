#nullable enable
using QubiCostElectrical;

namespace QubiCostElectrical;

public interface ILicenseService
{
    LicenseStatus Status { get; }

    bool IsReadOnly { get; }

    LicenseInfo? CurrentLicense { get; }

    string LicenseeCompanyName { get; }

    Result<LicenseInfo, LicenseError> Activate(string qclicFilePath);
}
