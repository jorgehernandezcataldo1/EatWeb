using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EatWeb.Migrations;

public partial class EstacionesYEstadoPorItem : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Estaciones",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                RestauranteId = table.Column<int>(type: "integer", nullable: false),
                Nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Activa = table.Column<bool>(type: "boolean", nullable: false),
                Orden = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Estaciones", x => x.Id);
                table.ForeignKey("FK_Estaciones_Restaurantes_RestauranteId", x => x.RestauranteId, "Restaurantes", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddColumn<int>(name: "EstacionId", table: "Categorias", type: "integer", nullable: true);
        migrationBuilder.AddColumn<int>(name: "EstacionId", table: "DetallesPedidos", type: "integer", nullable: true);
        migrationBuilder.AddColumn<string>(name: "EstacionNombre", table: "DetallesPedidos", type: "character varying(80)", maxLength: 80, nullable: false, defaultValue: "Cocina");
        migrationBuilder.AddColumn<string>(name: "Estado", table: "DetallesPedidos", type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Pendiente");
        migrationBuilder.AddColumn<DateTime>(name: "FechaInicioPreparacion", table: "DetallesPedidos", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "FechaListo", table: "DetallesPedidos", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "FechaEntregado", table: "DetallesPedidos", type: "timestamp with time zone", nullable: true);

        migrationBuilder.CreateIndex(name: "IX_Estaciones_RestauranteId_Nombre", table: "Estaciones", columns: new[] { "RestauranteId", "Nombre" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Categorias_EstacionId", table: "Categorias", column: "EstacionId");
        migrationBuilder.CreateIndex(name: "IX_DetallesPedidos_EstacionId", table: "DetallesPedidos", column: "EstacionId");

        migrationBuilder.AddForeignKey(name: "FK_Categorias_Estaciones_EstacionId", table: "Categorias", column: "EstacionId", principalTable: "Estaciones", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
        migrationBuilder.AddForeignKey(name: "FK_DetallesPedidos_Estaciones_EstacionId", table: "DetallesPedidos", column: "EstacionId", principalTable: "Estaciones", principalColumn: "Id", onDelete: ReferentialAction.SetNull);

        migrationBuilder.Sql(@"
            INSERT INTO ""Estaciones"" (""RestauranteId"", ""Nombre"", ""Activa"", ""Orden"")
            SELECT ""Id"", 'Cocina', TRUE, 1 FROM ""Restaurantes""
            ON CONFLICT (""RestauranteId"", ""Nombre"") DO NOTHING;
            INSERT INTO ""Estaciones"" (""RestauranteId"", ""Nombre"", ""Activa"", ""Orden"")
            SELECT ""Id"", 'Bar', TRUE, 2 FROM ""Restaurantes""
            ON CONFLICT (""RestauranteId"", ""Nombre"") DO NOTHING;

            UPDATE ""Categorias"" c
            SET ""EstacionId"" = e.""Id""
            FROM ""Estaciones"" e
            WHERE e.""RestauranteId"" = c.""RestauranteId""
              AND e.""Nombre"" = CASE WHEN LOWER(c.""Nombre"") LIKE '%bebida%' THEN 'Bar' ELSE 'Cocina' END;

            UPDATE ""DetallesPedidos"" d
            SET ""Estado"" = p.""Estado"",
                ""EstacionId"" = c.""EstacionId"",
                ""EstacionNombre"" = COALESCE(e.""Nombre"", 'Cocina')
            FROM ""Pedidos"" p, ""Productos"" pr, ""Categorias"" c
            LEFT JOIN ""Estaciones"" e ON e.""Id"" = c.""EstacionId""
            WHERE d.""PedidoId"" = p.""Id""
              AND d.""ProductoId"" = pr.""Id""
              AND pr.""CategoriaId"" = c.""Id"";
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Categorias_Estaciones_EstacionId", table: "Categorias");
        migrationBuilder.DropForeignKey(name: "FK_DetallesPedidos_Estaciones_EstacionId", table: "DetallesPedidos");
        migrationBuilder.DropTable(name: "Estaciones");
        migrationBuilder.DropIndex(name: "IX_Categorias_EstacionId", table: "Categorias");
        migrationBuilder.DropIndex(name: "IX_DetallesPedidos_EstacionId", table: "DetallesPedidos");
        migrationBuilder.DropColumn(name: "EstacionId", table: "Categorias");
        migrationBuilder.DropColumn(name: "EstacionId", table: "DetallesPedidos");
        migrationBuilder.DropColumn(name: "EstacionNombre", table: "DetallesPedidos");
        migrationBuilder.DropColumn(name: "Estado", table: "DetallesPedidos");
        migrationBuilder.DropColumn(name: "FechaInicioPreparacion", table: "DetallesPedidos");
        migrationBuilder.DropColumn(name: "FechaListo", table: "DetallesPedidos");
        migrationBuilder.DropColumn(name: "FechaEntregado", table: "DetallesPedidos");
    }
}
