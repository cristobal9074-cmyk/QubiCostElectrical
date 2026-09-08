using System.IO;

namespace QubiCostElectrical;

public interface IProjectWorkspaceService
{
    BudgetProjectSession Session { get; }
    bool HasOpenProject { get; }
    Task OpenAsync(string path, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
    Task SaveAsAsync(string path, CancellationToken cancellationToken = default);
    BudgetValidationResult ValidateCurrent();
    void DiscardChanges();
}

public sealed class ProjectWorkspaceService : IProjectWorkspaceService
{
    private readonly IProjectRepository repository;
    private readonly IValidationService validation;
    private readonly IProjectFileFormatDetector formatDetector;
    private readonly ProjectCollaborationLockService? collaborationLock;

    public ProjectWorkspaceService(
        BudgetProjectSession session,
        IProjectRepository repository,
        IValidationService validation,
        ProjectCollaborationLockService? collaborationLock = null)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.validation = validation ?? throw new ArgumentNullException(nameof(validation));
        this.collaborationLock = collaborationLock;
        formatDetector = new ProjectFileFormatDetector();
    }

    public BudgetProjectSession Session { get; }

    public bool HasOpenProject => !string.IsNullOrWhiteSpace(Session.CurrentProjectPath);

    public async Task OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Ruta de proyecto requerida.", nameof(path));

        path = ResolveProjectPath(path);

        var format = formatDetector.Detect(path);
        if (format == ProjectFileFormat.Unknown)
            throw new InvalidDataException("El archivo no contiene un proyecto compatible de Qubi Cost Electrical.");

