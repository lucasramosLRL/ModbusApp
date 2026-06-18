using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modbus.Core.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMqttCloudConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MqttBrokerHost",
                table: "Devices",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MqttClientId",
                table: "Devices",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MqttCommandTopic",
                table: "Devices",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MqttPassword",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MqttPort",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MqttReplyTopic",
                table: "Devices",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MqttTelemetryTopic",
                table: "Devices",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MqttUseTls",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MqttUsername",
                table: "Devices",
                type: "TEXT",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MqttBrokerHost",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MqttClientId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MqttCommandTopic",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MqttPassword",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MqttPort",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MqttReplyTopic",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MqttTelemetryTopic",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MqttUseTls",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MqttUsername",
                table: "Devices");
        }
    }
}
