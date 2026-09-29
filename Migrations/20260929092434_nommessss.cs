using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EatWeb.Migrations
{
    /// <inheritdoc />
    public partial class nommessss : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Restaurantes_RestauranteId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_RestauranteId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RestauranteId",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<int>(
                name: "CadenaId",
                table: "Restaurantes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Cadenas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    LogoUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cadenas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RestaurantesMiembros",
                columns: table => new
                {
                    RestauranteId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Rol = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FechaAsignacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantesMiembros", x => new { x.RestauranteId, x.UsuarioId });
                    table.ForeignKey(
                        name: "FK_RestaurantesMiembros_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RestaurantesMiembros_Restaurantes_RestauranteId",
                        column: x => x.RestauranteId,
                        principalTable: "Restaurantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CadenasMiembros",
                columns: table => new
                {
                    CadenaId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Rol = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FechaAsignacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CadenasMiembros", x => new { x.CadenaId, x.UsuarioId });
                    table.ForeignKey(
                        name: "FK_CadenasMiembros_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CadenasMiembros_Cadenas_CadenaId",
                        column: x => x.CadenaId,
                        principalTable: "Cadenas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Restaurantes_CadenaId",
                table: "Restaurantes",
                column: "CadenaId");

            migrationBuilder.CreateIndex(
                name: "IX_CadenasMiembros_UsuarioId",
                table: "CadenasMiembros",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantesMiembros_UsuarioId",
                table: "RestaurantesMiembros",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Restaurantes_Cadenas_CadenaId",
                table: "Restaurantes",
                column: "CadenaId",
                principalTable: "Cadenas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Restaurantes_Cadenas_CadenaId",
                table: "Restaurantes");

            migrationBuilder.DropTable(
                name: "CadenasMiembros");

            migrationBuilder.DropTable(
                name: "RestaurantesMiembros");

            migrationBuilder.DropTable(
                name: "Cadenas");

            migrationBuilder.DropIndex(
                name: "IX_Restaurantes_CadenaId",
                table: "Restaurantes");

            migrationBuilder.DropColumn(
                name: "CadenaId",
                table: "Restaurantes");

            migrationBuilder.AddColumn<int>(
                name: "RestauranteId",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_RestauranteId",
                table: "AspNetUsers",
                column: "RestauranteId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Restaurantes_RestauranteId",
                table: "AspNetUsers",
                column: "RestauranteId",
                principalTable: "Restaurantes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
