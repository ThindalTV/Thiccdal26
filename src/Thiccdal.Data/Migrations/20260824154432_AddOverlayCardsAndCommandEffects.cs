using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Thiccdal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOverlayCardsAndCommandEffects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LowerThirdText",
                table: "BotCommands",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LowerThirdTitle",
                table: "BotCommands",
                type: "TEXT",
                nullable: true);

            // Existing commands all replied in chat before effects existed, so they are backfilled as such.
            migrationBuilder.AddColumn<bool>(
                name: "SendInChat",
                table: "BotCommands",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowOnLowerThird",
                table: "BotCommands",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "OverlayCards",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    AccentColor = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OverlayCards", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OverlayCards");

            migrationBuilder.DropColumn(
                name: "LowerThirdText",
                table: "BotCommands");

            migrationBuilder.DropColumn(
                name: "LowerThirdTitle",
                table: "BotCommands");

            migrationBuilder.DropColumn(
                name: "SendInChat",
                table: "BotCommands");

            migrationBuilder.DropColumn(
                name: "ShowOnLowerThird",
                table: "BotCommands");
        }
    }
}
