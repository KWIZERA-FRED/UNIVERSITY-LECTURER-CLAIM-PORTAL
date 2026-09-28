using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academic_Staff_Engagement_Claim_Processing_System.Migrations
{
    /// <inheritdoc />
    public partial class AddLecturerFacultyAndDepartment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Department",
                table: "AdminAccounts");

            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "Lecturers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Faculty",
                table: "Lecturers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lecturers_Faculty",
                table: "Lecturers",
                column: "Faculty");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Lecturers_Faculty",
                table: "Lecturers");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "Lecturers");

            migrationBuilder.DropColumn(
                name: "Faculty",
                table: "Lecturers");

            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "AdminAccounts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
