using System;
using System.Threading.Tasks;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Features.CommandPalette;

public sealed class CommandPaletteItemVm
{
    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static readonly SolidColorBrush PurpleBg = CreateFrozenBrush(0xEF, 0xEA, 0xFA);
    private static readonly SolidColorBrush PurpleFg = CreateFrozenBrush(0x6B, 0x4F, 0xA8);
    private static readonly SolidColorBrush BlueBg = CreateFrozenBrush(0xEA, 0xF1, 0xFF);
    private static readonly SolidColorBrush BlueFg = CreateFrozenBrush(0x25, 0x63, 0xB0);
    private static readonly SolidColorBrush GreenBg = CreateFrozenBrush(0xEA, 0xF8, 0xEF);
    private static readonly SolidColorBrush GreenFg = CreateFrozenBrush(0x15, 0x80, 0x3D);
    private static readonly SolidColorBrush AmberBg = CreateFrozenBrush(0xFB, 0xF1, 0xDD);
    private static readonly SolidColorBrush AmberFg = CreateFrozenBrush(0x9A, 0x6A, 0x10);
    private static readonly SolidColorBrush CyanBg = CreateFrozenBrush(0xE8, 0xEF, 0xFA);
    private static readonly SolidColorBrush CyanFg = CreateFrozenBrush(0x08, 0x91, 0xB2);
    private static readonly SolidColorBrush IndigoBg = CreateFrozenBrush(0xED, 0xE9, 0xFE);
    private static readonly SolidColorBrush IndigoFg = CreateFrozenBrush(0x6D, 0x28, 0xD9);

    private static Brush Res(string key, Brush fallback)
    {
        try
        {
            if (System.Windows.Application.Current?.TryFindResource(key) is Brush brush)
                return brush;
        }
        catch
        {
            // Unit test / headless fallback
        }
        return fallback;
    }

    public CommandPaletteItemVm(
        string title,
        string subtitle,
        string shortcut,
        CommandPaletteCategory category,
        Func<Task> executeAsync,
        string icon = "\uE71D",
        bool isHeroAction = false,
        string heroBadge = "",
        int score = 0)
    {
        Title = title;
        Subtitle = subtitle;
        Shortcut = shortcut;
        Category = category;
        ExecuteAsync = executeAsync;
        Icon = icon;
        IsHeroAction = isHeroAction;
        HeroBadge = heroBadge;
        Score = score;
    }

    public string Title { get; }
    public string Subtitle { get; }
    public string Shortcut { get; }
    public CommandPaletteCategory Category { get; }
    public Func<Task> ExecuteAsync { get; }
    public string Icon { get; }
    public bool IsHeroAction { get; }
    public string HeroBadge { get; }
    public int Score { get; set; }

    public string CategoryLabel => Category switch
    {
        CommandPaletteCategory.Actions => "⚡ Acción",
        CommandPaletteCategory.Navigation => "Módulo",
        CommandPaletteCategory.Partidas => "Partida",
        CommandPaletteCategory.Cubicacion => "Cubicación",
        CommandPaletteCategory.Apu => "APU",
        CommandPaletteCategory.TablerosSc => "Tablero SC",
        CommandPaletteCategory.Materiales => "Banco Insumos",
        _ => "General"
    };

    public Brush CategoryBadgeBrush => Category switch
    {
        CommandPaletteCategory.Actions => Res("Brush.PurpleSoft", PurpleBg),
        CommandPaletteCategory.Navigation => Res("Brush.BlueSoft", BlueBg),
        CommandPaletteCategory.Partidas => Res("Brush.GreenSoft", GreenBg),
        CommandPaletteCategory.Cubicacion => Res("Brush.AmberSoft", AmberBg),
        CommandPaletteCategory.Apu => Res("Brush.InfoBg", CyanBg),
        CommandPaletteCategory.TablerosSc => Res("Brush.BlueSoft", BlueBg),
        CommandPaletteCategory.Materiales => Res("Brush.PurpleSoft", IndigoBg),
        _ => Res("Brush.Surface2", BlueBg)
    };

    public Brush CategoryBadgeForeground => Category switch
    {
        CommandPaletteCategory.Actions => Res("Brush.Purple", PurpleFg),
        CommandPaletteCategory.Navigation => Res("Brush.BlueDark", BlueFg),
        CommandPaletteCategory.Partidas => Res("Brush.SuccessDark", GreenFg),
        CommandPaletteCategory.Cubicacion => Res("Brush.AmberRisk", AmberFg),
        CommandPaletteCategory.Apu => Res("Brush.Info", CyanFg),
        CommandPaletteCategory.TablerosSc => Res("Brush.BlueDark", BlueFg),
        CommandPaletteCategory.Materiales => Res("Brush.Purple", IndigoFg),
        _ => Res("Brush.Ink3", BlueFg)
    };
}
