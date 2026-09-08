using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace QubiCostElectrical.Wpf.Infrastructure.Security;

/// <summary>
/// Provee ejecución y apertura segura de archivos, carpetas y enlaces web,
/// bloqueando extensiones ejecutables maliciosas y esquemas de URI no permitidos.
/// </summary>
public static class SafeProcessLauncher
{
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".ps1", ".psm1", ".psd1", ".vbs", ".vbe",
        ".js", ".jse", ".wsf", ".wsh", ".msc", ".scr", ".pif", ".com",
        ".hta", ".cpl", ".msi", ".msp", ".mst", ".reg", ".dll", ".sys",
        ".jar", ".gadget", ".iso", ".img", ".lnk", ".inf", ".scf"
    };

    private static readonly HashSet<string> AllowedUriSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        Uri.UriSchemeHttp,
        Uri.UriSchemeHttps,
        "mailto"
    };

    /// <summary>
    /// Gancho opcional para pruebas unitarias que evita invocar el shell real del sistema operativo.
    /// </summary>
    public static Func<ProcessStartInfo, Process?>? ProcessStarter { get; set; }

    /// <summary>
    /// Abre de forma segura una URL web tras verificar que el esquema sea HTTP, HTTPS o mailto.
    /// </summary>
    public static bool TryOpenUrl(string? rawUrl, out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            errorMessage = "La dirección URL está vacía.";
            return false;
        }

        var normalized = rawUrl.Trim();

        // Si contiene un scheme explícito (ej. "ms-msdt:", "file:", "powershell:", "http:", "https:", "mailto:")
        int colonIdx = normalized.IndexOf(':');
        int slashIdx = normalized.IndexOfAny(new[] { '/', '\\' });

        if (colonIdx > 0 && (slashIdx < 0 || colonIdx < slashIdx))
        {
            string scheme = normalized[..colonIdx];
            if (!AllowedUriSchemes.Contains(scheme))
            {
                errorMessage = $"El protocolo '{scheme}' no está permitido por razones de seguridad.";
                return false;
            }
        }
        else
        {
            // No tiene scheme explícito (ej. "www.ejemplo.cl" o "ejemplo.cl/ruta")
            normalized = "https://" + normalized;
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) || !AllowedUriSchemes.Contains(uri.Scheme))
        {
            errorMessage = $"La dirección '{rawUrl}' no es una URL permitida.";
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true };
            if (ProcessStarter != null)
            {
                ProcessStarter(psi);
            }
            else
            {
                Process.Start(psi);
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SafeProcessLauncher] Falló al abrir URL '{uri.AbsoluteUri}': {ex.Message}");
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Abre un archivo con su visor predeterminado tras validar su existencia y que su extensión no sea ejecutable/script.
    /// </summary>
    public static bool TryOpenFile(string? filePath, out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            errorMessage = "La ruta del archivo está vacía.";
            return false;
        }

        if (!File.Exists(filePath))
        {
            errorMessage = $"El archivo no existe: '{filePath}'.";
            return false;
        }

        var ext = Path.GetExtension(filePath);
        if (BlockedExtensions.Contains(ext))
        {
            errorMessage = $"La apertura directa de archivos con extensión '{ext}' está bloqueada por seguridad.";
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(filePath);
            var psi = new ProcessStartInfo(fullPath) { UseShellExecute = true };
            if (ProcessStarter != null)
            {
                ProcessStarter(psi);
            }
            else
            {
                Process.Start(psi);
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SafeProcessLauncher] Falló al abrir archivo '{filePath}': {ex.Message}");
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Abre una carpeta en el explorador del sistema tras validar su existencia.
    /// </summary>
    public static bool TryOpenFolder(string? folderPath, out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            errorMessage = "La ruta del directorio está vacía.";
            return false;
        }

        if (!Directory.Exists(folderPath))
        {
            errorMessage = $"El directorio no existe: '{folderPath}'.";
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(folderPath);
            var psi = new ProcessStartInfo(fullPath) { UseShellExecute = true };
            if (ProcessStarter != null)
            {
                ProcessStarter(psi);
            }
            else
            {
                Process.Start(psi);
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SafeProcessLauncher] Falló al abrir carpeta '{folderPath}': {ex.Message}");
            errorMessage = ex.Message;
            return false;
        }
    }
}
