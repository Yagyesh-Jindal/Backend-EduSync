using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduSyncAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrolledStudentsColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EnrolledStudents",
                table: "Courses",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnrolledStudents",
                table: "Courses");
        }
    }
}
