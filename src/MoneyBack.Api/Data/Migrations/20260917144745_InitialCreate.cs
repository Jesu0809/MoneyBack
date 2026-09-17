using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MoneyBack.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Hogares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Usuario1Id = table.Column<int>(type: "integer", nullable: false),
                    Usuario2Id = table.Column<int>(type: "integer", nullable: false),
                    PorcentajeRedondeoEmergencia = table.Column<decimal>(type: "numeric", nullable: false),
                    PorcentajeRedondeoApartamento = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hogares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hogares_Usuarios_Usuario1Id",
                        column: x => x.Usuario1Id,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Hogares_Usuarios_Usuario2Id",
                        column: x => x.Usuario2Id,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MetasAhorro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HogarId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    MontoObjetivo = table.Column<decimal>(type: "numeric", nullable: false),
                    FechaObjetivoEstimada = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetasAhorro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetasAhorro_Hogares_HogarId",
                        column: x => x.HogarId,
                        principalTable: "Hogares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosMeta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MetaAhorroId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Monto = table.Column<decimal>(type: "numeric", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Nota = table.Column<string>(type: "text", nullable: true),
                    EsAutomatico = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosMeta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosMeta_MetasAhorro_MetaAhorroId",
                        column: x => x.MetaAhorroId,
                        principalTable: "MetasAhorro",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MovimientosMeta_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hogares_Usuario1Id",
                table: "Hogares",
                column: "Usuario1Id");

            migrationBuilder.CreateIndex(
                name: "IX_Hogares_Usuario2Id",
                table: "Hogares",
                column: "Usuario2Id");

            migrationBuilder.CreateIndex(
                name: "IX_MetasAhorro_HogarId",
                table: "MetasAhorro",
                column: "HogarId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosMeta_MetaAhorroId",
                table: "MovimientosMeta",
                column: "MetaAhorroId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosMeta_UsuarioId",
                table: "MovimientosMeta",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovimientosMeta");

            migrationBuilder.DropTable(
                name: "MetasAhorro");

            migrationBuilder.DropTable(
                name: "Hogares");

            migrationBuilder.DropTable(
                name: "Usuarios");
        }
    }
}
