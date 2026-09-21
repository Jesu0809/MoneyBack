using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyBack.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarDiasPagoUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiaPago1",
                table: "Usuarios",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiaPago2",
                table: "Usuarios",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimoResumenEnviado",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiaPago1",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "DiaPago2",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "UltimoResumenEnviado",
                table: "Usuarios");
        }
    }
}
