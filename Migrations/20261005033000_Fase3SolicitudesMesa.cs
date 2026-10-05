using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EatWeb.Migrations
{
    public partial class Fase3SolicitudesMesa : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SolicitudesMesa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MesaSesionId = table.Column<int>(type: "integer", nullable: false),
                    ComensalId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Mensaje = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaResolucion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AtendidaPorId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudesMesa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitudesMesa_AspNetUsers_AtendidaPorId",
                        column: x => x.AtendidaPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SolicitudesMesa_Comensales_ComensalId",
                        column: x => x.ComensalId,
                        principalTable: "Comensales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SolicitudesMesa_MesaSesiones_MesaSesionId",
                        column: x => x.MesaSesionId,
                        principalTable: "MesaSesiones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesMesa_AtendidaPorId",
                table: "SolicitudesMesa",
                column: "AtendidaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesMesa_ComensalId_Estado",
                table: "SolicitudesMesa",
                columns: new[] { "ComensalId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesMesa_ComensalId_Tipo",
                table: "SolicitudesMesa",
                columns: new[] { "ComensalId", "Tipo" },
                unique: true,
                filter: "\"Estado\" = 'Pendiente'");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesMesa_MesaSesionId_Estado_FechaCreacion",
                table: "SolicitudesMesa",
                columns: new[] { "MesaSesionId", "Estado", "FechaCreacion" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SolicitudesMesa");
        }
    }
}
