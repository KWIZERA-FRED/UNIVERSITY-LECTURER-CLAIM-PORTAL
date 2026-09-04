using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academic_Staff_Engagement_Claim_Processing_System.Migrations
{
    /// <inheritdoc />
    public partial class AddContractRatePerHour : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClaimApprovals_AdminAccounts_ApprovedByAdminAccountId",
                table: "ClaimApprovals");

            migrationBuilder.DropForeignKey(
                name: "FK_Claims_Contracts_ContractId",
                table: "Claims");

            migrationBuilder.DropForeignKey(
                name: "FK_Claims_CourseAssignments_CourseAssignmentId",
                table: "Claims");

            migrationBuilder.DropIndex(
                name: "IX_Claims_QrCodeToken",
                table: "Claims");

            migrationBuilder.DropIndex(
                name: "IX_ClaimApprovals_ClaimId_SequenceOrder",
                table: "ClaimApprovals");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "MarksSubmissions");

            migrationBuilder.DropColumn(
                name: "SignedAtUtc",
                table: "MarksSubmissions");

            migrationBuilder.DropColumn(
                name: "SignedBy",
                table: "MarksSubmissions");

            migrationBuilder.DropColumn(
                name: "StoredFilePath",
                table: "MarksSubmissions");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "MarksSubmissions");

            migrationBuilder.RenameColumn(
                name: "OriginalFileName",
                table: "MarksSubmissions",
                newName: "FileName");

            migrationBuilder.RenameColumn(
                name: "FileSize",
                table: "MarksSubmissions",
                newName: "FileSizeBytes");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "MarksSubmissions",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "MarksSubmissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CourseId",
                table: "MarksSubmissions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "MarksSubmissions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                table: "MarksSubmissions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReviewComment",
                table: "MarksSubmissions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewedByManagementId",
                table: "MarksSubmissions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Semester",
                table: "MarksSubmissions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "RatePerHour",
                table: "Contracts",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<string>(
                name: "SignatureHashAtApproval",
                table: "ClaimApprovals",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarksSubmissions_CourseId",
                table: "MarksSubmissions",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_MarksSubmissions_ReviewedByManagementId",
                table: "MarksSubmissions",
                column: "ReviewedByManagementId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimApprovals_ClaimId",
                table: "ClaimApprovals",
                column: "ClaimId");

            migrationBuilder.AddForeignKey(
                name: "FK_ClaimApprovals_AdminAccounts_ApprovedByAdminAccountId",
                table: "ClaimApprovals",
                column: "ApprovedByAdminAccountId",
                principalTable: "AdminAccounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Claims_Contracts_ContractId",
                table: "Claims",
                column: "ContractId",
                principalTable: "Contracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Claims_CourseAssignments_CourseAssignmentId",
                table: "Claims",
                column: "CourseAssignmentId",
                principalTable: "CourseAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MarksSubmissions_AdminAccounts_ReviewedByManagementId",
                table: "MarksSubmissions",
                column: "ReviewedByManagementId",
                principalTable: "AdminAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MarksSubmissions_Courses_CourseId",
                table: "MarksSubmissions",
                column: "CourseId",
                principalTable: "Courses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClaimApprovals_AdminAccounts_ApprovedByAdminAccountId",
                table: "ClaimApprovals");

            migrationBuilder.DropForeignKey(
                name: "FK_Claims_Contracts_ContractId",
                table: "Claims");

            migrationBuilder.DropForeignKey(
                name: "FK_Claims_CourseAssignments_CourseAssignmentId",
                table: "Claims");

            migrationBuilder.DropForeignKey(
                name: "FK_MarksSubmissions_AdminAccounts_ReviewedByManagementId",
                table: "MarksSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_MarksSubmissions_Courses_CourseId",
                table: "MarksSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_MarksSubmissions_CourseId",
                table: "MarksSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_MarksSubmissions_ReviewedByManagementId",
                table: "MarksSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_ClaimApprovals_ClaimId",
                table: "ClaimApprovals");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "MarksSubmissions");

            migrationBuilder.DropColumn(
                name: "CourseId",
                table: "MarksSubmissions");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "MarksSubmissions");

            migrationBuilder.DropColumn(
                name: "FilePath",
                table: "MarksSubmissions");

            migrationBuilder.DropColumn(
                name: "ReviewComment",
                table: "MarksSubmissions");

            migrationBuilder.DropColumn(
                name: "ReviewedByManagementId",
                table: "MarksSubmissions");

            migrationBuilder.DropColumn(
                name: "Semester",
                table: "MarksSubmissions");

            migrationBuilder.DropColumn(
                name: "RatePerHour",
                table: "Contracts");

            migrationBuilder.RenameColumn(
                name: "FileSizeBytes",
                table: "MarksSubmissions",
                newName: "FileSize");

            migrationBuilder.RenameColumn(
                name: "FileName",
                table: "MarksSubmissions",
                newName: "OriginalFileName");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "MarksSubmissions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "MarksSubmissions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedAtUtc",
                table: "MarksSubmissions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedBy",
                table: "MarksSubmissions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoredFilePath",
                table: "MarksSubmissions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "MarksSubmissions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SignatureHashAtApproval",
                table: "ClaimApprovals",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Claims_QrCodeToken",
                table: "Claims",
                column: "QrCodeToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimApprovals_ClaimId_SequenceOrder",
                table: "ClaimApprovals",
                columns: new[] { "ClaimId", "SequenceOrder" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ClaimApprovals_AdminAccounts_ApprovedByAdminAccountId",
                table: "ClaimApprovals",
                column: "ApprovedByAdminAccountId",
                principalTable: "AdminAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Claims_Contracts_ContractId",
                table: "Claims",
                column: "ContractId",
                principalTable: "Contracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Claims_CourseAssignments_CourseAssignmentId",
                table: "Claims",
                column: "CourseAssignmentId",
                principalTable: "CourseAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
