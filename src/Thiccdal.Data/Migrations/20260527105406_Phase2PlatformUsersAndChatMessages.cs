using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Thiccdal.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase2PlatformUsersAndChatMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformUsers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    PlatformUserId = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    AvatarUrl = table.Column<string>(type: "TEXT", nullable: false),
                    IsFollower = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSubscriber = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubscriptionMonths = table.Column<int>(type: "INTEGER", nullable: true),
                    IsModerator = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlatformEventId = table.Column<long>(type: "INTEGER", nullable: false),
                    PlatformUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    HtmlContent = table.Column<string>(type: "TEXT", nullable: false),
                    RawData = table.Column<string>(type: "TEXT", nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessages_PlatformEvents_PlatformEventId",
                        column: x => x.PlatformEventId,
                        principalTable: "PlatformEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatMessages_PlatformUsers_PlatformUserId",
                        column: x => x.PlatformUserId,
                        principalTable: "PlatformUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_PlatformEventId",
                table: "ChatMessages",
                column: "PlatformEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_PlatformUserId",
                table: "ChatMessages",
                column: "PlatformUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformUsers_Source_PlatformUserId",
                table: "PlatformUsers",
                columns: new[] { "Source", "PlatformUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "PlatformUsers");
        }
    }
}
