using EatWeb.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace EatWeb.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261030030000_ComercialPlanes")]
public sealed class ComercialPlanes : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable(name: "Funcionalidades", columns: t => new
        {
            Id = t.Column<int>(type: "integer", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            Clave = t.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
            Nombre = t.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
            Activa = t.Column<bool>(type: "boolean", nullable: false)
        }, constraints: t => t.PrimaryKey("PK_Funcionalidades", x => x.Id));
        m.CreateIndex(name: "IX_Funcionalidades_Clave", table: "Funcionalidades", column: "Clave", unique: true);

        m.CreateTable(name: "PlanesComerciales", columns: t => new
        {
            Id = t.Column<int>(type: "integer", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            Nombre = t.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
            Activo = t.Column<bool>(type: "boolean", nullable: false),
            MaxRestaurantes = t.Column<int>(type: "integer", nullable: true)
        }, constraints: t => t.PrimaryKey("PK_PlanesComerciales", x => x.Id));

        m.CreateTable(name: "PlanesFuncionalidades", columns: t => new
        {
            PlanComercialId = t.Column<int>(type: "integer", nullable: false),
            FuncionalidadId = t.Column<int>(type: "integer", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_PlanesFuncionalidades", x => new { x.PlanComercialId, x.FuncionalidadId });
            t.ForeignKey("FK_PlanesFuncionalidades_Plan", x => x.PlanComercialId, "PlanesComerciales", "Id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("FK_PlanesFuncionalidades_Funcionalidad", x => x.FuncionalidadId, "Funcionalidades", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex(name: "IX_PlanesFuncionalidades_FuncionalidadId", table: "PlanesFuncionalidades", column: "FuncionalidadId");

        m.CreateTable(name: "SuscripcionesComerciales", columns: t => new
        {
            Id = t.Column<int>(type: "integer", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            PlanComercialId = t.Column<int>(type: "integer", nullable: false),
            CadenaId = t.Column<int>(type: "integer", nullable: true),
            RestauranteId = t.Column<int>(type: "integer", nullable: true),
            Activa = t.Column<bool>(type: "boolean", nullable: false),
            VenceUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: true)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_SuscripcionesComerciales", x => x.Id);
            t.ForeignKey("FK_SuscripcionesComerciales_Plan", x => x.PlanComercialId, "PlanesComerciales", "Id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_SuscripcionesComerciales_Cadena", x => x.CadenaId, "Cadenas", "Id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_SuscripcionesComerciales_Restaurante", x => x.RestauranteId, "Restaurantes", "Id", onDelete: ReferentialAction.Restrict);
            t.CheckConstraint("CK_Suscripcion_Comercial_Scope", "(\"CadenaId\" IS NOT NULL AND \"RestauranteId\" IS NULL) OR (\"CadenaId\" IS NULL AND \"RestauranteId\" IS NOT NULL)");
        });
        m.CreateIndex(name: "IX_SuscripcionesComerciales_CadenaId", table: "SuscripcionesComerciales", column: "CadenaId", unique: true, filter: "\"CadenaId\" IS NOT NULL");
        m.CreateIndex(name: "IX_SuscripcionesComerciales_RestauranteId", table: "SuscripcionesComerciales", column: "RestauranteId", unique: true, filter: "\"RestauranteId\" IS NOT NULL");
        m.CreateIndex(name: "IX_SuscripcionesComerciales_PlanComercialId", table: "SuscripcionesComerciales", column: "PlanComercialId");

        m.CreateTable(name: "RestaurantesFuncionalidadesOverride", columns: t => new
        {
            RestauranteId = t.Column<int>(type: "integer", nullable: false),
            FuncionalidadId = t.Column<int>(type: "integer", nullable: false),
            Habilitada = t.Column<bool>(type: "boolean", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_RestaurantesFuncionalidadesOverride", x => new { x.RestauranteId, x.FuncionalidadId });
            t.ForeignKey("FK_RestaurantesFuncionalidadesOverride_Restaurante", x => x.RestauranteId, "Restaurantes", "Id", onDelete: ReferentialAction.Cascade);
            t.ForeignKey("FK_RestaurantesFuncionalidadesOverride_Funcionalidad", x => x.FuncionalidadId, "Funcionalidades", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex(name: "IX_RestaurantesFuncionalidadesOverride_FuncionalidadId", table: "RestaurantesFuncionalidadesOverride", column: "FuncionalidadId");
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropTable(name: "RestaurantesFuncionalidadesOverride");
        m.DropTable(name: "SuscripcionesComerciales");
        m.DropTable(name: "PlanesFuncionalidades");
        m.DropTable(name: "PlanesComerciales");
        m.DropTable(name: "Funcionalidades");
    }
}
