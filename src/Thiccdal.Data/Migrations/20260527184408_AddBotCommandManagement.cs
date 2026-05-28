using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Thiccdal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBotCommandManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BotCommands",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Trigger = table.Column<string>(type: "TEXT", nullable: false),
                    ResponseTemplate = table.Column<string>(type: "TEXT", nullable: false),
                    HandlerType = table.Column<string>(type: "TEXT", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    UseCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotCommands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProactiveMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    IntervalSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSentAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProactiveMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BotCommands_Trigger",
                table: "BotCommands",
                column: "Trigger",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BotCommands");

            migrationBuilder.DropTable(
                name: "ProactiveMessages");
        }
    }
}
