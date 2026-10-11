using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Auth;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Features.Apu;
using System.Windows.Media;
using QubiCostElectrical.Wpf.Features.CommandPalette;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Features.Itemizado;

namespace QubiCostElectrical.Wpf.Shell;

public sealed partial class ShellViewModel : ViewModelBase, IShellNavigationHost, IDisposable
{
    public BudgetProjectSession Session => session;

    public sealed class QuickSearchItemVm
    {
        public QuickSearchItemVm(
            string title,
            string subtitle,
            string shortcut,
            Func<Task> executeAsync,
            string categoryLabel = "General",
            Brush? categoryBadgeBrush = null,
            Brush? categoryBadgeForeground = null,
            string icon = "\uE71D",
            bool isHeroAction = false,
            string heroBadge = "")
        {
            Title = title;
            Subtitle = subtitle;
            Shortcut = shortcut;
            ExecuteAsync = executeAsync;
            CategoryLabel = categoryLabel;
            CategoryBadgeBrush = categoryBadgeBrush;
            CategoryBadgeForeground = categoryBadgeForeground;
            Icon = icon;
            IsHeroAction = isHeroAction;
            HeroBadge = heroBadge;
        }

        public QuickSearchItemVm(CommandPaletteItemVm item)
            : this(
                item.Title,
                item.Subtitle,
                item.Shortcut,
                item.ExecuteAsync,
                item.CategoryLabel,
                item.CategoryBadgeBrush,
                item.CategoryBadgeForeground,
                item.Icon,
                item.IsHeroAction,
                item.HeroBadge)
        {
        }

        public string Title { get; }
        public string Subtitle { get; }
        public string Shortcut { get; }
        public Func<Task> ExecuteAsync { get; }
        public string CategoryLabel { get; }
        public Brush? CategoryBadgeBrush { get; }
        public Brush? CategoryBadgeForeground { get; }
        public string Icon { get; }
        public bool IsHeroAction { get; }
        public string HeroBadge { get; }
    }
}
