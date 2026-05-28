using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Thiccdal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRestreamConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RestreamConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IngestUrl = table.Column<string>(type: "TEXT", nullable: false),
                    RecordingOutputPath = table.Column<string>(type: "TEXT", nullable: false),
                    StartWithHost = table.Column<bool>(type: "INTEGER", nullable: false),
                    BrbSlatePath = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestreamConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RestreamDestinationConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlatformName = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestreamDestinationConfigurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RestreamDestinationConfigurations_PlatformName",
                table: "RestreamDestinationConfigurations",
                column: "PlatformName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RestreamConfigurations");

            migrationBuilder.DropTable(
                name: "RestreamDestinationConfigurations");
        }
    }
}
