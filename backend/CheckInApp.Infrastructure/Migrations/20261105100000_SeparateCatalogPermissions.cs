using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace CheckInApp.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261105100000_SeparateCatalogPermissions")]
public class SeparateCatalogPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE AspNetRoles SET Permissions = Permissions | CAST(CASE WHEN (Permissions & 2) <> 0 THEN 2147483648 ELSE 0 END AS bigint) | CAST(CASE WHEN (Permissions & 8) <> 0 THEN 4294967296 ELSE 0 END AS bigint) | CAST(CASE WHEN (Permissions & 32) <> 0 THEN 8589934592 ELSE 0 END AS bigint) | CAST(CASE WHEN (Permissions & 128) <> 0 THEN 17179869184 ELSE 0 END AS bigint) | CAST(CASE WHEN Name = 'Admin' THEN 103079215104 ELSE 0 END AS bigint)");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE AspNetRoles SET Permissions = Permissions & 2147483647");
    }
}
