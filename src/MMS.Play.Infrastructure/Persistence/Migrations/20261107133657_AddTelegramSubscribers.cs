using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMS.Play.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramSubscribers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TelegramSubscribers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LanguageCode = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SubscribedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UnsubscribedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastNotifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramSubscribers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TelegramSubscribers_IsActive_TelegramUserId",
                table: "TelegramSubscribers",
                columns: new[] { "IsActive", "TelegramUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TelegramSubscribers_TelegramUserId",
                table: "TelegramSubscribers",
                column: "TelegramUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TelegramSubscribers");
        }
    }
}
