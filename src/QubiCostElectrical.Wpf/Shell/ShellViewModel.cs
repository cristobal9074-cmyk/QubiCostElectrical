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
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Features.Itemizado;

namespace QubiCostElectrical.Wpf.Shell;

public sealed partial class ShellViewModel : ViewModelBase, IShellNavigationHost, IDisposable
{
    public sealed class QuickSearchItemVm
    {
        public QuickSearchItemVm(string title, string subtitle, string shortcut, Func<Task> executeAsync)
        {
            Title = title;
            Subtitle = subtitle;
            Shortcut = shortcut;
            ExecuteAsync = executeAsync;
        }

        public string Title { get; }
        public string Subtitle { get; }
        public string Shortcut { get; }
        public Func<Task> ExecuteAsync { get; }
    }
}
