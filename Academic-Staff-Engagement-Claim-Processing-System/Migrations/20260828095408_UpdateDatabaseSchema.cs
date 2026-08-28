using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academic_Staff_Engagement_Claim_Processing_System.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDatabaseSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SignatureHashAtSigning",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "SignedAtUtc",
                table: "Contracts");

            migrationBuilder.CreateTable(
                name: "ContractSignatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    SignerId = table.Column<int>(type: "int", nullable: false),
                    SignerType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SignerRole = table.Column<int>(type: "int", nullable: false),
                    SequenceOrder = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SignatureHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NotificationSent = table.Column<bool>(type: "bit", nullable: false),
                    NotificationSentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractSignatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractSignatures_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractSignatures_ContractId_SequenceOrder",
                table: "ContractSignatures",
                columns: new[] { "ContractId", "SequenceOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractSignatures_ContractId_SignerId_SignerType",
                table: "ContractSignatures",
                columns: new[] { "ContractId", "SignerId", "SignerType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractSignatures");

            migrationBuilder.AddColumn<string>(
                name: "SignatureHashAtSigning",
                table: "Contracts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedAtUtc",
                table: "Contracts",
                type: "datetime2",
                nullable: true);
        }
    }
}
