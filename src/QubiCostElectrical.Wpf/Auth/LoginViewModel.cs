using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Auth;

public class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private string _email = "";
    private string _pin = "";
    private bool _rememberMe;
    private string? _errorMessage;
    private bool _isBusy;
    private bool _isFirstUser;
    private string? _displayName;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
        LoginCommand = new RelayCommand(async () => await LoginAsync(), () => !IsBusy);

        var prefs = UserPreferencesStore.Load();
        _email = prefs.LastUserEmail;
        ProductVersion = $"v{ProductVersionInfo.ProductVersion}";

        CheckFirstUser();
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
        set
        {
            if (SetProperty(ref _isFirstUser, value))
                OnPropertyChanged(nameof(ShowFirstUserWizard));
        }
    }

    public bool ShowFirstUserWizard => IsFirstUser;

    public string? DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public ICommand LoginCommand { get; }

    public event System.EventHandler? LoginSucceeded;

    private void CheckFirstUser()
    {
        IsFirstUser = _authService.IsFirstUser();
        if (IsFirstUser)
            ErrorMessage = "Bienvenido. Crea tu cuenta de administrador para comenzar.";
    }

    private async Task LoginAsync()
    {
        if (IsBusy) return;

        var email = Email?.Trim().ToLowerInvariant() ?? "";
        var pin = Pin ?? "";
        var name = DisplayName?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(email))
        {
            ErrorMessage = "Ingresa tu correo electrónico.";
            return;
        }

        if (string.IsNullOrWhiteSpace(pin))
        {
            ErrorMessage = "Ingresa tu PIN.";
            return;
        }

        if (IsFirstUser && string.IsNullOrWhiteSpace(name))
        {
            ErrorMessage = "Ingresa tu nombre.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            Result<UserSession, AuthError> result;

            if (IsFirstUser)
            {
                if (pin.Length < 4 || pin.Length > 6)
                {
                    ErrorMessage = "El PIN debe tener entre 4 y 6 dígitos.";
                    return;
                }
                result = await _authService.CreateFirstUserAsync(email, pin, name);
            }
            else
            {
                result = await _authService.LoginAsync(email, pin, RememberMe);
            }

            if (result.IsSuccess)
            {
                SessionManager.SetSession(result.Value!);

                var prefs = UserPreferencesStore.Load();
                prefs.LastUserEmail = email;
                UserPreferencesStore.Save(prefs);

                LoginSucceeded?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ErrorMessage = result.Error switch
                {
                    AuthError.UserNotFound => "No existe una cuenta con ese correo.",
                    AuthError.InvalidCredentials => "PIN incorrecto. Verifica e intenta de nuevo.",
                    AuthError.AccountLocked => "Cuenta bloqueada por muchos intentos fallidos. Intenta en 15 minutos.",
                    AuthError.AccountInactive => "Esta cuenta está desactivada.",
                    AuthError.PinTooShort => "El PIN debe tener entre 4 y 6 dígitos.",
                    AuthError.TokenInvalid => "Error al crear la cuenta.",
                    _ => "Error al iniciar sesión."
                };
            }
        }
        catch
        {
            ErrorMessage = "Error de conexión. Verifica la aplicación.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
