using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EatWeb.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Esta migración es un placeholder. La BD ya tiene todas las tablas.
            // Las tables fueron creadas previamente y no necesitan ser recreadas.
            // Esta migración simplemente marca el punto de inicio del historial de migraciones.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No se pueden deshacer las operaciones de la BD inicial
        }
    }
}
