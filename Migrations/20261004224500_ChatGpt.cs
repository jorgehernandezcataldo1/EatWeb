using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EatWeb.Migrations
{
    /// <inheritdoc />
    public partial class ChatGpt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "MesaSesiones",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GarzonId",
                table: "MesaSesiones",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Cuentas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MesaSesionId = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaCierre = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cuentas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cuentas_MesaSesiones_MesaSesionId",
                        column: x => x.MesaSesionId,
                        principalTable: "MesaSesiones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pagos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CuentaId = table.Column<int>(type: "integer", nullable: false),
                    ComensalId = table.Column<int>(type: "integer", nullable: true),
                    Estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Metodo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Proveedor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReferenciaExterna = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    Propina = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaConfirmacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pagos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pagos_Comensales_ComensalId",
                        column: x => x.ComensalId,
                        principalTable: "Comensales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Pagos_Cuentas_CuentaId",
                        column: x => x.CuentaId,
                        principalTable: "Cuentas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PagoDetalles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PagoId = table.Column<int>(type: "integer", nullable: false),
                    DetallePedidoId = table.Column<int>(type: "integer", nullable: false),
                    MontoAsignado = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagoDetalles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PagoDetalles_DetallesPedidos_DetallePedidoId",
                        column: x => x.DetallePedidoId,
                        principalTable: "DetallesPedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PagoDetalles_Pagos_PagoId",
                        column: x => x.PagoId,
                        principalTable: "Pagos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MesaSesiones_GarzonId",
                table: "MesaSesiones",
                column: "GarzonId");

            migrationBuilder.CreateIndex(
                name: "IX_Cuentas_MesaSesionId",
                table: "Cuentas",
                column: "MesaSesionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PagoDetalles_DetallePedidoId",
                table: "PagoDetalles",
                column: "DetallePedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_PagoDetalles_PagoId_DetallePedidoId",
                table: "PagoDetalles",
                columns: new[] { "PagoId", "DetallePedidoId" });

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_ComensalId",
                table: "Pagos",
                column: "ComensalId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_CuentaId",
                table: "Pagos",
                column: "CuentaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_IdempotencyKey",
                table: "Pagos",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MesaSesiones_AspNetUsers_GarzonId",
                table: "MesaSesiones",
                column: "GarzonId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MesaSesiones_AspNetUsers_GarzonId",
                table: "MesaSesiones");

            migrationBuilder.DropTable(
                name: "PagoDetalles");

            migrationBuilder.DropTable(
                name: "Pagos");

            migrationBuilder.DropTable(
                name: "Cuentas");

            migrationBuilder.DropIndex(
                name: "IX_MesaSesiones_GarzonId",
                table: "MesaSesiones");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "MesaSesiones");

            migrationBuilder.DropColumn(
                name: "GarzonId",
                table: "MesaSesiones");
        }
    }
}
