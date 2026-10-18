using System;

namespace QubiCostElectrical;

/// <summary>
/// Implementación en memoria de <see cref="IUserPreferencesStore"/> para pruebas o fallback sin I/O.
/// </summary>
public sealed class InMemoryUserPreferencesStore : IUserPreferencesStore
{
    private UserPreferences _prefs;

    public InMemoryUserPreferencesStore(UserPreferences? initial = null)
    {
        _prefs = initial ?? new UserPreferences();
    }

    public string PreferencesFilePath => ":memory:";

    public UserPreferences Load() => _prefs;

    public void Save(UserPreferences prefs) => _prefs = prefs ?? new UserPreferences();

    public IDisposable UsePreferencesFilePathForTests(string? path) => new NoopDisposable();

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
