using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckInApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitStudentNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FamilyName",
                table: "Students",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GivenName",
                table: "Students",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
            migrationBuilder.Sql(@"
                UPDATE [Students]
                SET [GivenName] = RIGHT(LTRIM(RTRIM([FullName])), CHARINDEX(N' ', REVERSE(LTRIM(RTRIM([FullName]))) + N' ') - 1),
                    [FamilyName] = RTRIM(LEFT(LTRIM(RTRIM([FullName])), LEN(LTRIM(RTRIM([FullName]))) - (CHARINDEX(N' ', REVERSE(LTRIM(RTRIM([FullName]))) + N' ') - 1)));
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FamilyName",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "GivenName",
                table: "Students");
        }
    }
}
