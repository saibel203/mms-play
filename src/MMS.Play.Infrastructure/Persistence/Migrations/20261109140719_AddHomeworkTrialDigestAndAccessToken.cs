using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMS.Play.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeworkTrialDigestAndAccessToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessToken",
                table: "TelegramSubscribers",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                // the existing settings row gets the appsettings defaults, not false / 00:00
                name: "AdminDigestEnabled",
                table: "SchoolSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "AdminDigestTime",
                table: "SchoolSettings",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(8, 0, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "HomeworkReminderTime",
                table: "SchoolSettings",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(18, 0, 0));

            migrationBuilder.AddColumn<bool>(
                name: "HomeworkRemindersEnabled",
                table: "SchoolSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "TrialBookingEnabled",
                table: "SchoolSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "AdminDigestDeliveries",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminDigestDeliveries", x => x.Date);
                });

            migrationBuilder.CreateTable(
                name: "HomeworkReminderDeliveries",
                columns: table => new
                {
                    SubscriberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeworkReminderDeliveries", x => new { x.SubscriberId, x.Date });
                });

            migrationBuilder.CreateTable(
                name: "Homeworks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Instructions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FromBar = table.Column<int>(type: "int", nullable: true),
                    ToBar = table.Column<int>(type: "int", nullable: true),
                    StartTempoPercent = table.Column<int>(type: "int", nullable: true),
                    TargetTempoPercent = table.Column<int>(type: "int", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Homeworks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Homeworks_Scores_ScoreId",
                        column: x => x.ScoreId,
                        principalTable: "Scores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Homeworks_TelegramSubscribers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "TelegramSubscribers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrialRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    SubscriberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Contact = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PreferredDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PreferredTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LessonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrialRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrialRequests_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TrialRequests_TelegramSubscribers_SubscriberId",
                        column: x => x.SubscriberId,
                        principalTable: "TelegramSubscribers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "HomeworkPractices",
                columns: table => new
                {
                    HomeworkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    PracticedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeworkPractices", x => new { x.HomeworkId, x.Date });
                    table.ForeignKey(
                        name: "FK_HomeworkPractices_Homeworks_HomeworkId",
                        column: x => x.HomeworkId,
                        principalTable: "Homeworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TelegramSubscribers_AccessToken",
                table: "TelegramSubscribers",
                column: "AccessToken",
                unique: true,
                filter: "[AccessToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Homeworks_ScoreId",
                table: "Homeworks",
                column: "ScoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Homeworks_Status",
                table: "Homeworks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Homeworks_StudentId_Status",
                table: "Homeworks",
                columns: new[] { "StudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TrialRequests_LessonId",
                table: "TrialRequests",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_TrialRequests_Status_CreatedAtUtc",
                table: "TrialRequests",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TrialRequests_SubscriberId_Status",
                table: "TrialRequests",
                columns: new[] { "SubscriberId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminDigestDeliveries");

            migrationBuilder.DropTable(
                name: "HomeworkPractices");

            migrationBuilder.DropTable(
                name: "HomeworkReminderDeliveries");

            migrationBuilder.DropTable(
                name: "TrialRequests");

            migrationBuilder.DropTable(
                name: "Homeworks");

            migrationBuilder.DropIndex(
                name: "IX_TelegramSubscribers_AccessToken",
                table: "TelegramSubscribers");

            migrationBuilder.DropColumn(
                name: "AccessToken",
                table: "TelegramSubscribers");

            migrationBuilder.DropColumn(
                name: "AdminDigestEnabled",
                table: "SchoolSettings");

            migrationBuilder.DropColumn(
                name: "AdminDigestTime",
                table: "SchoolSettings");

            migrationBuilder.DropColumn(
                name: "HomeworkReminderTime",
                table: "SchoolSettings");

            migrationBuilder.DropColumn(
                name: "HomeworkRemindersEnabled",
                table: "SchoolSettings");

            migrationBuilder.DropColumn(
                name: "TrialBookingEnabled",
                table: "SchoolSettings");
        }
    }
}
