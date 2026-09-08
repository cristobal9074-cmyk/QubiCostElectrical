using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using QubiCostElectrical;
using Microsoft.Win32;

namespace QubiCostElectrical.Wpf.Features.Project;

public sealed partial class NewProjectDialog : Window, INotifyPropertyChanged
{
    private ClosingPolicyType _selectedPolicy = ClosingPolicyType.CostPlusMarkup;
    private bool _fileNameUserEdited;
    private bool _projectNameUserEdited;
    private bool _updatingFileName;
    private bool _updatingProjectName;
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
    public string FileName => FileNameBox.Text.Trim();
    public ProjectTemplate? SelectedTemplate => TemplateBox.SelectedItem as ProjectTemplate;

    public string FullPath
    {
        get
        {
            var folder = TargetFolder;
            var file = NormalizeProjectFileName(FileName);
            return Path.Combine(folder, file);
        }
    }

    public NewProjectDialog()
    {
        InitializeComponent();
        DataContext = this;
        TemplateBox.ItemsSource = ProjectTemplateCatalog.All;
        TemplateBox.SelectedIndex = 1; // Edificio Residencial SEC por defecto
        var docs = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        FolderBox.Text = Path.Combine(docs, "Presupuestos Electricos");
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
        var safe = ProjectTextValidator.MakeSafeFileName(NameBox.Text);
        FileNameBox.Text = safe + ProjectFileTypes.CurrentExtension;
        NameBox.TextChanged += OnProjectNameChanged;
        FileNameBox.TextChanged += OnFileNameUserEdited;
        NameBox.TextChanged += (_, _) => ClearValidationError();
        FolderBox.TextChanged += (_, _) => ClearValidationError();
        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }

    private void OnTemplateChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SelectedTemplate is { } template)
        {
            TemplateSubtitleBlock.Text = template.Subtitulo;
            TemplateNormaBadge.Text = string.IsNullOrWhiteSpace(template.NormaSec) ? "" : $"Norma: {template.NormaSec}";
            TemplateDescriptionBlock.Text = template.Descripcion;
            TemplateInfoBorder.Visibility = Visibility.Visible;
        }
    }

    private void ClearValidationError() => ValidationError = null;

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

    private void OnProjectNameChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_updatingProjectName)
            return;

        _projectNameUserEdited = true;
        if (_fileNameUserEdited) return;
        _updatingFileName = true;
        var safe = ProjectTextValidator.MakeSafeFileName(NameBox.Text);
        FileNameBox.Text = safe + ProjectFileTypes.CurrentExtension;
        _updatingFileName = false;
    }

    private void OnFileNameUserEdited(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_updatingFileName)
            _fileNameUserEdited = true;
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
        if (dialog.ShowDialog() == true)
        {
            var selected = Path.GetDirectoryName(dialog.FileName);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                FolderBox.Text = selected;
                ApplyProjectNameFromFolder(selected);
            }
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
        if (string.IsNullOrWhiteSpace(FileNameBox.Text))
        {
            FileNameBox.Text = ProjectTextValidator.MakeSafeFileName(NameBox.Text) + ProjectFileTypes.CurrentExtension;
        }

        ValidationError = null;
        DialogResult = true;
    }

    private static string NormalizeProjectFileName(string fileName)
    {
        var file = fileName.Trim();
        if (string.IsNullOrWhiteSpace(file))
            return "";

        if (file.EndsWith(ProjectFileTypes.CurrentExtension, StringComparison.OrdinalIgnoreCase))
            return file;

        var extension = Path.GetExtension(file);
        if (string.IsNullOrWhiteSpace(extension))
            return file + ProjectFileTypes.CurrentExtension;

        return Path.ChangeExtension(file, ProjectFileTypes.CurrentExtension) ?? file + ProjectFileTypes.CurrentExtension;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
