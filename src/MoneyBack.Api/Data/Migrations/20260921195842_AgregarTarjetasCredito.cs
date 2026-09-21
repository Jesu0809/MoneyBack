using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MoneyBack.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTarjetasCredito : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TarjetaCreditoId",
                table: "MovimientosDiaADia",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TarjetasCredito",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    DiaCorte = table.Column<int>(type: "integer", nullable: false),
                    Activa = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TarjetasCredito", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TarjetasCredito_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PagosTarjeta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TarjetaCreditoId = table.Column<int>(type: "integer", nullable: false),
                    Monto = table.Column<decimal>(type: "numeric", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagosTarjeta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PagosTarjeta_TarjetasCredito_TarjetaCreditoId",
                        column: x => x.TarjetaCreditoId,
                        principalTable: "TarjetasCredito",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosDiaADia_TarjetaCreditoId",
                table: "MovimientosDiaADia",
                column: "TarjetaCreditoId");

            migrationBuilder.CreateIndex(
                name: "IX_PagosTarjeta_TarjetaCreditoId",
                table: "PagosTarjeta",
                column: "TarjetaCreditoId");

            migrationBuilder.CreateIndex(
                name: "IX_TarjetasCredito_UsuarioId",
                table: "TarjetasCredito",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosDiaADia_TarjetasCredito_TarjetaCreditoId",
                table: "MovimientosDiaADia",
                column: "TarjetaCreditoId",
                principalTable: "TarjetasCredito",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosDiaADia_TarjetasCredito_TarjetaCreditoId",
                table: "MovimientosDiaADia");

            migrationBuilder.DropTable(
                name: "PagosTarjeta");

            migrationBuilder.DropTable(
                name: "TarjetasCredito");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosDiaADia_TarjetaCreditoId",
                table: "MovimientosDiaADia");

            migrationBuilder.DropColumn(
                name: "TarjetaCreditoId",
                table: "MovimientosDiaADia");
        }
    }
}
