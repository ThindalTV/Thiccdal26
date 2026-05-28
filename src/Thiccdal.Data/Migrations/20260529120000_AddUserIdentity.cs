using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Thiccdal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserIdentityId",
                table: "PlatformUsers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserIdentities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserIdentities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformUsers_UserIdentityId",
                table: "PlatformUsers",
                column: "UserIdentityId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlatformUsers_UserIdentities_UserIdentityId",
                table: "PlatformUsers",
                column: "UserIdentityId",
                principalTable: "UserIdentities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlatformUsers_UserIdentities_UserIdentityId",
                table: "PlatformUsers");

            migrationBuilder.DropTable(
                name: "UserIdentities");

            migrationBuilder.DropIndex(
                name: "IX_PlatformUsers_UserIdentityId",
                table: "PlatformUsers");

            migrationBuilder.DropColumn(
                name: "UserIdentityId",
                table: "PlatformUsers");
        }
    }
}
