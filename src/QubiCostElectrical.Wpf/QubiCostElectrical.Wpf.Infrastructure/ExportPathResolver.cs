#nullable enable
using System;
using System.IO;

namespace QubiCostElectrical.Wpf.Infrastructure;

public static class ExportPathResolver
{
	public static string Create(string prefix, string extension, DateTime? timestamp = null)
	{
		string path = $"{prefix}-{timestamp ?? DateTime.Now:yyyyMMdd-HHmmssfff}.{extension}";
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "QubiCostElectrical", "Exports");
		try
		{
			Directory.CreateDirectory(text);
			string text2 = Path.Combine(text, path);
			using (File.Open(text2, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
			{
			}
			File.Delete(text2);
			return text2;
		}
		catch (UnauthorizedAccessException)
		{
			string text3 = Path.Combine(Path.GetTempPath(), "QubiCostElectrical", "Exports");
			Directory.CreateDirectory(text3);
			return Path.Combine(text3, path);
		}
	}
}
