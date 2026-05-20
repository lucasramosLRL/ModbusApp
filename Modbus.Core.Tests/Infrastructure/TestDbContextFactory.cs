using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Modbus.Core.Persistence;

namespace Modbus.Core.Tests.Infrastructure;

/// <summary>
/// Creates an isolated in-memory SQLite context for each test.
/// The connection must stay open while the context is in use — closing it drops the database.
/// Callers must dispose both the context and the connection.
/// </summary>
internal static class TestDbContextFactory
{
    public static (ModbusDbContext Context, SqliteConnection Connection) Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ModbusDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new ModbusDbContext(options);
        context.Database.EnsureCreated();

        return (context, connection);
    }
}
