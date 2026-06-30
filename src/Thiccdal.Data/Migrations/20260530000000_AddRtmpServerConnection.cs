using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Thiccdal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRtmpServerConnection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RtmpServerApiKey",
                table: "RestreamConfigurations",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RtmpServerUrl",
                table: "RestreamConfigurations",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RtmpServerApiKey",
                table: "RestreamConfigurations");

            migrationBuilder.DropColumn(
                name: "RtmpServerUrl",
                table: "RestreamConfigurations");
        }
    }
}
