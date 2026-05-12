using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modbus.Core.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceModels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Manufacturer = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DeviceCode = table.Column<byte>(type: "INTEGER", nullable: true),
                    SqpfRegisterAddress = table.Column<ushort>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceModels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SlaveId = table.Column<byte>(type: "INTEGER", nullable: false),
                    TransportType = table.Column<string>(type: "TEXT", nullable: false),
                    SerialNumber = table.Column<uint>(type: "INTEGER", nullable: true),
                    FirmwareVersion = table.Column<byte>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeviceModelId = table.Column<int>(type: "INTEGER", nullable: true),
                    TcpIpAddress = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    TcpPort = table.Column<int>(type: "INTEGER", nullable: true),
                    RtuPortName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    RtuBaudRate = table.Column<int>(type: "INTEGER", nullable: true),
                    RtuDataBits = table.Column<int>(type: "INTEGER", nullable: true),
                    RtuParity = table.Column<string>(type: "TEXT", nullable: true),
                    RtuStopBits = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_DeviceModels_DeviceModelId",
                        column: x => x.DeviceModelId,
                        principalTable: "DeviceModels",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RegisterDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Address = table.Column<ushort>(type: "INTEGER", nullable: false),
                    RegisterType = table.Column<string>(type: "TEXT", nullable: false),
                    DataType = table.Column<string>(type: "TEXT", nullable: false),
                    WordOrder = table.Column<string>(type: "TEXT", nullable: false),
                    ScaleFactor = table.Column<double>(type: "REAL", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    IsWritable = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeviceModelId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegisterDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegisterDefinitions_DeviceModels_DeviceModelId",
                        column: x => x.DeviceModelId,
                        principalTable: "DeviceModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegisterValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Address = table.Column<ushort>(type: "INTEGER", nullable: false),
                    RegisterType = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<double>(type: "REAL", nullable: false),
                    RawWords = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegisterValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegisterValues_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_DeviceModelId",
                table: "Devices",
                column: "DeviceModelId");

            migrationBuilder.CreateIndex(
                name: "IX_RegisterDefinitions_DeviceModelId",
                table: "RegisterDefinitions",
                column: "DeviceModelId");

            migrationBuilder.CreateIndex(
                name: "IX_RegisterValues_DeviceId_Address_RegisterType",
                table: "RegisterValues",
                columns: new[] { "DeviceId", "Address", "RegisterType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegisterDefinitions");

            migrationBuilder.DropTable(
                name: "RegisterValues");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "DeviceModels");
        }
    }
}
