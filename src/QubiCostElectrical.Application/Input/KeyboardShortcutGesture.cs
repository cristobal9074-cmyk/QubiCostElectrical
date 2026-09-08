using System.Globalization;
using System.Text;

namespace QubiCostElectrical;

/// <summary>Representa un atajo de teclado independiente de WPF (Alt+L, Ctrl+Shift+Up).</summary>
public sealed class KeyboardShortcutGesture : IEquatable<KeyboardShortcutGesture>
{
    public bool Alt { get; init; }
    public bool Ctrl { get; init; }
    public bool Shift { get; init; }
    public string Key { get; init; } = "";

    public static bool TryParse(string? text, out KeyboardShortcutGesture? gesture)
    {
        gesture = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        bool alt = false, ctrl = false, shift = false;
        string? keyToken = null;

        foreach (var rawPart in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var part = rawPart.Trim();
            if (part.Length == 0) continue;

            switch (part.ToUpperInvariant())
            {
                case "ALT":
                    alt = true;
                    continue;
                case "CTRL":
                case "CONTROL":
                    ctrl = true;
                    continue;
                case "SHIFT":
                    shift = true;
                    continue;
            }

            if (keyToken != null)
                return false;

            keyToken = NormalizeKeyToken(part);
            if (keyToken == null)
                return false;
        }

        if (keyToken == null)
            return false;

        gesture = new KeyboardShortcutGesture
        {
            Alt = alt,
            Ctrl = ctrl,
            Shift = shift,
            Key = keyToken
        };
        return true;
    }

    public string ToStorageString()
    {
        var parts = new List<string>(4);
        if (Ctrl) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        parts.Add(Key);
        return string.Join('+', parts);
    }

    public string ToDisplayString()
    {
        var parts = new List<string>(4);
        if (Ctrl) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        parts.Add(DisplayKey(Key));
        return string.Join(" + ", parts);
    }

    public bool Equals(KeyboardShortcutGesture? other) =>
        other != null
        && Alt == other.Alt
        && Ctrl == other.Ctrl
        && Shift == other.Shift
        && string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => obj is KeyboardShortcutGesture g && Equals(g);

    public override int GetHashCode() =>
        HashCode.Combine(Alt, Ctrl, Shift, Key.ToUpperInvariant());

    private static string? NormalizeKeyToken(string token)
    {
        if (token.Length == 1 && char.IsLetterOrDigit(token[0]))
            return token.ToUpperInvariant();

        var normalized = token switch
        {
            "↑" or "Arriba" => "Up",
            "↓" or "Abajo" => "Down",
            "←" or "Izquierda" => "Left",
            "→" or "Derecha" => "Right",
            _ => token
        };

        if (normalized.Length == 0)
            return null;

        if (normalized.Length == 1)
            return normalized.ToUpperInvariant();

        return char.ToUpper(normalized[0], CultureInfo.InvariantCulture) + normalized[1..];
    }

    private static string DisplayKey(string key) => key switch
    {
        "Up" => "↑",
        "Down" => "↓",
        "Left" => "←",
        "Right" => "→",
        _ => key
    };
}
