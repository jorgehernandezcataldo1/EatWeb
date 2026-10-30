using EatWeb.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EatWeb.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261029235500_CadenaActivo")]
public class CadenaActivo : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "Activo",
            table: "Cadenas",
            type: "boolean",
            nullable: false,
            defaultValue: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Activo", table: "Cadenas");
    }
}
