#nullable enable
using System;
using System.Security.Cryptography;

namespace QubiCostElectrical.Infrastructure;

public static class QclicGenerator
{
    public static string Generate(string company, string rut, DateTime issuedAt, DateTime expiresAt, string privateKeyXml)
    {
        using RSA rSA = RSA.Create();
        rSA.FromXmlString(privateKeyXml);
        return QclicDocument.Serialize(new QclicPayload(company, rut, issuedAt, expiresAt), rSA);
    }
}
