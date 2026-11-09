using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMS.Play.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonSeriesAndPaymentDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentDetails",
                table: "SchoolSettings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentLink",
                table: "SchoolSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SeriesId",
                table: "Lessons",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_SeriesId_Date",
                table: "Lessons",
                columns: new[] { "SeriesId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Lessons_SeriesId_Date",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "PaymentDetails",
                table: "SchoolSettings");

            migrationBuilder.DropColumn(
                name: "PaymentLink",
                table: "SchoolSettings");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "Lessons");
        }
    }
}
