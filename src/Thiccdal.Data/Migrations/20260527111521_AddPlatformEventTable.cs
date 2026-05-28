using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Thiccdal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformEventTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Source",
                table: "PlatformEvents",
                newName: "Platform");

            migrationBuilder.AlterColumn<string>(
                name: "Platform",
                table: "PlatformEvents",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "PlatformEvents",
                type: "TEXT",
                maxLength: 21,
                nullable: false,
                defaultValue: "PlatformEvent");

            migrationBuilder.Sql(
                """
                UPDATE PlatformEvents
                SET Platform =
                    CASE Platform
                        WHEN '1' THEN 'Twitch'
                        WHEN '2' THEN 'YouTube'
                        WHEN '3' THEN 'Facebook'
                        WHEN '4' THEN 'X'
                        WHEN '5' THEN 'Discord'
                        WHEN '6' THEN 'LinkedIn'
                        WHEN '7' THEN 'TikTok'
                        WHEN '99' THEN 'Null'
                        ELSE Platform
                    END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EventType",
                table: "PlatformEvents");

            migrationBuilder.RenameColumn(
                name: "Platform",
                table: "PlatformEvents",
                newName: "Source");

            migrationBuilder.AlterColumn<int>(
                name: "Source",
                table: "PlatformEvents",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");
        }
    }
}
