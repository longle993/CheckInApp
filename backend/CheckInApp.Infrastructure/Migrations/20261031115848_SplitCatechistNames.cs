using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckInApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitCatechistNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FamilyName",
                table: "Catechists",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GivenName",
                table: "Catechists",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
            migrationBuilder.Sql(@"
                UPDATE [Catechists]
                SET [GivenName] = RIGHT(LTRIM(RTRIM([FullName])), CHARINDEX(N' ', REVERSE(LTRIM(RTRIM([FullName]))) + N' ') - 1),
                    [FamilyName] = RTRIM(LEFT(LTRIM(RTRIM([FullName])), LEN(LTRIM(RTRIM([FullName]))) - (CHARINDEX(N' ', REVERSE(LTRIM(RTRIM([FullName]))) + N' ') - 1)));
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FamilyName",
                table: "Catechists");

            migrationBuilder.DropColumn(
                name: "GivenName",
                table: "Catechists");
        }
    }
}
