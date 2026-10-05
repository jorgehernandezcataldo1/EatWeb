using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EatWeb.Migrations;

public partial class Fase1EstacionesObligatorias : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Categorias_Estaciones_EstacionId",
            table: "Categorias");

        migrationBuilder.DropForeignKey(
            name: "FK_DetallesPedidos_Estaciones_EstacionId",
            table: "DetallesPedidos");

        migrationBuilder.Sql(@"
            INSERT INTO ""Estaciones"" (""RestauranteId"", ""Nombre"", ""Activa"", ""Orden"")
            SELECT ""Id"", 'Cocina', TRUE, 1
            FROM ""Restaurantes""
            ON CONFLICT (""RestauranteId"", ""Nombre"") DO NOTHING;

            INSERT INTO ""Estaciones"" (""RestauranteId"", ""Nombre"", ""Activa"", ""Orden"")
            SELECT ""Id"", 'Bar', TRUE, 2
            FROM ""Restaurantes""
            ON CONFLICT (""RestauranteId"", ""Nombre"") DO NOTHING;

            UPDATE ""Categorias"" c
            SET ""EstacionId"" = e.""Id""
            FROM ""Estaciones"" e
            WHERE c.""EstacionId"" IS NULL
              AND e.""RestauranteId"" = c.""RestauranteId""
              AND e.""Nombre"" = CASE
                    WHEN LOWER(c.""Nombre"") LIKE '%bebida%' THEN 'Bar'
                    ELSE 'Cocina'
                  END;

            UPDATE ""DetallesPedidos"" d
            SET ""EstacionId"" = c.""EstacionId"",
                ""EstacionNombre"" = e.""Nombre""
            FROM ""Productos"" p
            JOIN ""Categorias"" c ON c.""Id"" = p.""CategoriaId""
            JOIN ""Estaciones"" e ON e.""Id"" = c.""EstacionId""
            WHERE d.""EstacionId"" IS NULL
              AND d.""ProductoId"" = p.""Id"";

            UPDATE ""DetallesPedidos"" d
            SET ""EstacionId"" = e.""Id"",
                ""EstacionNombre"" = e.""Nombre""
            FROM ""Pedidos"" p
            JOIN ""MesaSesiones"" s ON s.""Id"" = p.""MesaSesionId""
            JOIN ""Mesas"" m ON m.""Id"" = s.""MesaId""
            JOIN ""Estaciones"" e
              ON e.""RestauranteId"" = m.""RestauranteId""
             AND e.""Nombre"" = 'Cocina'
            WHERE d.""EstacionId"" IS NULL
              AND d.""PedidoId"" = p.""Id"";
        ");

        migrationBuilder.AlterColumn<int>(
            name: "EstacionId",
            table: "Categorias",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.AlterColumn<int>(
            name: "EstacionId",
            table: "DetallesPedidos",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.AddForeignKey(
            name: "FK_Categorias_Estaciones_EstacionId",
            table: "Categorias",
            column: "EstacionId",
            principalTable: "Estaciones",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_DetallesPedidos_Estaciones_EstacionId",
            table: "DetallesPedidos",
            column: "EstacionId",
            principalTable: "Estaciones",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Categorias_Estaciones_EstacionId",
            table: "Categorias");

        migrationBuilder.DropForeignKey(
            name: "FK_DetallesPedidos_Estaciones_EstacionId",
            table: "DetallesPedidos");

        migrationBuilder.AlterColumn<int>(
            name: "EstacionId",
            table: "Categorias",
            type: "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AlterColumn<int>(
            name: "EstacionId",
            table: "DetallesPedidos",
            type: "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AddForeignKey(
            name: "FK_Categorias_Estaciones_EstacionId",
            table: "Categorias",
            column: "EstacionId",
            principalTable: "Estaciones",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_DetallesPedidos_Estaciones_EstacionId",
            table: "DetallesPedidos",
            column: "EstacionId",
            principalTable: "Estaciones",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }
}
