using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheckInApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CatechistScanCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatechistCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatechistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatechistCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatechistCredentials_Catechists_CatechistId",
                        column: x => x.CatechistId,
                        principalTable: "Catechists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatechistCredentials_CatechistId",
                table: "CatechistCredentials",
                column: "CatechistId");

            migrationBuilder.CreateIndex(
                name: "IX_CatechistCredentials_TokenHash",
                table: "CatechistCredentials",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatechistCredentials");
        }
    }
}
