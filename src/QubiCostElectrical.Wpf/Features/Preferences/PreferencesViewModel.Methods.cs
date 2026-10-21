using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Auth;
using QubiCostElectrical.Wpf.Features.Cubicacion;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Infrastructure.Diagnostics;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Preferences;

public sealed partial class PreferencesViewModel
{
    public PreferencesViewModel(WpfAppHost host)
        : this(host.Session, host.ProjectWorkspace, host.Auth, host.Permissions, host.FileOpener, host.UserPreferencesStore) { }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public PreferencesViewModel(BudgetProjectSession session, IProjectWorkspaceService workspace, IAuthService auth, IPermissionService permissions, IFileOpenService fileOpener, IUserPreferencesStore? preferencesStore = null)
        : base(session, workspace)
    {
        this.auth = auth;
        this.permissions = permissions;
        this.fileOpener = fileOpener ?? WpfAppHost.Current?.FileOpener ?? new WpfFileOpenService();
        this.preferencesStore = preferencesStore ?? WpfAppHost.Current?.UserPreferencesStore ?? new UserPreferencesStore();
        _saved = this.preferencesStore.Load();
        _draft = Clone(_saved);
        LoadFromDraft();
        Updates = new UpdatesViewModel(
            service: new VelopackUpdateService(ProductVersionInfo.GitHubReleasesUrl),
            confirmInstallFn: () => WpfDialogService.Confirm(
                "Instalar y reiniciar",
                "La aplicación se cerrará y reiniciará para instalar la actualización.\n¿Continuar?"),
            hasUnsavedProjectFn: () =>
            {
                try { return Session.IsDirty == true; }
                catch { return false; }
            });
        InitializeNavigation();
        SyncWorkspaceGate();
        _ = FinishInitialLoadAsync();
    }

    /// <summary>Preferencias son globales: siempre hay contenido (sin empty por proyecto).</summary>
    protected override void SyncWorkspaceGate()
    {
        ShowEmptyState = false;
        CanEdit = permissions.CanManageAppSettings;
        OnWorkspaceGateChanged();
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        SyncWorkspaceGate();
    }

