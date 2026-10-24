using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMS.Play.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Artist = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Extension = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scores", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Scores_Sha256",
                table: "Scores",
                column: "Sha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Scores_UploadedAtUtc",
                table: "Scores",
                column: "UploadedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Scores");
        }
    }
}
