using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EatWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddMensajesRapidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MensajeRapido",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RestauranteId = table.Column<int>(type: "integer", nullable: false),
                    EstacionId = table.Column<int>(type: "integer", nullable: false),
                    Texto = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MensajeRapido", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MensajeRapido_Restaurante_RestauranteId",
                        column: x => x.RestauranteId,
                        principalTable: "Restaurante",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MensajeRapido_Estacion_EstacionId",
                        column: x => x.EstacionId,
                        principalTable: "Estacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistorialMensajeRapido",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MensajeRapidoId = table.Column<int>(type: "integer", nullable: false),
                    RestauranteId = table.Column<int>(type: "integer", nullable: false),
                    EstacionId = table.Column<int>(type: "integer", nullable: false),
                    GarzonId = table.Column<string>(type: "text", nullable: false),
                    EnviadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Leido = table.Column<bool>(type: "boolean", nullable: false),
                    LeidoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialMensajeRapido", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialMensajeRapido_MensajeRapido_MensajeRapidoId",
                        column: x => x.MensajeRapidoId,
                        principalTable: "MensajeRapido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HistorialMensajeRapido_Restaurante_RestauranteId",
                        column: x => x.RestauranteId,
                        principalTable: "Restaurante",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistorialMensajeRapido_Estacion_EstacionId",
                        column: x => x.EstacionId,
                        principalTable: "Estacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistorialMensajeRapido_AspNetUsers_GarzonId",
                        column: x => x.GarzonId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MensajeRapido_RestauranteId_EstacionId",
                table: "MensajeRapido",
                columns: new[] { "RestauranteId", "EstacionId" });

            migrationBuilder.CreateIndex(
                name: "IX_HistorialMensajeRapido_MensajeRapidoId",
                table: "HistorialMensajeRapido",
                column: "MensajeRapidoId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialMensajeRapido_RestauranteId_EstacionId_EnviadoEn",
                table: "HistorialMensajeRapido",
                columns: new[] { "RestauranteId", "EstacionId", "EnviadoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_HistorialMensajeRapido_GarzonId",
                table: "HistorialMensajeRapido",
                column: "GarzonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistorialMensajeRapido");

            migrationBuilder.DropTable(
                name: "MensajeRapido");
        }
    }
}
