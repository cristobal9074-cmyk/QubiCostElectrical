using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using QubiCostElectrical;
using Microsoft.Win32;

namespace QubiCostElectrical.Wpf.Features.Project;

public sealed partial class NewProjectDialog : Window, INotifyPropertyChanged
{
    /// <summary>Prefijo por defecto del código de proyecto (PEL = Presupuestos Eléctricos).</summary>
    private const string DefaultCodePrefix = "PEL";

    private static readonly Regex ProjectCodeRegex =
        new(@"^(?<pre>[A-Za-z]{2,5})(?<num>\d{1,4})A(?<yr>\d{4})(?:[ .].*)?$", RegexOptions.Compiled);

    private ClosingPolicyType _selectedPolicy = ClosingPolicyType.CostPlusMarkup;
    private bool _projectNameUserEdited;
    private bool _updatingProjectName;
    private bool _codeUserEdited;
    private bool _ufUserEdited;
    private bool _updatingUf;
    private bool _ufFetchStarted;
    private bool _uiReady;
    private string? _validationError;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string? ValidationError
    {
        get => _validationError;
        private set
        {
            if (_validationError == value)
                return;
            _validationError = value;
            ValidationErrorBlock.Visibility = string.IsNullOrWhiteSpace(value)
                ? Visibility.Collapsed
                : Visibility.Visible;
            OnPropertyChanged();
        }
    }

    public string ProjectName => NameBox.Text.Trim();
    public string Client => ClientBox.Text.Trim();
    public string Location => LocationBox.Text.Trim();
    public string Currency => (CurrencyBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "CLP";
    public string TargetFolder => FolderBox.Text.Trim();
    public string ProjectCode => CodeBox.Text.Trim().ToUpperInvariant();

    /// <summary>Valor en CLP de la UF ingresado en el diálogo, si es válido y mayor a cero.</summary>
    public decimal? UfValueClp
    {
        get
        {
            if (decimal.TryParse(UfBox.Text.Trim(), System.Globalization.NumberStyles.Number, CultureInfo.CurrentCulture, out var value)
                && value > 0m)
                return value;
            return null;
        }
    }

    /// <summary>
    /// Nombre derivado del código: el archivo .qce se llama igual que el código del proyecto.
    /// Mantiene la API pública que ShellViewModel y los tests usan.
    /// </summary>
    public string FileName
    {
        get
        {
            var code = ProjectCode;
            return string.IsNullOrWhiteSpace(code) ? "" : ProjectTextValidator.MakeSafeFileName(code) + ProjectFileTypes.CurrentExtension;
        }
    }

    public string FullPath
    {
        get
        {
            var folder = TargetFolder;
            var file = FileName;
            return Path.Combine(folder, file);
        }
    }

    public ProjectTemplate? SelectedTemplate => TemplateBox.SelectedItem as ProjectTemplate;

    /// <summary>
    /// Plantilla preseleccionada del diálogo. Sin id explícito devuelve la plantilla en blanco,
    /// de modo que un proyecto nuevo arranque con el itemizado vacío; un id desconocido también
    /// cae en blanco en vez de inyectar partidas que el usuario no pidió.
    /// </summary>
    public static ProjectTemplate ResolveInitialTemplate(string? initialTemplateId)
    {
        if (string.IsNullOrWhiteSpace(initialTemplateId))
            return ProjectTemplateCatalog.Blank;

        return ProjectTemplateCatalog.All.FirstOrDefault(
                   t => string.Equals(t.Id, initialTemplateId, StringComparison.OrdinalIgnoreCase))
               ?? ProjectTemplateCatalog.Blank;
    }

    public NewProjectDialog() : this(null, null)
    {
    }

    public NewProjectDialog(string? initialTemplateId, string? suggestedName = null)
    {
        InitializeComponent();
        DataContext = this;
        TemplateBox.ItemsSource = ProjectTemplateCatalog.All;

        // Un proyecto nuevo nace EN BLANCO: el itemizado queda vacío y se guarda así dentro del
        // .qce. La estructura se agrega solo si el usuario la elige acá o llega desde una tarjeta
        // de plantilla (esas pasan un id explícito y siguen precargando su obra típica).
        TemplateBox.SelectedItem = ResolveInitialTemplate(initialTemplateId);

        if (!string.IsNullOrWhiteSpace(suggestedName))
        {
            NameBox.Text = suggestedName.Trim();
        }

        var docs = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        FolderBox.Text = Path.Combine(docs, "Presupuestos Electricos");
        var prefs = UserPreferencesStore.Load();
        if (!string.IsNullOrWhiteSpace(prefs.DefaultProjectFolder) && Directory.Exists(prefs.DefaultProjectFolder))
        {
            FolderBox.Text = prefs.DefaultProjectFolder;
        }

        if (!string.IsNullOrWhiteSpace(prefs.DefaultMoneda))
        {
            for (int i = 0; i < CurrencyBox.Items.Count; i++)
            {
                if (CurrencyBox.Items[i] is System.Windows.Controls.ComboBoxItem cbi &&
                    string.Equals(cbi.Content?.ToString(), prefs.DefaultMoneda, StringComparison.OrdinalIgnoreCase))
                {
                    CurrencyBox.SelectedIndex = i;
                    break;
                }
            }
        }
        try
        {
            Directory.CreateDirectory(FolderBox.Text);
        }
        catch (IOException)
        {
            // Carpeta sugerida no se pudo crear (disco, ruta redirigida, etc.); OnCreate ya
            // valida Directory.Exists y le pide al usuario elegir otra carpeta antes de continuar.
        }
        catch (UnauthorizedAccessException)
        {
        }

        // Código autogenerado PEL###A{año} según los .qce ya existentes en la carpeta destino.
        CodeBox.Text = GenerateNextProjectCode(FolderBox.Text);

        UfBox.Text = PriceConversion.DefaultValorUfClp.ToString("N2", CultureInfo.CurrentCulture);
        PolicyQuickLabel.Text = "Costo + Markup (motor)";

        NameBox.TextChanged += OnProjectNameChanged;
        NameBox.TextChanged += (_, _) => OnLiveStateChanged();
        ClientBox.TextChanged += (_, _) => OnLiveStateChanged();
        LocationBox.TextChanged += (_, _) => OnLiveStateChanged();
        CodeBox.TextChanged += OnCodeChanged;
        FolderBox.TextChanged += OnFolderChanged;
        UfBox.TextChanged += OnUfTextChanged;
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
            if (string.Equals(Currency, "UF", StringComparison.OrdinalIgnoreCase))
                StartUfFetch();
            UpdateSummary();
            UpdateValidation();
        };

        // A partir de aquí la vista existe completa: los handlers pueden tocar cualquier control.
        _uiReady = true;
        ApplyCurrencyVisibility();
        UpdateSummary();
        UpdateValidation();
    }

