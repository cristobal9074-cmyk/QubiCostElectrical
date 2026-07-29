using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Shell;

public interface IShellNavigationHost
{
  WpfAppHost Host { get; }
  ShellRoute CurrentRoute { get; }
  string ActiveProjectLabel { get; }
  bool RequestNavigate(ShellRoute route, bool force = false);
  bool RequestNavigate(ShellRoute route, WorkspaceNavigationContext? context, bool force = false);
  Task NewProjectAsync();
  Task OpenProjectAsync();
  Task<bool> CloseProjectAsync();
  Task OpenRecentProjectAsync(string path);
  void RemoveRecentProject(string path);
  Task SaveProjectAsync();
  Task SaveProjectAsAsync();
  void RefreshChrome();
}
