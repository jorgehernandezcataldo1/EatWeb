using EatWeb.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EatWeb.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261028001000_HistorialEstadoDetallePedido")]
public class HistorialEstadoDetallePedidoMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "HistorialesEstadoDetallePedido",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                DetallePedidoId = table.Column<int>(type: "integer", nullable: false),
                EstadoAnterior = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                EstadoNuevo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UsuarioId = table.Column<string>(type: "text", nullable: true),
                Motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HistorialesEstadoDetallePedido", x => x.Id);
                table.ForeignKey(
                    name: "FK_HistorialesEstadoDetallePedido_AspNetUsers_UsuarioId",
                    column: x => x.UsuarioId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_HistorialesEstadoDetallePedido_DetallesPedidos_DetallePedidoId",
                    column: x => x.DetallePedidoId,
                    principalTable: "DetallesPedidos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_HistorialesEstadoDetallePedido_DetallePedidoId_Fecha",
            table: "HistorialesEstadoDetallePedido",
            columns: new[] { "DetallePedidoId", "Fecha" });

        migrationBuilder.CreateIndex(
            name: "IX_HistorialesEstadoDetallePedido_UsuarioId",
            table: "HistorialesEstadoDetallePedido",
            column: "UsuarioId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "HistorialesEstadoDetallePedido");
    }
}
