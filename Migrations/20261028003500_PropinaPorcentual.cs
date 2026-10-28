using EatWeb.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EatWeb.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261028003500_PropinaPorcentual")]
public class PropinaPorcentual : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "PropinaBase",
            table: "Pagos",
            type: "numeric(18,0)",
            precision: 18,
            scale: 0,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "PropinaPorcentaje",
            table: "Pagos",
            type: "numeric(5,2)",
            precision: 5,
            scale: 2,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PropinaBase",
            table: "Pagos");

        migrationBuilder.DropColumn(
            name: "PropinaPorcentaje",
            table: "Pagos");
    }
}
