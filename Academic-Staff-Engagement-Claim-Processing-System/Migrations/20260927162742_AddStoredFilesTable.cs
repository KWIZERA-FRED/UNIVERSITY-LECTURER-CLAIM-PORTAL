using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academic_Staff_Engagement_Claim_Processing_System.Migrations
{
    /// <inheritdoc />
    public partial class AddStoredFilesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FilePath",
                table: "MarksSubmissions");

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedAtUtc",
                table: "MarksSubmissions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StorageFileId",
                table: "MarksSubmissions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "StoredFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Folder = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredFiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarksSubmissions_StorageFileId",
                table: "MarksSubmissions",
                column: "StorageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_CreatedAtUtc",
                table: "StoredFiles",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_Folder",
                table: "StoredFiles",
                column: "Folder");

            migrationBuilder.AddForeignKey(
                name: "FK_MarksSubmissions_StoredFiles_StorageFileId",
                table: "MarksSubmissions",
                column: "StorageFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MarksSubmissions_StoredFiles_StorageFileId",
                table: "MarksSubmissions");

            migrationBuilder.DropTable(
                name: "StoredFiles");

            migrationBuilder.DropIndex(
                name: "IX_MarksSubmissions_StorageFileId",
                table: "MarksSubmissions");

            migrationBuilder.DropColumn(
                name: "SignedAtUtc",
                table: "MarksSubmissions");

            migrationBuilder.DropColumn(
                name: "StorageFileId",
                table: "MarksSubmissions");

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                table: "MarksSubmissions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }
    }
}
