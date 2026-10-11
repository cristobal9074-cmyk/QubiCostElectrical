#nullable enable
namespace QubiCostElectrical;

public enum LicenseError
{
    FileNotFound,
    CorruptFile,
    InvalidSignature,
    InvalidPayload,
    IoError
}
