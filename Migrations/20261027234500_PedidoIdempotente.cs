using EatWeb.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EatWeb.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261027234500_PedidoIdempotente")]
public class PedidoIdempotente : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "RequestId",
            table: "Pedidos",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RequestFingerprint",
            table: "Pedidos",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Pedidos_ComensalId_RequestId",
            table: "Pedidos",
            columns: new[] { "ComensalId", "RequestId" },
            unique: true,
            filter: "\"RequestId\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Pedidos_ComensalId_RequestId",
            table: "Pedidos");

        migrationBuilder.DropColumn(
            name: "RequestFingerprint",
            table: "Pedidos");

        migrationBuilder.DropColumn(
            name: "RequestId",
            table: "Pedidos");
    }
}
