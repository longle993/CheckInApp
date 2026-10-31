using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckInApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AttendanceTimesAndCatechists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CheckedOutAtUtc",
                table: "Attendance",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CatechistAttendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatechistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckedInAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CheckedOutAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatechistAttendance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatechistAttendance_Catechists_CatechistId",
                        column: x => x.CatechistId,
                        principalTable: "Catechists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CatechistAttendance_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatechistAttendance_CatechistId",
                table: "CatechistAttendance",
                column: "CatechistId");

            migrationBuilder.CreateIndex(
                name: "IX_CatechistAttendance_SessionId_CatechistId",
                table: "CatechistAttendance",
                columns: new[] { "SessionId", "CatechistId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatechistAttendance");

            migrationBuilder.DropColumn(
                name: "CheckedOutAtUtc",
                table: "Attendance");
        }
    }
}
