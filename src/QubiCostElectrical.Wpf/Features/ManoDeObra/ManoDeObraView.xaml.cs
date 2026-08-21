using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.ManoDeObra;

public partial class ManoDeObraView : UserControl
{
    private static readonly FontFamily MonoFont = new("Consolas");
    private static readonly FontFamily UiFont = new("Segoe UI");

    private static readonly Color Green = Color.FromRgb(0x15, 0x80, 0x3D);
    private static readonly Color Green2 = Color.FromRgb(0x16, 0xA3, 0x4A);
    private static readonly Color Amber = Color.FromRgb(0x9A, 0x6A, 0x10);
    private static readonly Color Blue = Color.FromRgb(0x25, 0x63, 0xB0);
    private static readonly Color Blue2 = Color.FromRgb(0x3B, 0x82, 0xF6);
    private static readonly Color Purple = Color.FromRgb(0x6B, 0x4F, 0xA8);
    private static readonly Color Ink = Color.FromRgb(0x10, 0x1A, 0x26);
    private static readonly Color Muted = Color.FromRgb(0x7B, 0x87, 0x98);
    private static readonly Color BlueFaint = Color.FromRgb(0xED, 0xF3, 0xFA);
    private static readonly Color PurpleSoft = Color.FromRgb(0xEF, 0xEA, 0xFA);
    private static readonly Color Surface2 = Color.FromRgb(0xF7, 0xF9, 0xFB);
    private static readonly Color Line = Color.FromRgb(0xE2, 0xE8, 0xF0);
    private static readonly Color Line2 = Color.FromRgb(0xED, 0xF0, 0xF5);
    private static readonly Color Navy2 = Color.FromRgb(0x14, 0x26, 0x3C);
    private static readonly Color PeakBg = Color.FromArgb(0x12, 0x15, 0x80, 0x3D);

    private const int ColCargo = 168;
    private const int ColDotPico = 45;
    private const int ColBase = 88;
    private const int ColLeyes = 78;
    private const int ColTotalMes = 96;
    private const int ColMonth = 40;
    private const int ColFinal = 92;
    private const int RowH = 30;
    private const int CellSz = 10;
}
