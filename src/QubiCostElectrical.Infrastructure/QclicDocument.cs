#nullable enable
using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QubiCostElectrical.Infrastructure;

public static class QclicDocument
{
	public const string Header = "QCLIC1";

	private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false
	};

	public static string Serialize(QclicPayload payload, RSA privateKey)
	{
		string s = JsonSerializer.Serialize(payload, JsonOpts);
		string text = Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
		byte[] inArray = privateKey.SignData(Encoding.UTF8.GetBytes(text), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		return string.Join('\n', "QCLIC1", text, Convert.ToBase64String(inArray));
	}

	public static QclicPayload? TryParse(string text, RSA publicKey)
	{
		string[] array = text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
		if (array.Length != 3 || array[0] != "QCLIC1")
		{
			return null;
		}
		if (!publicKey.VerifyData(Encoding.UTF8.GetBytes(array[1]), Convert.FromBase64String(array[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
		{
			return null;
		}
		try
		{
			return JsonSerializer.Deserialize<QclicPayload>(Encoding.UTF8.GetString(Convert.FromBase64String(array[1])), JsonOpts);
		}
		catch (JsonException)
		{
			return null;
		}
	}
}
