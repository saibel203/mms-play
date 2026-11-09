using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMS.Play.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaymentReminderEveryDays",
                table: "SchoolSettings",
                type: "int",
                nullable: false,
                // settings saved before payment reminders existed get the appsettings defaults
                defaultValue: 3);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "PaymentReminderTime",
                table: "SchoolSettings",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(10, 0, 0));

            migrationBuilder.AddColumn<bool>(
                name: "PaymentRemindersEnabled",
                table: "SchoolSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "PaymentReminderDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsManual = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentReminderDeliveries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReminderDeliveries_StudentId_SentAtUtc",
                table: "PaymentReminderDeliveries",
                columns: new[] { "StudentId", "SentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReminderDeliveries_SubscriberId_StudentId_SentAtUtc",
                table: "PaymentReminderDeliveries",
                columns: new[] { "SubscriberId", "StudentId", "SentAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentReminderDeliveries");

            migrationBuilder.DropColumn(
                name: "PaymentReminderEveryDays",
                table: "SchoolSettings");

            migrationBuilder.DropColumn(
                name: "PaymentReminderTime",
                table: "SchoolSettings");

            migrationBuilder.DropColumn(
                name: "PaymentRemindersEnabled",
                table: "SchoolSettings");
        }
    }
}
