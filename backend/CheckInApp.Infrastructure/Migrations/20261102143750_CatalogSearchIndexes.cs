using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckInApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CatalogSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Enrollments_ClassRoomId",
                table: "Enrollments");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_ClassRoomId",
                table: "Assignments");

            migrationBuilder.CreateIndex(
                name: "IX_Students_FullName_Id",
                table: "Students",
                columns: new[] { "FullName", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_ClassRoomId_From_Until_StudentId",
                table: "Enrollments",
                columns: new[] { "ClassRoomId", "From", "Until", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Catechists_FullName_Id",
                table: "Catechists",
                columns: new[] { "FullName", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Audit_EntityId_CreatedAtUtc",
                table: "Audit",
                columns: new[] { "EntityId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_ClassRoomId_From_Until_CatechistId",
                table: "Assignments",
                columns: new[] { "ClassRoomId", "From", "Until", "CatechistId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Students_FullName_Id",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Enrollments_ClassRoomId_From_Until_StudentId",
                table: "Enrollments");

            migrationBuilder.DropIndex(
                name: "IX_Catechists_FullName_Id",
                table: "Catechists");

            migrationBuilder.DropIndex(
                name: "IX_Audit_EntityId_CreatedAtUtc",
                table: "Audit");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_ClassRoomId_From_Until_CatechistId",
                table: "Assignments");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_ClassRoomId",
                table: "Enrollments",
                column: "ClassRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_ClassRoomId",
                table: "Assignments",
                column: "ClassRoomId");
        }
    }
}
