using EatWeb.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EatWeb.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261007022000_SectoresMesas")]
public partial class SectoresMesas : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "OrdenEnSector",
            table: "Mesas",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "SectorId",
            table: "Mesas",
            type: "integer",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "Sectores",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                RestauranteId = table.Column<int>(type: "integer", nullable: false),
                Nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Orden = table.Column<int>(type: "integer", nullable: false),
                Activo = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Sectores", x => x.Id);
                table.ForeignKey(
                    name: "FK_Sectores_Restaurantes_RestauranteId",
                    column: x => x.RestauranteId,
                    principalTable: "Restaurantes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Mesas_SectorId_OrdenEnSector",
            table: "Mesas",
            columns: new[] { "SectorId", "OrdenEnSector" });

        migrationBuilder.CreateIndex(
            name: "IX_Sectores_RestauranteId_Nombre",
            table: "Sectores",
            columns: new[] { "RestauranteId", "Nombre" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_Mesas_Sectores_SectorId",
            table: "Mesas",
            column: "SectorId",
            principalTable: "Sectores",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Mesas_Sectores_SectorId",
            table: "Mesas");

        migrationBuilder.DropTable(name: "Sectores");

        migrationBuilder.DropIndex(
            name: "IX_Mesas_SectorId_OrdenEnSector",
            table: "Mesas");

        migrationBuilder.DropColumn(name: "OrdenEnSector", table: "Mesas");
        migrationBuilder.DropColumn(name: "SectorId", table: "Mesas");
    }
}
