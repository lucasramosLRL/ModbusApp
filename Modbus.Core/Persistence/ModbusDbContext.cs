using Microsoft.EntityFrameworkCore;
using Modbus.Core.Domain.Entities;

namespace Modbus.Core.Persistence;

public class ModbusDbContext : DbContext
{
    public DbSet<ModbusDevice>         Devices              { get; set; }
    public DbSet<DeviceModel>          DeviceModels         { get; set; }
    public DbSet<RegisterDefinition>   RegisterDefinitions  { get; set; }
    public DbSet<RegisterValue>        RegisterValues       { get; set; }

    public ModbusDbContext(DbContextOptions<ModbusDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ModbusDbContext).Assembly);
}
