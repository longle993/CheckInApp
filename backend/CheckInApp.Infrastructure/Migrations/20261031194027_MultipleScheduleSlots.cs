using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckInApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MultipleScheduleSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_ClassRoomId_Date",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Schedules_ClassRoomId",
                table: "Schedules");

            migrationBuilder.DropIndex(
                name: "IX_Exceptions_ClassRoomId_Date",
                table: "Exceptions");

            migrationBuilder.AddColumn<Guid>(
                name: "ScheduleId",
                table: "Sessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SourceDate",
                table: "Sessions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MovedToDate",
                table: "Exceptions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ScheduleId",
                table: "Exceptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(@"UPDATE s SET s.ScheduleId=c.Id,s.SourceDate=s.Date FROM Sessions s JOIN Schedules c ON c.ClassRoomId=s.ClassRoomId;
                UPDATE e SET e.ScheduleId=c.Id FROM Exceptions e JOIN Schedules c ON c.ClassRoomId=e.ClassRoomId;");
            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ClassRoomId_Date_OpensAtUtc",
                table: "Sessions",
                columns: new[] { "ClassRoomId", "Date", "OpensAtUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_ClassRoomId",
                table: "Schedules",
                column: "ClassRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Exceptions_ClassRoomId_ScheduleId_Date",
                table: "Exceptions",
                columns: new[] { "ClassRoomId", "ScheduleId", "Date" },
                unique: true,
                filter: "[ScheduleId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_ClassRoomId_Date_OpensAtUtc",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Schedules_ClassRoomId",
                table: "Schedules");

            migrationBuilder.DropIndex(
                name: "IX_Exceptions_ClassRoomId_ScheduleId_Date",
                table: "Exceptions");

            migrationBuilder.DropColumn(
                name: "ScheduleId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "SourceDate",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "MovedToDate",
                table: "Exceptions");

            migrationBuilder.DropColumn(
                name: "ScheduleId",
                table: "Exceptions");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ClassRoomId_Date",
                table: "Sessions",
                columns: new[] { "ClassRoomId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_ClassRoomId",
                table: "Schedules",
                column: "ClassRoomId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Exceptions_ClassRoomId_Date",
                table: "Exceptions",
                columns: new[] { "ClassRoomId", "Date" },
                unique: true);
        }
    }
}
