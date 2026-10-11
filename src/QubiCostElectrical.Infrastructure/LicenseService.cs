#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
using QubiCostElectrical;

namespace QubiCostElectrical.Infrastructure;

public sealed class LicenseService : ILicenseService
{
    public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(14);

    private readonly string _publicKeyXml;

    private readonly string _licenseFile;

    private readonly object _gate = new object();

    private LicenseInfo? _cached;

    public LicenseStatus Status
    {
        get
        {
            lock (_gate)
            {
                return EvaluateStatus();
            }
        }
    }

    public bool IsReadOnly
    {
        get
        {
            LicenseStatus status = Status;
            if ((uint)(status - 2) <= 2u)
            {
                return true;
            }
            return false;
        }
    }

    public LicenseInfo? CurrentLicense
    {
        get
        {
            lock (_gate)
            {
                return Evaluate();
            }
        }
    }

    public string LicenseeCompanyName
    {
        get
        {
            lock (_gate)
            {
                return Evaluate()?.Company ?? string.Empty;
            }
        }
    }

    public LicenseService()
        : this("<RSAKeyValue><Modulus>29KFdIwyA+1Cw8qvZIDliM0f5CM9cAQ4olotJj8nhLOkbJtQ0ss962NI6/R12Mg+6wu0acNN15TXXb/fpCn71+sshLFlWyRjx9udrOAkGIC1so1tAAcdNn2JGU78w85//vG8fXdoyiarZHDqzBymN1KwL2buwPQzhhgIQYIHmcja2lwarXQgAxpeAtXkoNomUoTQIFSYeyvo3bTrZwNOlLLrhsAOp6gnIZEM9zuyH0Ed1Hxsnw/683wGwXKJHpjXWE91Mx5xkx9vHBmcZYNbCNV2PhkKC7xl5pyBaYkHFWD9mYOL+WJTqYgUeP2Hov7LvMaG/zckj1H+fJXSrDC5hvoT7/fDOe1xc7yyJxfPW9kzbhYdPEZDClblQIcu8MDCsVtwO+R5+5XXm6jmbnMmz+NJj169u3DE6ifbMnCUjlHZ98scikdnX/hE6mn32VrgeJ9X45v1yMPEQH5I/PpdD4EAQO4fLWK56u4+coCjDh9ysesqX9kjUGfDqztlDg6F</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>", ProductPaths.LicenseFile())
    {
    }

    internal LicenseService(string publicKeyXml, string licenseFile)
    {
        _publicKeyXml = publicKeyXml;
        _licenseFile = licenseFile;
    }

    public Result<LicenseInfo, LicenseError> Activate(string qclicFilePath)
    {
        if (!File.Exists(qclicFilePath))
        {
            return Result<LicenseInfo, LicenseError>.Failure(LicenseError.FileNotFound);
        }
        string text;
        try
        {
            text = File.ReadAllText(qclicFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result<LicenseInfo, LicenseError>.Failure(LicenseError.IoError);
        }
        using (RSA rSA = RSA.Create())
        {
            rSA.FromXmlString(_publicKeyXml);
            if (QclicDocument.TryParse(text, rSA) == null)
            {
                return Result<LicenseInfo, LicenseError>.Failure(LicenseError.InvalidSignature);
            }
        }
        try
        {
            string? directoryName = Path.GetDirectoryName(_licenseFile);
            if (!string.IsNullOrEmpty(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }
            File.WriteAllText(_licenseFile, text);
        }
        catch (Exception ex2) when (ex2 is IOException or UnauthorizedAccessException)
        {
            return Result<LicenseInfo, LicenseError>.Failure(LicenseError.IoError);
        }
        lock (_gate)
        {
            _cached = null;
            LicenseInfo? evaluated = Evaluate();
            return evaluated == null
                ? Result<LicenseInfo, LicenseError>.Failure(LicenseError.CorruptFile)
                : Result<LicenseInfo, LicenseError>.Success(evaluated);
        }
    }

    private LicenseInfo? Evaluate()
    {
        if (_cached != null)
        {
            return _cached;
        }
        if (!File.Exists(_licenseFile))
        {
            return null;
        }
        string text;
        try
        {
            text = File.ReadAllText(_licenseFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
        using RSA rSA = RSA.Create();
        rSA.FromXmlString(_publicKeyXml);
        QclicPayload? qclicPayload = QclicDocument.TryParse(text, rSA);
        if (qclicPayload == null)
        {
            return null;
        }
        LicenseStatus status = ComputeStatus(qclicPayload.ExpiresAt, AppClock.Now);
        _cached = new LicenseInfo(qclicPayload.Company, qclicPayload.Rut, qclicPayload.IssuedAt, qclicPayload.ExpiresAt, status);
        return _cached;
    }

    private LicenseStatus EvaluateStatus()
    {
        LicenseInfo? licenseInfo = Evaluate();
        if (licenseInfo == null)
        {
            if (!File.Exists(_licenseFile))
            {
                return LicenseStatus.NotActivated;
            }
            return LicenseStatus.Invalid;
        }
        return licenseInfo.Status;
    }

    internal static LicenseStatus ComputeStatus(DateTime expiresAt, DateTime now)
    {
        if (now <= expiresAt)
        {
            return LicenseStatus.Valid;
        }
        if (now <= expiresAt.Add(GracePeriod))
        {
            return LicenseStatus.GracePeriod;
        }
        return LicenseStatus.Expired;
    }
}