    private async Task FinishInitialLoadAsync()
    {
        IsLoading = true;
        try
        {
            await RefreshUsersAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [ObservableProperty] private string _defaultProjectFolder = "";
    [ObservableProperty] private string _priceBankDatabasePath = "";
    [ObservableProperty] private string _priceBankPathWarning = "";

    partial void OnPriceBankDatabasePathChanged(string value)
    {
        HasPendingChanges = true;
        PriceBankPathWarning = ProductPaths.IsLikelyCloudSyncPath(value)
            ? "Esta ruta está dentro de una carpeta sincronizada por la nube (OneDrive/Dropbox/Drive/iCloud). SQLite no soporta escritura concurrente: el archivo puede sincronizarse a medio guardar y corromperse. Use una ruta local o un share de red (UNC)."
            : "";
    }
    [ObservableProperty] private string _companyName = "";
    [ObservableProperty] private string _rut = "";
    [ObservableProperty] private string _address = "";
    [ObservableProperty] private string _phone = "";
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _website = "";
    [ObservableProperty] private string _professionalName = "";
    [ObservableProperty] private string _professionalRut = "";
    [ObservableProperty] private string _professionalRole = "";
    [ObservableProperty] private string _professionalRegistroSec = "";
    [ObservableProperty] private string _professionalSignatureImagePath = "";
    [ObservableProperty] private int _professionalSignatureWidthMm = 46;
    [ObservableProperty] private int _professionalSignatureHeightMm = 16;

    [ObservableProperty]
    private string _logoPath = "";

    partial void OnLogoPathChanged(string value)
    {
        HasPendingChanges = true;
        _logoImageSource = LoadImageSafe(value);
        OnPropertyChanged(nameof(LogoImageSource));
    }

    public BitmapImage? LogoImageSource => _logoImageSource;

    [ObservableProperty] private bool _hasPendingChanges;
    [ObservableProperty] private string? _validationError;
    [ObservableProperty] private string _saveStatus = "";
    [ObservableProperty] private string _newUserEmail = "";
    [ObservableProperty] private string _newUserDisplayName = "";
    [ObservableProperty] private string _newUserPin = "";
    [ObservableProperty] private UserRole _newUserRole = UserRole.Estimator;
    [ObservableProperty] private string _userAdminStatus = "";
    [ObservableProperty] private string _userSearchText = "";
    [ObservableProperty] private string _selectedUserRoleFilter = "Todos";
    [ObservableProperty] private string _selectedUserActiveFilter = "Todos";
    [ObservableProperty] private AuthUserSummary? _selectedUser;
    [ObservableProperty] private int _pinMinLength = 4;
    [ObservableProperty] private int _pinMaxLength = 6;
    [ObservableProperty] private bool _pinBlockSequences = true;
    [ObservableProperty] private bool _pinBlockRepeats = true;

    [ObservableProperty]
    private bool _isDarkMode;

    [ObservableProperty]
    private bool _autosaveEnabled;

    [ObservableProperty]
    private string _defaultGastosGeneralesPctText = "14,0";

    [ObservableProperty]
    private string _defaultUtilidadPctText = "18,0";

    partial void OnAutosaveEnabledChanged(bool value) => HasPendingChanges = true;
    partial void OnDefaultGastosGeneralesPctTextChanged(string value) => HasPendingChanges = true;
    partial void OnDefaultUtilidadPctTextChanged(string value) => HasPendingChanges = true;
    [ObservableProperty]
    private string _defaultMermasPctText = "0,0";

    [ObservableProperty]
    private string _defaultHerramientasPctText = "0,0";

    [ObservableProperty]
    private string _defaultMoneda = "CLP";

    public IReadOnlyList<string> AvailableCurrencies { get; } = ["CLP", "UF", "USD", "EUR"];

    partial void OnDefaultMermasPctTextChanged(string value) => HasPendingChanges = true;
    partial void OnDefaultHerramientasPctTextChanged(string value) => HasPendingChanges = true;
    partial void OnDefaultMonedaChanged(string value) => HasPendingChanges = true;

    partial void OnIsDarkModeChanged(bool oldValue, bool newValue)
    {
        DesignSystem.ThemeManager.SetDarkMode(newValue);
        HasPendingChanges = true;
    }

    partial void OnDefaultProjectFolderChanged(string value) => HasPendingChanges = true;
    partial void OnCompanyNameChanged(string value) => HasPendingChanges = true;
    partial void OnRutChanged(string value) => HasPendingChanges = true;
    partial void OnAddressChanged(string value) => HasPendingChanges = true;
    partial void OnPhoneChanged(string value) => HasPendingChanges = true;
    partial void OnEmailChanged(string value) => HasPendingChanges = true;
    partial void OnWebsiteChanged(string value) => HasPendingChanges = true;
    partial void OnProfessionalNameChanged(string value) => HasPendingChanges = true;
    partial void OnProfessionalRutChanged(string value) => HasPendingChanges = true;
    partial void OnProfessionalRoleChanged(string value) => HasPendingChanges = true;
    partial void OnProfessionalRegistroSecChanged(string value) => HasPendingChanges = true;
    partial void OnProfessionalSignatureImagePathChanged(string value) => HasPendingChanges = true;
    partial void OnProfessionalSignatureWidthMmChanged(int value) => HasPendingChanges = true;
    partial void OnProfessionalSignatureHeightMmChanged(int value) => HasPendingChanges = true;
    partial void OnPinMinLengthChanged(int value) => HasPendingChanges = true;
    partial void OnPinMaxLengthChanged(int value) => HasPendingChanges = true;
    partial void OnPinBlockSequencesChanged(bool value) => HasPendingChanges = true;
    partial void OnPinBlockRepeatsChanged(bool value) => HasPendingChanges = true;

    public UpdatesViewModel Updates { get; }
    public IReadOnlyList<UserRole> AvailableRoles { get; } = [UserRole.Admin, UserRole.Estimator, UserRole.Viewer];
    public ObservableCollection<AuthUserSummary> Users => _users;
    public ObservableCollection<string> UserAuditEntries => _userAuditEntries;
    public IReadOnlyList<string> UserRoleFilters { get; } = ["Todos", "Admin", "Estimator", "Viewer"];
    public IReadOnlyList<string> UserActiveFilters { get; } = ["Todos", "Activos", "Inactivos"];

    public bool IsSettingsReadOnly => !permissions.CanManageAppSettings;

    public string AppVersion => ProductVersionInfo.ProductVersionLabel;
    public string LogsFolderPath => new LogConfiguration().LogsFolderPath();
    public string RecoveryFolderPath => AutoSaveService.GetAutoSaveRecoveryDir();

    public string? ValidateLogoPath(string? path) => LetterheadApplicator.ValidateImagePath(path);
    public string? ValidateSignaturePath(string? path) => LetterheadApplicator.ValidateImagePath(path);

    public async Task RefreshUsersAsync()
    {
        _allUsers.Clear();
        try
        {
            var users = await auth.GetUsersAsync();
            foreach (var user in users)
                _allUsers.Add(user);
            ApplyUserFilters();
            await RefreshAuditAsync();
        }
        catch
        {
            UserAdminStatus = "No se pudo cargar la lista de usuarios.";
        }
    }

    public async Task RefreshAuditAsync()
    {
        _userAuditEntries.Clear();
        try
        {
            var entries = await auth.GetAdminAuditAsync(25);
            foreach (var entry in entries)
            {
                _userAuditEntries.Add(
                    $"{entry.OccurredAt:dd/MM/yyyy HH:mm} · {entry.Action} · {entry.TargetEmail} · {entry.Details}");
            }
        }
        catch
        {
            _userAuditEntries.Add("No se pudo cargar la auditoría de usuarios.");
        }
    }

    public async Task CreateUserAsync()
    {
        if (IsSettingsReadOnly)
        {
            UserAdminStatus = "Solo administradores pueden gestionar usuarios.";
            return;
        }

        var result = await auth.CreateUserAsync(NewUserEmail, NewUserPin, NewUserDisplayName, NewUserRole, SessionManager.UserEmail);
        if (!result.IsSuccess)
        {
            UserAdminStatus = result.Error switch
            {
                AuthAdminError.EmailAlreadyExists => "Ya existe un usuario con ese correo.",
                AuthAdminError.InvalidInput => "Datos inválidos. Verifica correo, nombre y PIN (4-6 dígitos).",
                AuthAdminError.WeakPin => "PIN débil. Evita secuencias (1234/4321), repeticiones (1111) y usa 4-6 dígitos.",
                _ => "No se pudo crear el usuario."
            };
            return;
        }

        NewUserEmail = "";
        NewUserDisplayName = "";
        NewUserPin = "";
        NewUserRole = UserRole.Estimator;
        UserAdminStatus = "Usuario creado.";
        await RefreshUsersAsync();
    }

    public async Task ToggleUserActiveAsync(AuthUserSummary? user)
    {
        if (user == null)
            return;
        if (IsSettingsReadOnly)
        {
            UserAdminStatus = "Solo administradores pueden gestionar usuarios.";
            return;
        }

        var result = await auth.SetUserActiveAsync(user.Id, !user.IsActive, SessionManager.UserEmail);
        if (!result.IsSuccess)
        {
            UserAdminStatus = result.Error switch
            {
                AuthAdminError.CannotDeactivateSelf => "No puedes desactivar tu propia cuenta.",
                AuthAdminError.UserNotFound => "Usuario no encontrado.",
                _ => "No se pudo actualizar el estado del usuario."
            };
            return;
        }

        UserAdminStatus = result.Value!.IsActive ? "Usuario activado." : "Usuario desactivado.";
        await RefreshUsersAsync();
    }

    public async Task ChangeUserRoleAsync(AuthUserSummary? user, UserRole role)
    {
        if (user == null)
            return;
        if (IsSettingsReadOnly)
        {
            UserAdminStatus = "Solo administradores pueden gestionar usuarios.";
            return;
        }

        var result = await auth.SetUserRoleAsync(user.Id, role, SessionManager.UserEmail);
        if (!result.IsSuccess)
        {
            UserAdminStatus = "No se pudo actualizar el rol.";
            return;
        }

        UserAdminStatus = "Rol actualizado.";
        await RefreshUsersAsync();
    }

    public async Task UnlockUserAsync(AuthUserSummary? user)
    {
        if (user == null)
            return;
        if (IsSettingsReadOnly)
        {
            UserAdminStatus = "Solo administradores pueden gestionar usuarios.";
            return;
        }

        var result = await auth.UnlockUserAsync(user.Id, SessionManager.UserEmail);
        if (!result.IsSuccess)
        {
            UserAdminStatus = "No se pudo desbloquear la cuenta.";
            return;
        }

        UserAdminStatus = $"Cuenta desbloqueada: {result.Value!.DisplayName}.";
        await RefreshUsersAsync();
    }

    public async Task ResetUserPinAsync(AuthUserSummary? user, string newPin)
    {
        if (user == null)
            return;
        if (IsSettingsReadOnly)
        {
            UserAdminStatus = "Solo administradores pueden gestionar usuarios.";
            return;
        }

        var result = await auth.ResetUserPinAsync(user.Id, newPin, SessionManager.UserEmail);
        if (!result.IsSuccess)
        {
            UserAdminStatus = result.Error == AuthAdminError.InvalidInput
                ? "PIN inválido. Debe tener 4-6 dígitos."
                : result.Error == AuthAdminError.WeakPin
                    ? "PIN débil. Evita secuencias (1234/4321), repeticiones (1111) y usa 4-6 dígitos."
                    : "No se pudo resetear el PIN.";
            return;
        }

        UserAdminStatus = "PIN restablecido.";
        await RefreshUsersAsync();
    }

    public bool ApplyChanges()
    {
        if (IsSettingsReadOnly)
        {
            ValidationError = "Solo administradores pueden cambiar la configuración global de la aplicación.";
            SaveStatus = "";
            return false;
        }
        ValidationError = Validate();
        if (ValidationError != null) { SaveStatus = ""; return false; }
        CommitDraftToSaved();
        preferencesStore.Save(_saved);
        CubicacionShortcutRouting.InvalidateCache();
        ShellShortcutRouting.InvalidateCache();
        HasPendingChanges = false;
        SaveStatus = "Preferencias guardadas correctamente.";
        AfterPreferencesSaved();
        return true;
    }

    public void TrySave()
    {
        if (IsSettingsReadOnly)
        {
            ValidationError = "Solo administradores pueden cambiar la configuración global de la aplicación.";
            return;
        }
        ValidationError = Validate();
        if (ValidationError != null) return;
        CommitDraftToSaved();
        preferencesStore.Save(_saved);
        CubicacionShortcutRouting.InvalidateCache();
        ShellShortcutRouting.InvalidateCache();
        HasPendingChanges = false;
        SaveStatus = "Preferencias guardadas correctamente.";
        AfterPreferencesSaved();
    }

    private void AfterPreferencesSaved()
    {
        if (Workspace.HasOpenProject && !Session.IsReadOnly)
        {
            if (WpfDialogService.Confirm(
                "Márgenes comerciales predeterminados",
                $"Se guardaron los márgenes predeterminados:\n• Gastos Generales: {_saved.DefaultGastosGeneralesPct:N1}%\n• Utilidad: {_saved.DefaultUtilidadPct:N1}%\n\n¿Deseas aplicar estos nuevos porcentajes al proyecto abierto actualmente?"))
            {
                Session.ApplyCommercialMargins(_saved.DefaultGastosGeneralesPct, _saved.DefaultUtilidadPct, _saved.DefaultMermasPct, _saved.DefaultHerramientasPct);
            }
        }
    }

    public void Discard()
    {
        _saved = preferencesStore.Load();
        _draft = Clone(_saved);
        LoadFromDraft();
        HasPendingChanges = false;
        ValidationError = null;
        SaveStatus = "";
    }

    public void Cancel() => Discard();

    bool IPageWithPendingChanges.HasPendingChanges => HasPendingChanges;

    bool IPageWithPendingChanges.TryDiscardChanges()
    {
        Discard();
        return true;
    }

    bool IPageWithCommitChanges.TryApplyChanges() => ApplyChanges();

    public bool RestoreDefaults(Func<bool> confirmFn)
    {
        if (!confirmFn()) return false;
        _draft = new UserPreferences();
        LoadFromDraft();
        HasPendingChanges = true;
        ValidationError = null;
        SaveStatus = "";
        return true;
    }

    public void OpenLogsFolder(IFileOpenService? opener = null)
    {
        var path = EnsureWritableFolder(LogsFolderPath, "Logs");
        (opener ?? fileOpener).OpenFolder(path);
    }

    public void OpenRecoveryFolder(IFileOpenService? opener = null)
    {
        var path = EnsureWritableFolder(RecoveryFolderPath, "Autosave");
        (opener ?? fileOpener).OpenFolder(path);
    }

    private string? Validate()
    {
        if (!string.IsNullOrWhiteSpace(Email) && !Email.Contains('@'))
            return "El correo no tiene un formato válido.";
        if (!string.IsNullOrWhiteSpace(DefaultProjectFolder) && !Directory.Exists(DefaultProjectFolder))
            return $"La carpeta de proyectos no existe: {DefaultProjectFolder}";
        if (!string.IsNullOrWhiteSpace(PriceBankDatabasePath))
        {
            var path = PriceBankDatabasePath.Trim();
            if (!ProductPaths.IsAbsolutePriceBankPath(path))
                return "La ruta del banco de precios debe ser absoluta (ej. C:\\Datos\\banco.db o \\\\servidor\\share\\banco.db).";
            if (!ProductPaths.IsUncPath(path))
            {
                var folder = Path.GetDirectoryName(path);
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                    return $"La carpeta del banco de precios no existe: {folder}";
            }
        }
        var logoError = LetterheadApplicator.ValidateImagePath(LogoPath);
        if (logoError != null) return $"Logo: {logoError}";
        var sigError = LetterheadApplicator.ValidateImagePath(ProfessionalSignatureImagePath);
        if (sigError != null) return $"Firma: {sigError}";
        if (PinMinLength < 4 || PinMaxLength > 8 || PinMinLength > PinMaxLength)
            return "Política PIN inválida: longitud mínima 4, máxima 8 y mínimo <= máximo.";
        if (!TryParsePct(DefaultGastosGeneralesPctText, out var gg) || gg < 0m || gg > 100m)
            return "Gastos Generales predeterminados debe ser un porcentaje entre 0 y 100.";
        if (!TryParsePct(DefaultUtilidadPctText, out var ut) || ut < 0m || ut > 100m)
            return "Utilidad predeterminada debe ser un porcentaje entre 0 y 100.";
        if (!TryParsePct(DefaultMermasPctText, out var mm) || mm < 0m || mm > 100m)
            return "Mermas predeterminadas debe ser un porcentaje entre 0 y 100.";
        if (!TryParsePct(DefaultHerramientasPctText, out var hr) || hr < 0m || hr > 100m)
            return "Herramientas predeterminadas debe ser un porcentaje entre 0 y 100.";
        return null;
    }

    private static bool TryParsePct(string? text, out decimal val)
    {
        val = 0m;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var cleaned = text.Replace("%", "").Trim().Replace(',', '.');
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out val) && val >= 0m;
    }

    private void CommitDraftToSaved()
    {
        _saved.DefaultProjectFolder = DefaultProjectFolder;
        _saved.PriceBankDatabasePath = PriceBankDatabasePath;
        _saved.LetterheadCompanyName = CompanyName;
        _saved.LetterheadRut = Rut;
        _saved.LetterheadAddress = Address;
        _saved.LetterheadPhone = Phone;
        _saved.LetterheadEmail = Email;
        _saved.LetterheadWebsite = Website;
        _saved.LetterheadLogoPath = LogoPath;
        _saved.ProfessionalName = ProfessionalName;
        _saved.ProfessionalRut = ProfessionalRut;
        _saved.ProfessionalRole = ProfessionalRole;
        _saved.ProfessionalRegistroSec = ProfessionalRegistroSec;
        _saved.ProfessionalSignatureImagePath = ProfessionalSignatureImagePath;
        _saved.ProfessionalSignatureWidthMm = ProfessionalSignatureWidthMm;
        _saved.ProfessionalSignatureHeightMm = ProfessionalSignatureHeightMm;
        _saved.DarkMode = IsDarkMode;
        _saved.AutosaveEnabled = AutosaveEnabled;
        if (TryParsePct(DefaultGastosGeneralesPctText, out var gg))
            _saved.DefaultGastosGeneralesPct = gg;
        if (TryParsePct(DefaultUtilidadPctText, out var ut))
            _saved.DefaultUtilidadPct = ut;
        if (TryParsePct(DefaultMermasPctText, out var mm))
            _saved.DefaultMermasPct = mm;
        if (TryParsePct(DefaultHerramientasPctText, out var hr))
            _saved.DefaultHerramientasPct = hr;
        _saved.DefaultMoneda = string.IsNullOrWhiteSpace(DefaultMoneda) ? "CLP" : DefaultMoneda.Trim().ToUpperInvariant();
        _saved.PinSecurity = new PinSecurityPolicy
        {
            MinLength = PinMinLength,
            MaxLength = PinMaxLength,
            BlockSequences = PinBlockSequences,
            BlockRepeats = PinBlockRepeats
        };
        SyncShortcutsToDraft();
        _saved.KeyboardShortcuts = new Dictionary<string, string>(_draft.KeyboardShortcuts, StringComparer.Ordinal);
    }

    private void LoadFromDraft()
    {
        DefaultProjectFolder = _draft.DefaultProjectFolder;
        PriceBankDatabasePath = _draft.PriceBankDatabasePath;
        CompanyName = _draft.LetterheadCompanyName;
        Rut = _draft.LetterheadRut;
        Address = _draft.LetterheadAddress;
        Phone = _draft.LetterheadPhone;
        Email = _draft.LetterheadEmail;
        Website = _draft.LetterheadWebsite;
        LogoPath = _draft.LetterheadLogoPath;
        _logoImageSource = LoadImageSafe(_draft.LetterheadLogoPath);
        OnPropertyChanged(nameof(LogoImageSource));
        ProfessionalName = _draft.ProfessionalName;
        ProfessionalRut = _draft.ProfessionalRut;
        ProfessionalRole = _draft.ProfessionalRole;
        ProfessionalRegistroSec = _draft.ProfessionalRegistroSec;
        ProfessionalSignatureImagePath = _draft.ProfessionalSignatureImagePath;
        ProfessionalSignatureWidthMm = _draft.ProfessionalSignatureWidthMm <= 0 ? 46 : _draft.ProfessionalSignatureWidthMm;
        ProfessionalSignatureHeightMm = _draft.ProfessionalSignatureHeightMm <= 0 ? 16 : _draft.ProfessionalSignatureHeightMm;
        IsDarkMode = _draft.DarkMode;
        AutosaveEnabled = _draft.AutosaveEnabled;
        DefaultGastosGeneralesPctText = _draft.DefaultGastosGeneralesPct.ToString("N1", CultureInfo.CurrentCulture);
        DefaultUtilidadPctText = _draft.DefaultUtilidadPct.ToString("N1", CultureInfo.CurrentCulture);
        DefaultMermasPctText = _draft.DefaultMermasPct.ToString("N1", CultureInfo.CurrentCulture);
        DefaultHerramientasPctText = _draft.DefaultHerramientasPct.ToString("N1", CultureInfo.CurrentCulture);
        DefaultMoneda = string.IsNullOrWhiteSpace(_draft.DefaultMoneda) ? "CLP" : _draft.DefaultMoneda.Trim().ToUpperInvariant();
        PinMinLength = _draft.PinSecurity?.MinLength ?? 4;
        PinMaxLength = _draft.PinSecurity?.MaxLength ?? 6;
        PinBlockSequences = _draft.PinSecurity?.BlockSequences ?? true;
        PinBlockRepeats = _draft.PinSecurity?.BlockRepeats ?? true;
        HasPendingChanges = false;
        LoadShortcutsFromDraft();
    }

    private static UserPreferences Clone(UserPreferences src) => new()
    {
        DefaultProjectFolder = src.DefaultProjectFolder,
        PriceBankDatabasePath = src.PriceBankDatabasePath,
        LetterheadCompanyName = src.LetterheadCompanyName,
        LetterheadRut = src.LetterheadRut,
        LetterheadAddress = src.LetterheadAddress,
        LetterheadPhone = src.LetterheadPhone,
        LetterheadEmail = src.LetterheadEmail,
        LetterheadWebsite = src.LetterheadWebsite,
        LetterheadLogoPath = src.LetterheadLogoPath,
        ProfessionalName = src.ProfessionalName,
        ProfessionalRut = src.ProfessionalRut,
        ProfessionalRole = src.ProfessionalRole,
        ProfessionalRegistroSec = src.ProfessionalRegistroSec,
        ProfessionalSignatureImagePath = src.ProfessionalSignatureImagePath,
        ProfessionalSignatureWidthMm = src.ProfessionalSignatureWidthMm,
        ProfessionalSignatureHeightMm = src.ProfessionalSignatureHeightMm,
        MaterialsPageSize = src.MaterialsPageSize,
        FontSize = src.FontSize,
        ThemeName = src.ThemeName,
        DarkMode = src.DarkMode,
        AutosaveEnabled = src.AutosaveEnabled,
        DefaultGastosGeneralesPct = src.DefaultGastosGeneralesPct,
        DefaultUtilidadPct = src.DefaultUtilidadPct,
        DefaultMermasPct = src.DefaultMermasPct,
        DefaultHerramientasPct = src.DefaultHerramientasPct,
        DefaultMoneda = src.DefaultMoneda,
        ExperimentalFeaturesEnabled = src.ExperimentalFeaturesEnabled,
        RecentFiles = new List<string>(src.RecentFiles),
        PriceFeedUrls = new List<string>(src.PriceFeedUrls),
        // src.PinSecurity puede llegar null si preferences.json es de una versión anterior a
        // este campo, o fue editado a mano: LoadFromDraft() ya se defiende con "?." (línea
        // 435-438) pero este Clone() no lo hacía, y se llama en el constructor de esta pantalla
        // — un preferences.json así crasheaba con NullReferenceException al abrir Preferencias.
        PinSecurity = new PinSecurityPolicy
        {
            MinLength = src.PinSecurity?.MinLength ?? 4,
            MaxLength = src.PinSecurity?.MaxLength ?? 6,
            BlockSequences = src.PinSecurity?.BlockSequences ?? true,
            BlockRepeats = src.PinSecurity?.BlockRepeats ?? true
        },
        KeyboardShortcuts = new Dictionary<string, string>(src.KeyboardShortcuts ?? new Dictionary<string, string>(), StringComparer.Ordinal),
    };

    private static string EnsureWritableFolder(string preferredPath, string leafFolderName)
    {
        try
        {
            if (!Directory.Exists(preferredPath))
                Directory.CreateDirectory(preferredPath);
            return preferredPath;
        }
        catch (UnauthorizedAccessException ex)
        {
            return CreateTempFallbackFolder(leafFolderName, ex);
        }
        catch (IOException ex)
        {
            return CreateTempFallbackFolder(leafFolderName, ex);
        }
    }

    private static string CreateTempFallbackFolder(string leafFolderName, Exception ex)
    {
        var fallback = Path.Combine(Path.GetTempPath(), "QubiCostElectrical", leafFolderName);
        Directory.CreateDirectory(fallback);
        AppLog.Warning("Preferences", $"No se pudo usar la carpeta principal de preferencias; se usará '{fallback}'.", ex);
        return fallback;
    }

    internal static BitmapImage? LoadImageSafe(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length == 0) return null;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = new MemoryStream(bytes);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 200;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    partial void OnUserSearchTextChanged(string value) => ApplyUserFilters();
    partial void OnSelectedUserRoleFilterChanged(string value) => ApplyUserFilters();
    partial void OnSelectedUserActiveFilterChanged(string value) => ApplyUserFilters();

    private void ApplyUserFilters()
    {
        var search = (UserSearchText ?? "").Trim();
        var roleFilter = (SelectedUserRoleFilter ?? "Todos").Trim();
        var activeFilter = (SelectedUserActiveFilter ?? "Todos").Trim();

        var filtered = _allUsers
            .Where(u =>
                (string.IsNullOrWhiteSpace(search)
                 || u.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
                 || u.Email.Contains(search, StringComparison.OrdinalIgnoreCase))
                && (string.Equals(roleFilter, "Todos", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(u.Role.ToString(), roleFilter, StringComparison.OrdinalIgnoreCase))
                && (string.Equals(activeFilter, "Todos", StringComparison.OrdinalIgnoreCase)
                    || (string.Equals(activeFilter, "Activos", StringComparison.OrdinalIgnoreCase) && u.IsActive)
                    || (string.Equals(activeFilter, "Inactivos", StringComparison.OrdinalIgnoreCase) && !u.IsActive)))
            .OrderBy(u => u.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _users.Clear();
        foreach (var user in filtered)
            _users.Add(user);
    }
}
