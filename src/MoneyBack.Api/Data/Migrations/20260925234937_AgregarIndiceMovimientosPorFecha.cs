using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyBack.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarIndiceMovimientosPorFecha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MovimientosDiaADia_UsuarioId",
                table: "MovimientosDiaADia");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosDiaADia_UsuarioId_Fecha",
                table: "MovimientosDiaADia",
                columns: new[] { "UsuarioId", "Fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MovimientosDiaADia_UsuarioId_Fecha",
                table: "MovimientosDiaADia");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosDiaADia_UsuarioId",
                table: "MovimientosDiaADia",
                column: "UsuarioId");
        }
    }
}
