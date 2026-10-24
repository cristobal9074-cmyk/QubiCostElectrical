using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Infrastructure;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Auth;

public class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly IRemoteLicenseService _licenseService;
    private readonly IUserPreferencesStore? _preferencesStore;

    private string _email = "";
    private string _pin = "";
    private bool _rememberMe = true;
    private string? _errorMessage;
    private bool _isBusy;
    private bool _isFirstUser;
    private bool _isRegisterMode;
    private string? _displayName;

    public LoginViewModel(
        IAuthService authService,
        IRemoteLicenseService? licenseService = null,
        IUserPreferencesStore? preferencesStore = null)
    {
        _authService = authService;
        _licenseService = licenseService ?? throw new ArgumentNullException(nameof(licenseService));
        _preferencesStore = preferencesStore ?? WpfAppHost.Current?.UserPreferencesStore;

        LoginCommand = new RelayCommand(async () => await LoginAsync(), () => !IsBusy);
        ToggleModeCommand = new RelayCommand(ToggleMode);

        var prefs = _preferencesStore?.Load() ?? new UserPreferences();
        _email = prefs.LastUserEmail ?? "";
        ProductVersion = $"v{ProductVersionInfo.ProductVersion}";
    }

    public LoginViewModel(IAuthService authService, IUserPreferencesStore? preferencesStore)
        : this(authService, null, preferencesStore)
    {
    }

    public string ProductVersion { get; }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Pin
    {
        get => _pin;
        set => SetProperty(ref _pin, value);
    }

    public bool RememberMe
    {
        get => _rememberMe;
        set => SetProperty(ref _rememberMe, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ((RelayCommand)LoginCommand).RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(CanInteract));
            }
        }
    }

    public bool CanInteract => !IsBusy;

    public bool IsFirstUser
    {
        get => _isFirstUser;
        set => SetProperty(ref _isFirstUser, value);
    }

    public bool IsRegisterMode
    {
        get => _isRegisterMode;
        set => SetProperty(ref _isRegisterMode, value);
    }

    public bool IsRegistrationActive => false;

    public string ViewTitleText => "Acceso a la Plataforma";

    public string ViewDescText => "Ingresa tus credenciales autorizadas para acceder a tus presupuestos y cubicaciones.";

    public string SubmitButtonText => "Ingresar al sistema";

    public string SwitchText => "";

    public string SwitchLinkText => "";

    public bool ShowFirstUserWizard => false;

    public string? DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public ICommand LoginCommand { get; }
    public ICommand ToggleModeCommand { get; }

    public event EventHandler? LoginSucceeded;

    private void ToggleMode()
    {
        ErrorMessage = null;
    }

    private async Task LoginAsync()
    {
        if (IsBusy) return;

        var email = Email?.Trim().ToLowerInvariant() ?? "";
        var password = Pin ?? "";

        if (string.IsNullOrWhiteSpace(email))
        {
            ErrorMessage = "Ingresa tu correo electrónico.";
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "Ingresa tu contraseña.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            // 1. Validar suscripción/licencia contra la API en tiempo real
            var licenseResult = await _licenseService.VerifyLicenseAsync(email, password);

            // Caída o falta de internet: si el servidor es inalcanzable (no si rechazó las
            // credenciales), se permite validar contra las credenciales locales ya registradas
            // para no dejar al usuario fuera de su propia app.
            var isOfflineGrace = !licenseResult.IsValid
                && licenseResult.Reason is "network_error" or "timeout" or "http_error" or "unknown";

            if (!licenseResult.IsValid && !isOfflineGrace)
            {
                ErrorMessage = licenseResult.ErrorMessage ?? "Acceso denegado.";
                return;
            }

            // 2. Si la licencia remota es válida, asegurar usuario en la base de datos local
            var effectiveName = !string.IsNullOrWhiteSpace(licenseResult.DisplayName)
                ? licenseResult.DisplayName
                : email;

            if (isOfflineGrace)
            {
                // Sin servidor no se crean ni sincronizan perfiles: solo cuenta local previa.
                var localUsers = await _authService.GetUsersAsync();
                if (localUsers.All(u => !string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)))
                {
                    ErrorMessage = "Sin conexión al servidor de licencias y sin credenciales locales previas. Conéctate a internet para el primer ingreso.";
                    return;
                }
            }
            else if (_authService.IsFirstUser())
            {
                // El primer usuario del equipo queda con rol Administrador
                var firstRes = await _authService.CreateFirstUserAsync(email, password, effectiveName, allowLicenseCredential: true);
                if (!firstRes.IsSuccess)
                {
                    ErrorMessage = "Error al inicializar las credenciales locales.";
                    return;
                }
            }
            else
            {
                var allUsers = await _authService.GetUsersAsync();
                var localUser = allUsers.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));

                if (localUser == null)
                {
                    // Crear usuario local sincronizado
                    var createRes = await _authService.CreateUserAsync(email, password, effectiveName, UserRole.Estimator, null, allowLicenseCredential: true);
                    if (!createRes.IsSuccess && createRes.Error != AuthAdminError.EmailAlreadyExists)
                    {
                        ErrorMessage = "Error al sincronizar el perfil de usuario local.";
                        return;
                    }
                }
                else
                {
                    // Sincronizar contraseña local y reactivar si estaba inactiva
                    await _authService.ResetUserPinAsync(localUser.Id, password, null, allowLicenseCredential: true);
                    if (!localUser.IsActive)
                    {
                        await _authService.SetUserActiveAsync(localUser.Id, true, email);
                    }
                }
            }

            // 3. Autenticación local y creación de sesión
            var result = await _authService.LoginAsync(email, password, RememberMe);
            if (result.IsSuccess)
            {
                SessionManager.SetSession(result.Value!);

                var prefs = _preferencesStore?.Load() ?? new UserPreferences();
                prefs.LastUserEmail = email;
                _preferencesStore?.Save(prefs);

                LoginSucceeded?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ErrorMessage = result.Error switch
                {
                    AuthError.AccountLocked => "Cuenta bloqueada temporalmente por intentos fallidos.",
                    AuthError.AccountInactive => "Cuenta inactiva en este equipo.",
                    AuthError.InvalidCredentials when isOfflineGrace => "Contraseña local incorrecta (verificación sin conexión).",
                    _ => "Error al iniciar sesión local."
                };
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error inesperado al iniciar sesión: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