        ProjectFileData data = format switch
        {
            ProjectFileFormat.Sqlite => await LoadSqliteWithBackupFallbackAsync(path, cancellationToken).ConfigureAwait(false),
            ProjectFileFormat.LegacyJson => await LoadJsonWithBackupFallbackAsync(path, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidDataException("El archivo no contiene un proyecto compatible de Qubi Cost Electrical.")
        };

        if (format != ProjectFileFormat.Sqlite || ShouldMigrateToCurrentFormat(path))
        {
            await MigrateToCurrentFormatAsync(path, data, cancellationToken).ConfigureAwait(false);
            return;
        }

        Session.ApplySnapshot(data, path, markDirty: false);
        Session.MarkSaved();
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Session.CurrentProjectPath))
            throw new InvalidOperationException("No hay proyecto abierto para guardar.");
        Session.EnsureWritable("guardar el proyecto");
        EnsureCollaborationLease(Session.CurrentProjectPath);

        await SaveAsAsync(Session.CurrentProjectPath, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Ruta de guardado requerida.", nameof(path));
        Session.EnsureWritable("guardar como");

        path = NormalizeCurrentProjectPath(path);
        // SaveAs a otra ruta no exige el lease del archivo original; SaveAsync sí.
        bool requiresLease = string.Equals(path, Session.CurrentProjectPath, StringComparison.OrdinalIgnoreCase);
        if (requiresLease)
            EnsureCollaborationLease(path);

        using var writeLease = requiresLease ? collaborationLock?.TryBeginWriteLease(path) : null;
        if (requiresLease && collaborationLock != null && collaborationLock.RequiresExclusiveLease(path) && writeLease == null)
            throw new ProjectLockLostException();

        if (requiresLease)
        {
            EnsureCollaborationLease(path);
            collaborationLock?.TryRefreshLease(path);
        }

        var snapshot = Session.CreateSnapshot();
        snapshot.Proyecto.FechaModificacion = AppClock.Now;

        await CreateSQLiteBackupIfNeededAsync(path, cancellationToken).ConfigureAwait(false);
        await repository.SaveSQLiteSnapshotAsync(path, snapshot, cancellationToken).ConfigureAwait(false);

        if (requiresLease)
            EnsureCollaborationLease(path);

        Session.ApplySnapshot(snapshot, path, markDirty: false);
        Session.MarkSaved();
    }

    /// <summary>
    /// Si hay bloqueo de colaboración activo para esta sesión, exige que aún seamos dueños
    /// antes de escribir el .qce (evita overwrite tras takeover).
    /// </summary>
    private void EnsureCollaborationLease(string projectPath)
    {
        if (collaborationLock == null)
            return;
        if (!collaborationLock.RequiresExclusiveLease(projectPath))
            return;
        if (collaborationLock.OwnsActiveLock(projectPath))
            return;

        throw new ProjectLockLostException();
    }

    public BudgetValidationResult ValidateCurrent()
    {
        var snapshot = Session.CreateSnapshot();
        return validation.ValidateProject(snapshot);
    }

    public void DiscardChanges()
    {
        if (string.IsNullOrWhiteSpace(Session.CurrentProjectPath))
        {
            Session.NewProject(Environment.UserName);
            return;
        }

        string path = Session.CurrentProjectPath;
        ProjectFileData data = formatDetector.Detect(path) == ProjectFileFormat.Sqlite
            ? repository.LoadSQLiteSnapshot(path)
            : repository.LoadProjectFile(path, ProjectJsonSerializer.Options);

        Session.ApplySnapshot(data, path, markDirty: false);
        Session.MarkSaved();
    }

    private async Task<ProjectFileData> LoadSqliteWithBackupFallbackAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await repository.LoadSQLiteSnapshotAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            string backup = repository.GetLatestUsableSQLiteBackup(path);
            if (string.IsNullOrWhiteSpace(backup))
                throw;

            AppLog.Warning("ProjectWorkspace", $"SQLite principal falló; usando respaldo '{Path.GetFileName(backup)}'.", ex);
            return await repository.LoadSQLiteSnapshotAsync(backup, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<ProjectFileData> LoadJsonWithBackupFallbackAsync(string path, CancellationToken cancellationToken)
    {
        ProjectFileData sourceData;
        try
        {
            sourceData = await repository.LoadProjectFileAsync(path, ProjectJsonSerializer.Options, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            string backup = GetJsonBackupPath(path);
            if (!File.Exists(backup))
                throw;

            AppLog.Warning("ProjectWorkspace", $"Archivo principal falló; usando respaldo '{Path.GetFileName(backup)}'.", ex);
            sourceData = await repository.LoadProjectFileAsync(backup, ProjectJsonSerializer.Options, cancellationToken).ConfigureAwait(false);
        }

        // Buscar base de datos sidecar
        string sidecarPath = path + ProjectFileTypes.SqliteSuffix; // ej. Hospital.pepro.db
        if (!File.Exists(sidecarPath))
        {
            // Intentar con la extensión cambiada a .db (ej. Hospital.db) por compatibilidad
            string alternativePath = Path.ChangeExtension(path, ".db");
            if (File.Exists(alternativePath) && !PathsEqual(alternativePath, path))
            {
                sidecarPath = alternativePath;
            }
        }

        if (File.Exists(sidecarPath))
        {
            try
            {
                var sqliteData = await repository.LoadSQLiteSnapshotAsync(sidecarPath, cancellationToken).ConfigureAwait(false);

                // Mezclar colecciones del SQLite en sourceData
                sourceData.Itemizado = sqliteData.Itemizado;
                sourceData.Materiales = sqliteData.Materiales;
                sourceData.Cubicacion = sqliteData.Cubicacion;
                sourceData.Plantillas = sqliteData.Plantillas;
                sourceData.ConjuntosElectricos = sqliteData.ConjuntosElectricos;
                sourceData.CubicacionRecursos = sqliteData.CubicacionRecursos;
                sourceData.CubicacionZonaCantidades = sqliteData.CubicacionZonaCantidades;
                sourceData.CubicacionValorizaciones = sqliteData.CubicacionValorizaciones;
                sourceData.PartidaApuVinculos = sqliteData.PartidaApuVinculos;
                sourceData.Versiones = sqliteData.Versiones;
                sourceData.Apus = sqliteData.Apus;
                sourceData.OrdenesCambio = sqliteData.OrdenesCambio;
                sourceData.QtyHeadersByItem = sqliteData.QtyHeadersByItem;
                sourceData.Zonas = sqliteData.Zonas;
                sourceData.Mediciones = sqliteData.Mediciones;
                sourceData.CubicacionSC = sqliteData.CubicacionSC;
                sourceData.CubicacionScConfig = sqliteData.CubicacionScConfig;

                AppLog.Info("ProjectWorkspace", $"Reconciliación exitosa: se integraron los datos del sidecar SQLite '{Path.GetFileName(sidecarPath)}' al proyecto legacy.");
            }
            catch (Exception ex)
            {
                AppLog.Warning("ProjectWorkspace", $"Se detectó base de datos sidecar '{sidecarPath}' pero falló su carga. Se continuará solo con los datos del JSON.", ex);
            }
        }

        return sourceData;
    }

    private async Task MigrateToCurrentFormatAsync(string sourcePath, ProjectFileData sourceData, CancellationToken cancellationToken)
    {
        string targetPath = NormalizeCurrentProjectPath(sourcePath);

        try
        {
            if (File.Exists(sourcePath))
                File.Copy(sourcePath, GetMigrationBackupPath(sourcePath), overwrite: true);
        }
        catch (Exception ex)
        {
            AppLog.Warning("ProjectWorkspace", $"No se pudo crear respaldo previo para '{sourcePath}'.", ex);
        }

        if (!PathsEqual(sourcePath, targetPath) && File.Exists(targetPath))
        {
            try
            {
                await repository.CreateNumberedSQLiteBackupAsync(targetPath, 6, "Backup previo a migración").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLog.Warning("ProjectWorkspace", $"No se pudo respaldar el destino previo '{targetPath}'.", ex);
            }
        }

        await repository.SaveSQLiteSnapshotAsync(targetPath, sourceData, cancellationToken).ConfigureAwait(false);
        var migrated = await repository.LoadSQLiteSnapshotAsync(targetPath, cancellationToken).ConfigureAwait(false);
        ValidateMigration(sourceData, migrated, sourcePath, targetPath);

        // Si la migración es exitosa y se guardó en un archivo diferente (o en el mismo renombrado),
        // eliminamos los archivos desincronizados del formato legacy:
        // 1. El JSON legacy original (sourcePath)
        // 2. El sidecar legacy original (sourcePath + ".db")
        
        // Liberar todos los pools de conexiones de SQLite para desbloquear los archivos físicos en disco
        repository.ClearSQLitePools();

        if (!PathsEqual(sourcePath, targetPath))
        {
            try
            {
                if (File.Exists(sourcePath))
                    File.Delete(sourcePath);
                
                string sidecarPath = sourcePath + ProjectFileTypes.SqliteSuffix;
                if (File.Exists(sidecarPath))
                    File.Delete(sidecarPath);
                else
                {
                    string alternativePath = Path.ChangeExtension(sourcePath, ".db");
                    if (File.Exists(alternativePath) && !PathsEqual(alternativePath, sourcePath))
                        File.Delete(alternativePath);
                }
                
                AppLog.Info("ProjectWorkspace", $"Archivos legacy eliminados tras migración exitosa: '{sourcePath}' y su base de datos sidecar.");
            }
            catch (Exception ex)
            {
                AppLog.Warning("ProjectWorkspace", $"No se pudieron limpiar todos los archivos legacy tras migración.", ex);
            }
        }
        else
        {
            // Si tienen la misma ruta física, el archivo ya es el SQLite.
            // Aún así, buscamos y eliminamos el sidecar legacy redundante (.db) si existiese separado.
            try
            {
                string sidecarPath = sourcePath + ProjectFileTypes.SqliteSuffix;
                if (File.Exists(sidecarPath))
                    File.Delete(sidecarPath);
                else
                {
                    string alternativePath = Path.ChangeExtension(sourcePath, ".db");
                    if (File.Exists(alternativePath) && !PathsEqual(alternativePath, sourcePath))
                        File.Delete(alternativePath);
                }
            }
            catch (Exception ex)
            {
                AppLog.Warning("ProjectWorkspace", $"No se pudo limpiar el archivo sidecar redundante posterior a migración en caliente.", ex);
            }
        }

        Session.ApplySnapshot(migrated, targetPath, markDirty: false);
        Session.MarkSaved();
    }

    private async Task CreateSQLiteBackupIfNeededAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return;

        try
        {
            await repository.CreateNumberedSQLiteBackupAsync(path, 6, "Backup previo a guardado", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppLog.Warning("ProjectWorkspace", $"No se pudo crear backup SQLite previo para '{path}'.", ex);
        }
    }

    private static bool ShouldMigrateToCurrentFormat(string path)
    {
        return !PathsEqual(path, NormalizeCurrentProjectPath(path));
    }

    private static string NormalizeCurrentProjectPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ProjectFileTypes.CurrentExtension;

        string normalized = path;
        if (Path.GetExtension(normalized).Equals(ProjectFileTypes.SqliteSuffix, StringComparison.OrdinalIgnoreCase))
            normalized = Path.ChangeExtension(normalized, null) ?? normalized;

        return Path.ChangeExtension(normalized, ProjectFileTypes.CurrentExtension) ?? normalized;
    }

    private static string ResolveProjectPath(string path)
    {
        if (File.Exists(path))
            return path;

        if (!Directory.Exists(path))
            return path;

        string folderName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(folderName))
            folderName = Path.GetFileName(Path.GetFullPath(path));

        string preferredQce = Path.Combine(path, folderName + ProjectFileTypes.CurrentExtension);
        if (File.Exists(preferredQce))
            return preferredQce;

        string preferredPepro = Path.Combine(path, folderName + ProjectFileTypes.LegacyExtension);
        if (File.Exists(preferredPepro))
            return preferredPepro;

        var qceFiles = Directory.EnumerateFiles(path, "*" + ProjectFileTypes.CurrentExtension, SearchOption.TopDirectoryOnly).ToList();
        if (qceFiles.Count == 1)
            return qceFiles[0];

        var peproFiles = Directory.EnumerateFiles(path, "*" + ProjectFileTypes.LegacyExtension, SearchOption.TopDirectoryOnly).ToList();
        if (peproFiles.Count == 1)
            return peproFiles[0];

        if (qceFiles.Count > 1 || peproFiles.Count > 1)
            throw new InvalidDataException(
                $"La carpeta contiene más de un proyecto compatible. Abra el archivo .qce o .pepro exacto dentro de:\n{path}");

        throw new InvalidDataException(
            $"No se encontró un archivo de proyecto compatible dentro de:\n{path}\n\nAbra el archivo .qce o .pepro directamente.");
    }

    private static string GetMigrationBackupPath(string path) => path + ".bak";

    private static string GetJsonBackupPath(string path) => path + ".bak";

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateMigration(ProjectFileData source, ProjectFileData migrated, string sourcePath, string targetPath)
    {
        if (source.Proyecto.Nombre != migrated.Proyecto.Nombre ||
            source.Proyecto.Cliente != migrated.Proyecto.Cliente ||
            source.Proyecto.Ubicacion != migrated.Proyecto.Ubicacion ||
            source.Proyecto.UsuarioNombre != migrated.Proyecto.UsuarioNombre ||
            source.Proyecto.UsuarioRol != migrated.Proyecto.UsuarioRol ||
            source.Proyecto.MonedaPresupuesto != migrated.Proyecto.MonedaPresupuesto ||
            source.Proyecto.EstadoPresupuesto != migrated.Proyecto.EstadoPresupuesto ||
            source.Proyecto.ValorUfClp != migrated.Proyecto.ValorUfClp ||
            source.Proyecto.TipoCambioUsdClp != migrated.Proyecto.TipoCambioUsdClp ||
            source.Proyecto.GastosGeneralesPct != migrated.Proyecto.GastosGeneralesPct ||
            source.Proyecto.UtilidadPct != migrated.Proyecto.UtilidadPct ||
            source.Proyecto.MetrosCuadrados != migrated.Proyecto.MetrosCuadrados ||
            source.Itemizado.Count != migrated.Itemizado.Count ||
            source.Materiales.Count != migrated.Materiales.Count ||
            source.Cubicacion.Count != migrated.Cubicacion.Count ||
            source.Plantillas.Count != migrated.Plantillas.Count ||
            source.Apus.Count != migrated.Apus.Count ||
            source.Versiones.Count != migrated.Versiones.Count ||
            source.OrdenesCambio.Count != migrated.OrdenesCambio.Count)
        {
            throw new InvalidDataException(
                $"La migración del proyecto '{sourcePath}' a '{targetPath}' no conservó los datos principales.");
        }
    }
}
