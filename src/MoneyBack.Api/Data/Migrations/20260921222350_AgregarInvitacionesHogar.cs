using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MoneyBack.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarInvitacionesHogar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvitacionesHogar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvitadorId = table.Column<int>(type: "integer", nullable: false),
                    InvitadoId = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    AplicaTope150 = table.Column<bool>(type: "boolean", nullable: false),
                    PorcentajeRedondeoEmergencia = table.Column<decimal>(type: "numeric", nullable: false),
                    PorcentajeRedondeoApartamento = table.Column<decimal>(type: "numeric", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaResolucion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvitacionesHogar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvitacionesHogar_Usuarios_InvitadoId",
                        column: x => x.InvitadoId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvitacionesHogar_Usuarios_InvitadorId",
                        column: x => x.InvitadorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvitacionesHogar_InvitadoId",
                table: "InvitacionesHogar",
                column: "InvitadoId");

            migrationBuilder.CreateIndex(
                name: "IX_InvitacionesHogar_InvitadorId",
                table: "InvitacionesHogar",
                column: "InvitadorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvitacionesHogar");
        }
    }
}
