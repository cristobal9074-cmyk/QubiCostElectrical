using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Dashboard;

/// <summary>Botón "Actualizar UF al día" de la tarjeta TOTAL VENTA (cockpit Resumen).</summary>
public sealed partial class DashboardViewModel
{
    private const string UfUpdateLabelIdle = "Actualizar UF al día";

    private DashboardUfUpdateService ufUpdater = null!;

    private string _ufUpdateLabel = UfUpdateLabelIdle;
    private bool _isUpdatingUf;

    public ICommand ActualizarUfCommand { get; }

    public string UfUpdateLabel
    {
        get => _ufUpdateLabel;
        private set => SetProperty(ref _ufUpdateLabel, value);
    }

    public bool IsUpdatingUf
    {
        get => _isUpdatingUf;
        private set
        {
            if (SetProperty(ref _isUpdatingUf, value))
                OnPropertyChanged(nameof(PuedeActualizarUf));
        }
    }

    public bool PuedeActualizarUf => !IsUpdatingUf && HasOpenProject && !session.IsReadOnly;

    private async Task ActualizarUfAsync()
    {
        if (IsUpdatingUf || !HasOpenProject) return;
        if (session.IsReadOnly)
        {
            ModuleFeedback.NotifyReadOnly("Actualizar UF");
            return;
        }

        IsUpdatingUf = true;
        try
        {
            UfUpdateOutcome outcome = await ufUpdater.ActualizarAsync();
            switch (outcome.Status)
            {
                case UfUpdateStatus.SoloLectura:
                    ModuleFeedback.NotifyReadOnly("Actualizar UF");
                    break;

                case UfUpdateStatus.SinCambios:
                    UfUpdateLabel = $"UF al día · $ {outcome.ValorClp:N0}";
                    ModuleFeedback.NotifySuccess("UF al día",
                        $"La UF del proyecto ya corresponde al {outcome.Fecha:dd/MM/yyyy}: $ {outcome.ValorClp:N2}.");
                    break;

                default:
                    UfUpdateLabel = $"UF al día · $ {outcome.ValorClp:N0}";
                    ModuleFeedback.NotifySuccess("UF actualizada",
                        $"1 UF = $ {outcome.ValorClp:N2} · vigente al {outcome.Fecha:dd/MM/yyyy}.");
                    Refresh();
                    break;
            }
        }
        catch (HttpRequestException)
        {
            ShowUfFetchError();
        }
        catch (TaskCanceledException)
        {
            ShowUfFetchError();
        }
        catch (JsonException)
        {
            ShowUfFetchError();
        }
        catch (InvalidDataException)
        {
            ShowUfFetchError();
        }
        finally
        {
            IsUpdatingUf = false;
        }
    }

    private static void ShowUfFetchError() =>
        ModuleFeedback.NotifyError("Actualizar UF",
            "No se pudo obtener la UF del día en Chile. Revisa tu conexión a internet e inténtalo de nuevo.");
}
