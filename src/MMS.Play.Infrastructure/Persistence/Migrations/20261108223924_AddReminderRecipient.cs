using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMS.Play.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderRecipient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_LessonReminderDeliveries",
                table: "LessonReminderDeliveries");

            migrationBuilder.AddColumn<Guid>(
                name: "SubscriberId",
                table: "LessonReminderDeliveries",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // the deliveries so far all went to the student of the lesson
            migrationBuilder.Sql(
                """
                UPDATE d SET d.SubscriberId = l.StudentId
                FROM LessonReminderDeliveries d
                JOIN Lessons l ON l.Id = d.LessonId
                WHERE l.StudentId IS NOT NULL
                """);

            migrationBuilder.AddPrimaryKey(
                name: "PK_LessonReminderDeliveries",
                table: "LessonReminderDeliveries",
                columns: new[] { "LessonId", "SubscriberId", "SlotUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // one row per lesson and slot again: keep the student's, drop the parents'
            migrationBuilder.Sql(
                """
                DELETE d
                FROM LessonReminderDeliveries d
                JOIN Lessons l ON l.Id = d.LessonId
                WHERE l.StudentId IS NULL OR d.SubscriberId <> l.StudentId
                """);

            migrationBuilder.DropPrimaryKey(
                name: "PK_LessonReminderDeliveries",
                table: "LessonReminderDeliveries");

            migrationBuilder.DropColumn(
                name: "SubscriberId",
                table: "LessonReminderDeliveries");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LessonReminderDeliveries",
                table: "LessonReminderDeliveries",
                columns: new[] { "LessonId", "SlotUtc" });
        }
    }
}
