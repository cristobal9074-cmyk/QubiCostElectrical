using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public partial class CubicacionScView : UserControl
{
    // Catálogo maestro por ComboBox (no usar Tag: Tag lleva el nombre de propiedad a confirmar).
    private static readonly ConditionalWeakTable<ComboBox, List<string>> ComboMasters = new();
    // Escape cancela el commit en LostFocus sin pisar el Tag (propiedad destino).
    private readonly HashSet<ComboBox> _cancelledFilterCombos = new();

    private CubicacionScViewModel Vm => (CubicacionScViewModel)DataContext;
}
