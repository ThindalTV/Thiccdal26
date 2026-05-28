using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Thiccdal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdentitySuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserIdentitySuggestions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirstPlatformUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    SecondPlatformUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    SimilarityScore = table.Column<double>(type: "REAL", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserIdentitySuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserIdentitySuggestions_PlatformUsers_FirstPlatformUserId",
                        column: x => x.FirstPlatformUserId,
                        principalTable: "PlatformUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserIdentitySuggestions_PlatformUsers_SecondPlatformUserId",
                        column: x => x.SecondPlatformUserId,
                        principalTable: "PlatformUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserIdentitySuggestions_FirstPlatformUserId",
                table: "UserIdentitySuggestions",
                column: "FirstPlatformUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserIdentitySuggestions_SecondPlatformUserId",
                table: "UserIdentitySuggestions",
                column: "SecondPlatformUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserIdentitySuggestions_FirstPlatformUserId_SecondPlatformUserId",
                table: "UserIdentitySuggestions",
                columns: new[] { "FirstPlatformUserId", "SecondPlatformUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserIdentitySuggestions");
        }
    }
}
