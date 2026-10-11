namespace QubiCostElectrical.Infrastructure;

internal static class SqliteApuCatalogTable
{
    internal const string TableName = "professional_apu_catalog";

    internal const string CreateSql = "CREATE TABLE IF NOT EXISTS professional_apu_catalog (\r\n    apu_id TEXT PRIMARY KEY,\r\n    code TEXT NOT NULL DEFAULT '',\r\n    name TEXT NOT NULL DEFAULT '',\r\n    description TEXT NOT NULL DEFAULT '',\r\n    category TEXT NOT NULL DEFAULT '',\r\n    unit TEXT NOT NULL DEFAULT 'UN',\r\n    version_count INTEGER NOT NULL DEFAULT 0,\r\n    active_version_number INTEGER NOT NULL DEFAULT 0,\r\n    active_version_id TEXT NOT NULL DEFAULT '',\r\n    active_version_status TEXT NOT NULL DEFAULT 'Draft',\r\n    last_modified_at TEXT NOT NULL,\r\n    last_price REAL NOT NULL DEFAULT 0,\r\n    approved_version_id TEXT NOT NULL DEFAULT '',\r\n    approved_version_number INTEGER NOT NULL DEFAULT 0,\r\n    owner TEXT NOT NULL DEFAULT '',\r\n    tags TEXT NOT NULL DEFAULT '',\r\n    is_active INTEGER NOT NULL DEFAULT 1,\r\n    json TEXT NOT NULL\r\n);\r\nCREATE INDEX IF NOT EXISTS idx_apu_catalog_code ON professional_apu_catalog(code);\r\nCREATE INDEX IF NOT EXISTS idx_apu_catalog_active ON professional_apu_catalog(is_active);\r\nCREATE INDEX IF NOT EXISTS idx_apu_catalog_category ON professional_apu_catalog(category);";
}
