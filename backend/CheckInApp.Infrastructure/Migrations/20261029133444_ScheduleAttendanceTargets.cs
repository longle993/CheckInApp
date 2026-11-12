using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckInApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ScheduleAttendanceTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CatechistAttendanceEnabled",
                table: "Sessions",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "StudentAttendanceEnabled",
                table: "Sessions",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CatechistAttendanceEnabled",
                table: "Schedules",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "StudentAttendanceEnabled",
                table: "Schedules",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CatechistAttendanceEnabled",
                table: "Exceptions",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "StudentAttendanceEnabled",
                table: "Exceptions",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CatechistAttendanceEnabled",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "StudentAttendanceEnabled",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "CatechistAttendanceEnabled",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "StudentAttendanceEnabled",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "CatechistAttendanceEnabled",
                table: "Exceptions");

            migrationBuilder.DropColumn(
                name: "StudentAttendanceEnabled",
                table: "Exceptions");
        }
    }
}
