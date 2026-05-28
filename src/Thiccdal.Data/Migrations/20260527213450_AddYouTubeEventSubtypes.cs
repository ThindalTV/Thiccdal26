using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Thiccdal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddYouTubeEventSubtypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AmountMicros",
                table: "PlatformEvents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "PlatformEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayString",
                table: "PlatformEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LevelName",
                table: "PlatformEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MonthCount",
                table: "PlatformEvents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserComment",
                table: "PlatformEvents",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountMicros",
                table: "PlatformEvents");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "PlatformEvents");

            migrationBuilder.DropColumn(
                name: "DisplayString",
                table: "PlatformEvents");

            migrationBuilder.DropColumn(
                name: "LevelName",
                table: "PlatformEvents");

            migrationBuilder.DropColumn(
                name: "MonthCount",
                table: "PlatformEvents");

            migrationBuilder.DropColumn(
                name: "UserComment",
                table: "PlatformEvents");
        }
    }
}
