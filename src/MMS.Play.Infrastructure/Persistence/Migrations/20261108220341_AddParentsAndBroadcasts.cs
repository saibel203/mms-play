using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMS.Play.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddParentsAndBroadcasts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsParent",
                table: "TelegramSubscribers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ParentBroadcasts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Audience = table.Column<int>(type: "int", nullable: false),
                    LessonDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Text = table.Column<string>(type: "nvarchar(3500)", maxLength: 3500, nullable: false),
                    Options = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AllowTextAnswer = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentBroadcasts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelegramSubscriberChildren",
                columns: table => new
                {
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChildId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramSubscriberChildren", x => new { x.ParentId, x.ChildId });
                    table.ForeignKey(
                        name: "FK_TelegramSubscriberChildren_TelegramSubscribers_ChildId",
                        column: x => x.ChildId,
                        principalTable: "TelegramSubscribers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TelegramSubscriberChildren_TelegramSubscribers_ParentId",
                        column: x => x.ParentId,
                        principalTable: "TelegramSubscribers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ParentBroadcastDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BroadcastId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<int>(type: "int", nullable: true),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChildName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LessonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LessonDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LessonTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Answer = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AnswerOption = table.Column<int>(type: "int", nullable: true),
                    AnsweredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentBroadcastDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentBroadcastDeliveries_ParentBroadcasts_BroadcastId",
                        column: x => x.BroadcastId,
                        principalTable: "ParentBroadcasts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParentBroadcastDeliveries_TelegramSubscribers_ParentId",
                        column: x => x.ParentId,
                        principalTable: "TelegramSubscribers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParentBroadcastDeliveries_BroadcastId",
                table: "ParentBroadcastDeliveries",
                column: "BroadcastId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentBroadcastDeliveries_ChatId_MessageId",
                table: "ParentBroadcastDeliveries",
                columns: new[] { "ChatId", "MessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_ParentBroadcastDeliveries_ParentId",
                table: "ParentBroadcastDeliveries",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentBroadcasts_CreatedAtUtc",
                table: "ParentBroadcasts",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TelegramSubscriberChildren_ChildId",
                table: "TelegramSubscriberChildren",
                column: "ChildId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParentBroadcastDeliveries");

            migrationBuilder.DropTable(
                name: "TelegramSubscriberChildren");

            migrationBuilder.DropTable(
                name: "ParentBroadcasts");

            migrationBuilder.DropColumn(
                name: "IsParent",
                table: "TelegramSubscribers");
        }
    }
}
