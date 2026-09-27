using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academic_Staff_Engagement_Claim_Processing_System.Migrations
{
    /// <inheritdoc />
    public partial class AddLecturerRssbNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClaimApprovals_AdminAccounts_ApprovedByAdminAccountId",
                table: "ClaimApprovals");

            migrationBuilder.DropForeignKey(
                name: "FK_ClaimAttendanceRecord_ClaimAttendances_ClaimAttendanceId",
                table: "ClaimAttendanceRecord");

            migrationBuilder.DropForeignKey(
                name: "FK_Claims_CourseAssignments_CourseAssignmentId",
                table: "Claims");

            migrationBuilder.DropIndex(
                name: "IX_CourseAssignments_LecturerId",
                table: "CourseAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ClaimApprovals_ClaimId",
                table: "ClaimApprovals");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClaimAttendanceRecord",
                table: "ClaimAttendanceRecord");

            migrationBuilder.RenameTable(
                name: "ClaimAttendanceRecord",
                newName: "ClaimAttendanceRecords");

            migrationBuilder.RenameIndex(
                name: "IX_ClaimAttendanceRecord_ClaimAttendanceId",
                table: "ClaimAttendanceRecords",
                newName: "IX_ClaimAttendanceRecords_ClaimAttendanceId");

            migrationBuilder.AddColumn<string>(
                name: "RssbNumber",
                table: "Lecturers",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Claims",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "Claims",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ClaimReference",
                table: "Claims",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAtUtc",
                table: "Claims",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LecturerId",
                table: "Claims",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LecturerRemarks",
                table: "Claims",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewerRemarks",
                table: "Claims",
                type: "nvarchar(2000)",
                maxLength: 2000,
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

            migrationBuilder.AlterColumn<string>(
                name: "Comments",
                table: "ClaimApprovals",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Faculty",
                table: "AdminAccounts",
                type: "int",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClaimAttendanceRecords",
                table: "ClaimAttendanceRecords",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "ClaimChecklists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimId = table.Column<int>(type: "int", nullable: false),
                    NotesUploadedToELearning = table.Column<bool>(type: "bit", nullable: false),
                    IndividualGroupWorkOnELearning = table.Column<bool>(type: "bit", nullable: false),
                    MarksAvailableInMIS = table.Column<bool>(type: "bit", nullable: false),
                    MarksApprovedByHOD = table.Column<bool>(type: "bit", nullable: false),
                    HardCopySubmittedToHodAndRegistrar = table.Column<bool>(type: "bit", nullable: false),
                    ExamAndMarkingSchemeAvailable = table.Column<bool>(type: "bit", nullable: false),
                    ClassAttendanceListAvailable = table.Column<bool>(type: "bit", nullable: false),
                    ExamScriptsReturned = table.Column<bool>(type: "bit", nullable: false),
                    ConfirmedByHodId = table.Column<int>(type: "int", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimChecklists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimChecklists_AdminAccounts_ConfirmedByHodId",
                        column: x => x.ConfirmedByHodId,
                        principalTable: "AdminAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClaimChecklists_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Lecturers_RssbNumber",
                table: "Lecturers",
                column: "RssbNumber");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAssignments_LecturerId_CourseId_AcademicYear_Semester",
                table: "CourseAssignments",
                columns: new[] { "LecturerId", "CourseId", "AcademicYear", "Semester" });

            migrationBuilder.CreateIndex(
                name: "IX_Claims_ClaimReference",
                table: "Claims",
                column: "ClaimReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Claims_LecturerId_Status",
                table: "Claims",
                columns: new[] { "LecturerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimApprovals_ClaimId_Decision",
                table: "ClaimApprovals",
                columns: new[] { "ClaimId", "Decision" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimApprovals_ClaimId_SequenceOrder",
                table: "ClaimApprovals",
                columns: new[] { "ClaimId", "SequenceOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimChecklists_ClaimId",
                table: "ClaimChecklists",
                column: "ClaimId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimChecklists_ConfirmedByHodId",
                table: "ClaimChecklists",
                column: "ConfirmedByHodId");

            migrationBuilder.AddForeignKey(
                name: "FK_ClaimApprovals_AdminAccounts_ApprovedByAdminAccountId",
                table: "ClaimApprovals",
                column: "ApprovedByAdminAccountId",
                principalTable: "AdminAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClaimAttendanceRecords_ClaimAttendances_ClaimAttendanceId",
                table: "ClaimAttendanceRecords",
                column: "ClaimAttendanceId",
                principalTable: "ClaimAttendances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Claims_CourseAssignments_CourseAssignmentId",
                table: "Claims",
                column: "CourseAssignmentId",
                principalTable: "CourseAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Claims_Lecturers_LecturerId",
                table: "Claims",
                column: "LecturerId",
                principalTable: "Lecturers",
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
                name: "FK_ClaimAttendanceRecords_ClaimAttendances_ClaimAttendanceId",
                table: "ClaimAttendanceRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Claims_CourseAssignments_CourseAssignmentId",
                table: "Claims");

            migrationBuilder.DropForeignKey(
                name: "FK_Claims_Lecturers_LecturerId",
                table: "Claims");

            migrationBuilder.DropTable(
                name: "ClaimChecklists");

            migrationBuilder.DropIndex(
                name: "IX_Lecturers_RssbNumber",
                table: "Lecturers");

            migrationBuilder.DropIndex(
                name: "IX_CourseAssignments_LecturerId_CourseId_AcademicYear_Semester",
                table: "CourseAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Claims_ClaimReference",
                table: "Claims");

            migrationBuilder.DropIndex(
                name: "IX_Claims_LecturerId_Status",
                table: "Claims");

            migrationBuilder.DropIndex(
                name: "IX_ClaimApprovals_ClaimId_Decision",
                table: "ClaimApprovals");

            migrationBuilder.DropIndex(
                name: "IX_ClaimApprovals_ClaimId_SequenceOrder",
                table: "ClaimApprovals");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClaimAttendanceRecords",
                table: "ClaimAttendanceRecords");

            migrationBuilder.DropColumn(
                name: "RssbNumber",
                table: "Lecturers");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ClaimReference",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "LecturerId",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "LecturerRemarks",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ReviewerRemarks",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "Faculty",
                table: "AdminAccounts");

            migrationBuilder.RenameTable(
                name: "ClaimAttendanceRecords",
                newName: "ClaimAttendanceRecord");

            migrationBuilder.RenameIndex(
                name: "IX_ClaimAttendanceRecords_ClaimAttendanceId",
                table: "ClaimAttendanceRecord",
                newName: "IX_ClaimAttendanceRecord_ClaimAttendanceId");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Claims",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SignatureHashAtApproval",
                table: "ClaimApprovals",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Comments",
                table: "ClaimApprovals",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClaimAttendanceRecord",
                table: "ClaimAttendanceRecord",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAssignments_LecturerId",
                table: "CourseAssignments",
                column: "LecturerId");

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
                name: "FK_ClaimAttendanceRecord_ClaimAttendances_ClaimAttendanceId",
                table: "ClaimAttendanceRecord",
                column: "ClaimAttendanceId",
                principalTable: "ClaimAttendances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Claims_CourseAssignments_CourseAssignmentId",
                table: "Claims",
                column: "CourseAssignmentId",
                principalTable: "CourseAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
