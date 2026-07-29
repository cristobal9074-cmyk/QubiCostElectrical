using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

/// <summary>
/// Banco de precios central, persistente y compartido entre todos los proyectos.
/// Vive fuera de cada .qce en %LOCALAPPDATA%\QubiCostElectrical\banco_precios.db,
/// igual que el CRM. La clave es el código de material normalizado.
/// </summary>
public sealed partial class PriceBankStore : IPriceBankStore
{
    private const int LatestSchemaVersion = 3;
    private readonly string databasePath;
    private List<PriceBankEntry>? allEntriesCache;
}
