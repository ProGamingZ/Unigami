using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityScheduler.Migrations
{
    /// <inheritdoc />
    public partial class UpdateInstructorSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedRoomId",
                table: "Instructors",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredYearLevels",
                table: "Instructors",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Instructors_AssignedRoomId",
                table: "Instructors",
                column: "AssignedRoomId");

            migrationBuilder.AddForeignKey(
                name: "FK_Instructors_Rooms_AssignedRoomId",
                table: "Instructors",
                column: "AssignedRoomId",
                principalTable: "Rooms",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Instructors_Rooms_AssignedRoomId",
                table: "Instructors");

            migrationBuilder.DropIndex(
                name: "IX_Instructors_AssignedRoomId",
                table: "Instructors");

            migrationBuilder.DropColumn(
                name: "AssignedRoomId",
                table: "Instructors");

            migrationBuilder.DropColumn(
                name: "PreferredYearLevels",
                table: "Instructors");
        }
    }
}
