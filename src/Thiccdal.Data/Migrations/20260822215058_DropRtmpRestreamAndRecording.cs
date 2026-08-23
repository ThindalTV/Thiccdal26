using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Thiccdal.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropRtmpRestreamAndRecording : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RestreamConfigurations");

            migrationBuilder.DropTable(
                name: "RestreamDestinationConfigurations");

            migrationBuilder.DropTable(
                name: "StreamRecordings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RestreamConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BrbSlatePath = table.Column<string>(type: "TEXT", nullable: false),
                    IngestUrl = table.Column<string>(type: "TEXT", nullable: false),
                    RecordingOutputPath = table.Column<string>(type: "TEXT", nullable: false),
                    RtmpServerApiKey = table.Column<string>(type: "TEXT", nullable: false),
                    RtmpServerUrl = table.Column<string>(type: "TEXT", nullable: false),
                    StartWithHost = table.Column<bool>(type: "INTEGER", nullable: false),
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
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    PlatformName = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestreamDestinationConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StreamRecordings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EndedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Error = table.Column<string>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    Platform = table.Column<string>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamRecordings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RestreamDestinationConfigurations_PlatformName",
                table: "RestreamDestinationConfigurations",
                column: "PlatformName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StreamRecordings_Platform_StartedAt",
                table: "StreamRecordings",
                columns: new[] { "Platform", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StreamRecordings_SessionId",
                table: "StreamRecordings",
                column: "SessionId");
        }
    }
}
