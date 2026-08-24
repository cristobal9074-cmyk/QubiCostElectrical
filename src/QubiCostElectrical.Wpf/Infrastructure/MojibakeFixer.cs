using System;
using System.Text;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Repara, solo para mostrar (nunca persiste el cambio), texto guardado antes de que la
/// capa de datos actual existiera y que quedó con el clásico mojibake UTF-8 leído como
/// Windows-1252 (ej.: "23.000m²" guardado como "23.000mÂ²"). El código de lectura/escritura
/// actual ya es UTF-8 correcto de punta a punta; esto solo corrige datos históricos ya
/// dañados en archivos .qce antiguos, sin reescribirlos.
/// </summary>
public static class MojibakeFixer
{
    public static string Fix(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        if (value.IndexOf('Ã') < 0 && value.IndexOf('Â') < 0)
        {
            return value;
        }

        // Latin1 coincide con Windows-1252 en el rango 0xA0-0xFF (donde caen "Â"/"Ã" y las
        // letras acentuadas afectadas); a diferencia de GetEncoding(1252), no requiere
        // registrar System.Text.Encoding.CodePages en .NET moderno.
        string repaired;
        try
        {
            var bytes = Encoding.Latin1.GetBytes(value);
            repaired = Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return value;
        }

        if (repaired == value || repaired.IndexOf('�') >= 0)
        {
            return value;
        }

        return repaired;
    }
}