    private void OnTemplateChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_uiReady)
            return;
        UpdateSummary();
        UpdateValidation();
    }

    private void OnCurrencyChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_uiReady)
            return;

        ApplyCurrencyVisibility();
        if (string.Equals(Currency, "UF", StringComparison.OrdinalIgnoreCase))
            StartUfFetch();
        OnLiveStateChanged();
    }

    private void ApplyCurrencyVisibility()
    {
        UfRow.Visibility = string.Equals(Currency, "UF", StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnPolicyChanged(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized)
            return;

        _selectedPolicy =
            PolicyTargetPrice.IsChecked == true ? ClosingPolicyType.TargetPrice :
            PolicyLumpSumEpc.IsChecked == true ? ClosingPolicyType.LumpSumEpc :
            PolicyCostPlusFee.IsChecked == true ? ClosingPolicyType.CostPlusFee :
            PolicyUnitPricePure.IsChecked == true ? ClosingPolicyType.UnitPricePure :
            PolicyGainsharePainshare.IsChecked == true ? ClosingPolicyType.GainsharePainshare :
            ClosingPolicyType.CostPlusMarkup;

        switch (_selectedPolicy)
        {
            case ClosingPolicyType.TargetPrice:
                PolicyParamLabel.Text = "Precio objetivo (CLP)";
                PolicyParamBox.Text = "0";
                PolicyParamPanel.Visibility = Visibility.Visible;
                break;
            case ClosingPolicyType.LumpSumEpc:
                PolicyParamLabel.Text = "Contingencia mínima (%)";
                PolicyParamBox.Text = "15";
                PolicyParamPanel.Visibility = Visibility.Visible;
                break;
            case ClosingPolicyType.CostPlusFee:
                PolicyParamLabel.Text = "Fee (% del costo, o monto en CLP si es mayor a 100)";
                PolicyParamBox.Text = "8";
                PolicyParamPanel.Visibility = Visibility.Visible;
                break;
            case ClosingPolicyType.GainsharePainshare:
                PolicyParamLabel.Text = "Costo objetivo (CLP)";
                PolicyParamBox.Text = "0";
                PolicyParamPanel.Visibility = Visibility.Visible;
                break;
            default:
                PolicyParamPanel.Visibility = Visibility.Collapsed;
                break;
        }

        PolicyQuickLabel.Text = _selectedPolicy switch
        {
            ClosingPolicyType.CostPlusMarkup => "Costo + Markup (motor)",
            ClosingPolicyType.TargetPrice => "Precio Objetivo",
            ClosingPolicyType.LumpSumEpc => "Suma Alzada / EPC",
            ClosingPolicyType.CostPlusFee => "Costo + Fee",
            ClosingPolicyType.UnitPricePure => "Precio Unitario Puro",
            ClosingPolicyType.GainsharePainshare => "Gainshare / Painshare",
            _ => ""
        };
    }

    /// <summary>Selección de política de cierre elegida en el diálogo, lista para persistir.</summary>
    public ClosingPolicySelection BuildClosingPolicy()
    {
        decimal param = 0m;
        if (PolicyParamPanel.Visibility == Visibility.Visible)
            decimal.TryParse(PolicyParamBox.Text?.Trim(), System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out param);
        return BuildClosingPolicy(_selectedPolicy, param);
    }

    /// <summary>Mapea la política elegida + su único parámetro a una <see cref="ClosingPolicySelection"/>.</summary>
    public static ClosingPolicySelection BuildClosingPolicy(ClosingPolicyType policy, decimal param)
    {
        param = Math.Max(0m, param);
        var selection = ClosingPolicySelection.Default();
        selection.Policy = policy;
        switch (policy)
        {
            case ClosingPolicyType.TargetPrice:
                selection.TargetPriceClp = param;
                break;
            case ClosingPolicyType.LumpSumEpc:
                selection.LumpSumMinRiskPct = Math.Clamp(param, 0m, 100m);
                break;
            case ClosingPolicyType.CostPlusFee:
                if (param > 100m)
                    selection.FeeClp = param;
                else
                    selection.FeePct = param;
                break;
            case ClosingPolicyType.GainsharePainshare:
                selection.TargetCostClp = param;
                break;
        }
        return selection;
    }

    private async void StartUfFetch()
    {
        if (_ufFetchStarted)
            return;
        _ufFetchStarted = true;

        try
        {
            var uf = await new UfRateService().GetUfHoyAsync();
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => ApplyUfValue(uf.ValorClp));
                return;
            }
            ApplyUfValue(uf.ValorClp);
        }
        catch (Exception)
        {
            // Sin conexión: queda el valor por defecto editable (PriceConversion.DefaultValorUfClp).
        }
    }

    private void ApplyUfValue(decimal valorClp)
    {
        if (_ufUserEdited)
            return;
        _updatingUf = true;
        try
        {
            UfBox.Text = valorClp.ToString("N2", CultureInfo.CurrentCulture);
        }
        finally
        {
            _updatingUf = false;
        }
    }

    private void OnProjectNameChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_updatingProjectName)
            return;

        _projectNameUserEdited = true;
    }

    private void OnCodeChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _codeUserEdited = true;
        CodeHintBlock.Visibility = Visibility.Collapsed;
        OnLiveStateChanged();
    }

    private void OnFolderChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        OnLiveStateChanged();
    }

    private void OnUfTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_updatingUf)
            return;
        _ufUserEdited = true;
        OnLiveStateChanged();
    }

    private void OnLiveStateChanged()
    {
        UpdateSummary();
        UpdateValidation();
    }

    private void OnBrowseFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Seleccionar carpeta destino",
            CheckFileExists = false,
            CheckPathExists = true,
            FileName = ".",
            Filter = "Carpeta|.",
            ValidateNames = false,
            InitialDirectory = FolderBox.Text
        };
        if (dialog.ShowDialog() != true)
            return;

        var selected = Path.GetDirectoryName(dialog.FileName);
        if (string.IsNullOrWhiteSpace(selected))
            return;

        FolderBox.Text = selected;
        ApplyProjectNameFromFolder(selected);

        if (!_codeUserEdited)
        {
            CodeHintBlock.Visibility = Visibility.Visible;
            CodeBox.Text = GenerateNextProjectCode(selected);
        }
    }

    private void ApplyProjectNameFromFolder(string selectedFolder)
    {
        if (_projectNameUserEdited && !string.IsNullOrWhiteSpace(NameBox.Text))
            return;

        var folderName = new DirectoryInfo(selectedFolder).Name.Trim();
        if (string.IsNullOrWhiteSpace(folderName))
            return;

        _updatingProjectName = true;
        NameBox.Text = folderName;
        _updatingProjectName = false;
    }

    private void OnCopyCode(object sender, RoutedEventArgs e)
    {
        var code = ProjectCode;
        if (!string.IsNullOrWhiteSpace(code))
            Clipboard.SetText(code);
    }

    private void OnRegenCode(object sender, RoutedEventArgs e)
    {
        _codeUserEdited = false;
        CodeHintBlock.Visibility = Visibility.Visible;
        CodeBox.Text = GenerateNextProjectCode(TargetFolder);
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control && CreateBtn.IsEnabled)
        {
            OnCreate(CreateBtn, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    /// <summary>
    /// Propone el siguiente código secuencial PEL###A{año} escaneando los .qce existentes en
    /// la carpeta destino. Si la carpeta está vacía (o inaccesible) parte en 001 con el año corriente.
    /// </summary>
    public static string GenerateNextProjectCode(string folder)
    {
        var year = DateTime.Now.Year;
        var pelBestNum = 0;
        var fallbackBestNum = 0;
        var fallbackPrefix = DefaultCodePrefix;

        try
        {
            if (Directory.Exists(folder))
            {
                foreach (var path in Directory.GetFiles(folder, "*" + ProjectFileTypes.CurrentExtension))
                {
                    var match = ProjectCodeRegex.Match(Path.GetFileNameWithoutExtension(path));
                    if (!match.Success)
                        continue;
                    if (!int.TryParse(match.Groups["num"].Value, out var num)
                        || !int.TryParse(match.Groups["yr"].Value, out var yr)
                        || num >= 9999)
                        continue;

                    // Solo se cuentan códigos del año corriente; los de años anteriores no desplazan la secuencia.
                    if (yr != year)
                        continue;

                    var prefix = match.Groups["pre"].Value.ToUpperInvariant();
                    if (prefix == DefaultCodePrefix)
                    {
                        pelBestNum = Math.Max(pelBestNum, num);
                    }
                    else if (num > fallbackBestNum)
                    {
                        // Proveedor de prefijo de respaldo si aún no hay PEL este año.
                        fallbackBestNum = num;
                        fallbackPrefix = prefix;
                    }
                }
            }
        }
        catch (Exception)
        {
            // Carpeta inaccesible: se cae al código por defecto.
        }

        if (pelBestNum > 0)
            return $"{DefaultCodePrefix}{(pelBestNum + 1).ToString("000")}A{year}";

        if (fallbackBestNum > 0)
            return $"{fallbackPrefix}{(fallbackBestNum + 1).ToString("000")}A{year}";

        return $"{DefaultCodePrefix}001A{year}";
    }

    private void OnCreate(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ValidationError = "Debe ingresar un nombre para el proyecto.";
            NameBox.Focus();
            return;
        }
        if (string.IsNullOrWhiteSpace(FolderBox.Text) || !Directory.Exists(FolderBox.Text))
        {
            ValidationError = "Debe seleccionar una carpeta destino válida.";
            BrowseFolderBtn.Focus();
            return;
        }
        if (string.IsNullOrWhiteSpace(CodeBox.Text))
        {
            ValidationError = "Debe ingresar un código de proyecto (sugerencia: Regenerar).";
            CodeBox.Focus();
            return;
        }
        if (string.IsNullOrWhiteSpace(FileName))
        {
            ValidationError = "El código no produce un nombre de archivo .qce válido; cámbielo o regenérelo.";
            CodeBox.Focus();
            return;
        }

        ValidationError = null;
        DialogResult = true;
    }

    private void UpdateSummary()
    {
        SummaryNameBlock.Text = string.IsNullOrWhiteSpace(ProjectName) ? "Sin nombre" : ProjectName;
        SummaryClientBlock.Text = string.IsNullOrWhiteSpace(Client) ? "—" : Client;
        SummaryLocationBlock.Text = string.IsNullOrWhiteSpace(Location) ? "—" : Location;
        SummaryCurrencyBlock.Text = Currency;
        if (string.Equals(Currency, "UF", StringComparison.OrdinalIgnoreCase))
        {
            var uf = UfValueClp;
            SummaryCurrencyBlock.Text = uf is { } ufValue
                ? $"{Currency} · Valor UF {ufValue.ToString("N2", CultureInfo.CurrentCulture)}"
                : Currency;
        }

        if (SelectedTemplate is { } template)
        {
            var norma = string.IsNullOrWhiteSpace(template.NormaSec) ? "" : $"Norma: {template.NormaSec}";
            SummarySubtitleBlock.Text = template.Subtitulo;
            SummaryTemplateBlock.Text = template.Nombre;
            SummaryNormaBlock.Text = norma;
            SummaryNormaBadge.Visibility = string.IsNullOrWhiteSpace(norma) ? Visibility.Collapsed : Visibility.Visible;
            SummaryPartidasBlock.Text = template.EsEnBlanco ? "Se parte de cero" : $"{template.PartidasCount} partidas base";
        }

        var code = ProjectCode;
        SummaryCodeBlock.Text = string.IsNullOrWhiteSpace(code) ? "—" : code;
        SummaryFolderBlock.Text = string.IsNullOrWhiteSpace(TargetFolder) ? "—" : TargetFolder;
        SummaryFileBlock.Text = string.IsNullOrWhiteSpace(FileName) ? "—" : FileName;
    }

    private void UpdateValidation()
    {
        var nameOk = !string.IsNullOrWhiteSpace(ProjectName);
        var codeOk = !string.IsNullOrWhiteSpace(ProjectCode);
        var folderOk = !string.IsNullOrWhiteSpace(TargetFolder) && Directory.Exists(TargetFolder);

        var file = FileName;
        var fileAlreadyExists = folderOk && !string.IsNullOrWhiteSpace(file) && File.Exists(Path.Combine(TargetFolder, file));

        SetLiveChip(ChipNameDot, ChipNameText, nameOk, "Nombre");
        if (codeOk && fileAlreadyExists)
        {
            SetLiveChip(ChipCodeDot, ChipCodeText, codeOk, "Código único", warnText: "El archivo ya existe");
        }
        else
        {
            SetLiveChip(ChipCodeDot, ChipCodeText, codeOk, "Código único");
        }
        SetLiveChip(ChipFolderDot, ChipFolderText, folderOk, "Carpeta destino");

        StepDotIdentidad.Fill = Chip(nameOk ? ChipState.Ok : ChipState.Error);
        StepDotPlantilla.Fill = Chip(SelectedTemplate is not null ? ChipState.Ok : ChipState.Error);
        StepDotArchivo.Fill = Chip(folderOk && codeOk ? ChipState.Ok : ChipState.Warn);

        CreateBtn.IsEnabled = nameOk && codeOk && folderOk;
    }

    private enum ChipState
    {
        Ok,
        Warn,
        Error
    }

    /// <summary>Pinta un chip de estado vivo: verde OK, ámbar advertencia, rojo error.</summary>
    private void SetLiveChip(System.Windows.Shapes.Ellipse dot, System.Windows.Controls.TextBlock text,
        bool okState, string okText, string? warnText = null)
    {
        if (!okState && warnText is not null)
        {
            dot.Fill = Chip(ChipState.Warn);
            text.Text = warnText;
            return;
        }
        dot.Fill = Chip(okState ? ChipState.Ok : ChipState.Error);
        text.Text = okText;
    }

    private System.Windows.Media.Brush Chip(ChipState state) => state switch
    {
        ChipState.Ok => ResourceBrush("Brush.Green"),
        ChipState.Warn => ResourceBrush("Brush.AmberRisk"),
        _ => ResourceBrush("Brush.Danger"),
    };

    private System.Windows.Media.Brush ResourceBrush(string key) =>
        TryFindResource(key) as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Transparent;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
