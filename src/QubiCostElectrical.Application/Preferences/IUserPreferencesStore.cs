using System;

namespace QubiCostElectrical;

/// <summary>
/// Contrato para la persistencia y carga de preferencias del usuario.
/// </summary>
public interface IUserPreferencesStore
{
    /// <summary>Ruta física del archivo de preferencias en disco.</summary>
    string PreferencesFilePath { get; }

    /// <summary>Carga las preferencias desde disco o devuelve las predeterminadas.</summary>
    UserPreferences Load();

    /// <summary>Guarda las preferencias en disco de forma atómica.</summary>
    void Save(UserPreferences prefs);

    /// <summary>Sobrescribe temporalmente la ruta para tests.</summary>
    IDisposable UsePreferencesFilePathForTests(string? path);
}
