using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMS.Play.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonRescheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "TelegramSubscribers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReminderIntervalMinutes",
                table: "SchoolSettings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RescheduledToId",
                table: "Lessons",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Lessons",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "LessonRescheduleRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LessonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProposedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ProposedTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    ProposedRoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StudentChatId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AdminMessages = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonRescheduleRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonRescheduleRequests_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LessonRescheduleRequests_Rooms_ProposedRoomId",
                        column: x => x.ProposedRoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_RescheduledToId",
                table: "Lessons",
                column: "RescheduledToId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonRescheduleRequests_LessonId_Status",
                table: "LessonRescheduleRequests",
                columns: new[] { "LessonId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonRescheduleRequests_ProposedRoomId",
                table: "LessonRescheduleRequests",
                column: "ProposedRoomId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lessons_Lessons_RescheduledToId",
                table: "Lessons",
                column: "RescheduledToId",
                principalTable: "Lessons",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lessons_Lessons_RescheduledToId",
                table: "Lessons");

            migrationBuilder.DropTable(
                name: "LessonRescheduleRequests");

            migrationBuilder.DropIndex(
                name: "IX_Lessons_RescheduledToId",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "TelegramSubscribers");

            migrationBuilder.DropColumn(
                name: "ReminderIntervalMinutes",
                table: "SchoolSettings");

            migrationBuilder.DropColumn(
                name: "RescheduledToId",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Lessons");
        }
    }
}
