namespace QubiCostElectrical.Infrastructure;

internal static class SqliteApuSnapshotTable
{
	internal const string TableName = "professional_apu_snapshots";

	internal const string CreateSql = "CREATE TABLE IF NOT EXISTS professional_apu_snapshots (\r\n    snapshot_id TEXT PRIMARY KEY,\r\n    source_version_id TEXT NOT NULL,\r\n    captured_at TEXT NOT NULL,\r\n    version_number INTEGER NOT NULL,\r\n    author TEXT NOT NULL DEFAULT '',\r\n    captured_by TEXT NOT NULL DEFAULT '',\r\n    change_reason TEXT NOT NULL DEFAULT '',\r\n    reason TEXT NOT NULL DEFAULT '',\r\n    total_direct_cost REAL NOT NULL DEFAULT 0,\r\n    total_final_price REAL NOT NULL DEFAULT 0,\r\n    currency TEXT NOT NULL DEFAULT 'CLP',\r\n    component_count INTEGER NOT NULL DEFAULT 0,\r\n    resource_ref_count INTEGER NOT NULL DEFAULT 0,\r\n    economic_concept_count INTEGER NOT NULL DEFAULT 0,\r\n    scenario_count INTEGER NOT NULL DEFAULT 0,\r\n    integrity_hash TEXT NOT NULL DEFAULT '',\r\n    json TEXT NOT NULL\r\n);\r\nCREATE INDEX IF NOT EXISTS idx_apu_snapshots_source ON professional_apu_snapshots(source_version_id);";
}
