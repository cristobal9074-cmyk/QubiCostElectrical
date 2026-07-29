namespace QubiCostElectrical.Wpf.Features.Preferences;

public enum UpdateState
{
    Idle,
    Checking,
    UpToDate,
    UpdateAvailable,
    Downloading,
    ReadyToInstall,
    Applying,
    Error,
}
