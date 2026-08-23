using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Thiccdal.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropNonTwitchPlatformTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YouTubeTokens");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.CreateTable(
                name: "YouTubeTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccessToken = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RefreshToken = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YouTubeTokens", x => x.Id);
                });
        }
    }
}
