using System;
using System.Collections.Generic;
using System.IO;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using QubiCostElectrical.Wpf.Auth;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Infrastructure.Diagnostics;

namespace QubiCostElectrical.Wpf.Features.Preferences;

public sealed partial class PreferencesViewModel : ViewModelBase
{
    private readonly WpfAppHost host;
    private UserPreferences _saved;
    private UserPreferences _draft = new();
    private BitmapImage? _logoImageSource;
    private readonly ObservableCollection<AuthUserSummary> _users = new();
    private readonly List<AuthUserSummary> _allUsers = new();
    private readonly ObservableCollection<string> _userAuditEntries = new();
}
