using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academic_Staff_Engagement_Claim_Processing_System.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimAttendanceTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MarksSubmissionId",
                table: "Claims",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClaimAttendances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimId = table.Column<int>(type: "int", nullable: false),
                    MisReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LecturerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CourseCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CourseTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AcademicYear = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Semester = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TotalSessions = table.Column<int>(type: "int", nullable: false),
                    AttendedSessions = table.Column<int>(type: "int", nullable: false),
                    RetrievedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimAttendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimAttendances_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClaimAttendanceRecord",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimAttendanceId = table.Column<int>(type: "int", nullable: false),
                    SessionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SessionTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Attended = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimAttendanceRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimAttendanceRecord_ClaimAttendances_ClaimAttendanceId",
                        column: x => x.ClaimAttendanceId,
                        principalTable: "ClaimAttendances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Claims_MarksSubmissionId",
                table: "Claims",
                column: "MarksSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimAttendanceRecord_ClaimAttendanceId",
                table: "ClaimAttendanceRecord",
                column: "ClaimAttendanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimAttendances_ClaimId",
                table: "ClaimAttendances",
                column: "ClaimId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Claims_MarksSubmissions_MarksSubmissionId",
                table: "Claims",
                column: "MarksSubmissionId",
                principalTable: "MarksSubmissions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Claims_MarksSubmissions_MarksSubmissionId",
                table: "Claims");

            migrationBuilder.DropTable(
                name: "ClaimAttendanceRecord");

            migrationBuilder.DropTable(
                name: "ClaimAttendances");

            migrationBuilder.DropIndex(
                name: "IX_Claims_MarksSubmissionId",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "MarksSubmissionId",
                table: "Claims");
        }
    }
}
