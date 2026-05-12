using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Modbus.Core.Persistence;

internal class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ModbusDbContext>
{
    public ModbusDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ModbusDbContext>()
            .UseSqlite("Data Source=modbusapp.design.db", b => b.MigrationsAssembly("Modbus.Core"))
            .Options;
        return new ModbusDbContext(options);
    }
}
