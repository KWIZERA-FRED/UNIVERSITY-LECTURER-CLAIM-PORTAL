using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academic_Staff_Engagement_Claim_Processing_System.Migrations
{
    /// <inheritdoc />
    public partial class ParallelDeanHRContractSigning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContractSignatures_ContractId_SequenceOrder",
                table: "ContractSignatures");

            migrationBuilder.CreateIndex(
                name: "IX_ContractSignatures_ContractId_SignerRole",
                table: "ContractSignatures",
                columns: new[] { "ContractId", "SignerRole" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContractSignatures_ContractId_SignerRole",
                table: "ContractSignatures");

            migrationBuilder.CreateIndex(
                name: "IX_ContractSignatures_ContractId_SequenceOrder",
                table: "ContractSignatures",
                columns: new[] { "ContractId", "SequenceOrder" },
                unique: true);
        }
    }
}
