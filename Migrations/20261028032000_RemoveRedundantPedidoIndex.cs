using EatWeb.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EatWeb.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261028032000_RemoveRedundantPedidoIndex")]
public class RemoveRedundantPedidoIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Pedidos_ComensalId",
            table: "Pedidos");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Pedidos_ComensalId",
            table: "Pedidos",
            column: "ComensalId");
    }
}
