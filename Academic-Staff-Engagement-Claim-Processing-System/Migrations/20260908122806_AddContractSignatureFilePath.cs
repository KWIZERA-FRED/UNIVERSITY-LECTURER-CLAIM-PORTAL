using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academic_Staff_Engagement_Claim_Processing_System.Migrations
{
    /// <inheritdoc />
    public partial class AddContractSignatureFilePath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SignatureFilePath",
                table: "ContractSignatures",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SignatureFilePath",
                table: "ContractSignatures");
        }
    }
}
