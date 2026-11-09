using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMS.Play.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RemindersEnabled",
                table: "TelegramSubscribers",
                type: "bit",
                nullable: false,
                // fills the existing rows only: subscribers who already get reminders keep getting them.
                // New rows always get the value from code (true), the model has no database default.
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "MessageTemplates",
                columns: table => new
                {
                    Key = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageTemplates", x => x.Key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessageTemplates");

            migrationBuilder.DropColumn(
                name: "RemindersEnabled",
                table: "TelegramSubscribers");
        }
    }
}
