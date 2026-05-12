using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace Modbus.Core.Persistence;

/// <summary>
/// Brings the SQLite database up to the current schema:
/// - For new installs: creates everything via EF Core Migrations.
/// - For legacy databases created with the old EnsureCreated + manual ALTER TABLE
///   strategy: idempotently patches missing columns and baselines the first
///   migration as already applied, then applies any newer migrations.
/// </summary>
public static class DatabaseInitializer
{
    private const string InitialMigrationId = "20260512114118_InitialSchema";
    private const string MigrationsProductVersion = "9.0.3";

    public static void Initialize(ModbusDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        conn.Open();

        var hasHistory = TableExists(conn, "__EFMigrationsHistory");
        var hasDevices = TableExists(conn, "Devices");

        if (!hasHistory && hasDevices)
            BaselineLegacyDatabase(conn);

        db.Database.Migrate();
    }

    private static void BaselineLegacyDatabase(DbConnection conn)
    {
        AddColumnIfMissing(conn, "DeviceModels", "SqpfRegisterAddress", "INTEGER");
        AddColumnIfMissing(conn, "Devices", "FirmwareVersion", "INTEGER");

        Execute(conn,
            """
            CREATE TABLE "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            """);

        using var insert = conn.CreateCommand();
        insert.CommandText =
            """INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ($id, $ver);""";
        AddParam(insert, "$id", InitialMigrationId);
        AddParam(insert, "$ver", MigrationsProductVersion);
        insert.ExecuteNonQuery();
    }

    private static bool TableExists(DbConnection conn, string name)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$n;";
        AddParam(cmd, "$n", name);
        return cmd.ExecuteScalar() is not null;
    }

    private static bool ColumnExists(DbConnection conn, string table, string column)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{table}\");";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static void AddColumnIfMissing(DbConnection conn, string table, string column, string type)
    {
        if (ColumnExists(conn, table, column)) return;
        Execute(conn, $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {type};");
    }

    private static void Execute(DbConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static void AddParam(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
