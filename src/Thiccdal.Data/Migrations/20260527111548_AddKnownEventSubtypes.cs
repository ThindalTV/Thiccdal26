using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Thiccdal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddKnownEventSubtypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "GifterPlatformUserId",
                table: "PlatformEvents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGift",
                table: "PlatformEvents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RaidingChannel",
                table: "PlatformEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RewardId",
                table: "PlatformEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RewardTitle",
                table: "PlatformEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tier",
                table: "PlatformEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserInput",
                table: "PlatformEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ViewerCount",
                table: "PlatformEvents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformEvents_GifterPlatformUserId",
                table: "PlatformEvents",
                column: "GifterPlatformUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlatformEvents_PlatformUsers_GifterPlatformUserId",
                table: "PlatformEvents",
                column: "GifterPlatformUserId",
                principalTable: "PlatformUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlatformEvents_PlatformUsers_GifterPlatformUserId",
                table: "PlatformEvents");

            migrationBuilder.DropIndex(
                name: "IX_PlatformEvents_GifterPlatformUserId",
                table: "PlatformEvents");

            migrationBuilder.DropColumn(
                name: "GifterPlatformUserId",
                table: "PlatformEvents");

            migrationBuilder.DropColumn(
                name: "IsGift",
                table: "PlatformEvents");

            migrationBuilder.DropColumn(
                name: "RaidingChannel",
                table: "PlatformEvents");

            migrationBuilder.DropColumn(
                name: "RewardId",
                table: "PlatformEvents");

            migrationBuilder.DropColumn(
                name: "RewardTitle",
                table: "PlatformEvents");

            migrationBuilder.DropColumn(
                name: "Tier",
                table: "PlatformEvents");

            migrationBuilder.DropColumn(
                name: "UserInput",
                table: "PlatformEvents");

            migrationBuilder.DropColumn(
                name: "ViewerCount",
                table: "PlatformEvents");
        }
    }
}
