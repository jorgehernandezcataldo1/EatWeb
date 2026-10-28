using EatWeb.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EatWeb.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261028011000_PagoRequestFingerprint")]
public class PagoRequestFingerprint : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RequestFingerprint",
            table: "Pagos",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RequestFingerprint",
            table: "Pagos");
    }
}
