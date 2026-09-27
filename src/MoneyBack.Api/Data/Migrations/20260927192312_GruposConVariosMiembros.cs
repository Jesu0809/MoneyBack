using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MoneyBack.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class GruposConVariosMiembros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Igual que en la migración de metas, EF pone los DropColumn de
            // primeras: habría borrado Usuario1Id y Usuario2Id antes de
            // copiarlos a la tabla de miembros, y con eso a las dos personas
            // del hogar que ya existe. Primero se crea, después se copia, y
            // solo al final se borra.

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCreacion",
                table: "Hogares",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<string>(
                name: "Nombre",
                table: "Hogares",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "Nuestro hogar");

            migrationBuilder.CreateTable(
                name: "MiembrosHogar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HogarId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    FechaIngreso = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EsAdministrador = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MiembrosHogar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MiembrosHogar_Hogares_HogarId",
                        column: x => x.HogarId,
                        principalTable: "Hogares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MiembrosHogar_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Las dos personas del hogar pasan a ser miembros. Quien figuraba
            // de primero queda como administrador: era quien lo había creado.
            migrationBuilder.Sql("""
                INSERT INTO "MiembrosHogar" ("HogarId", "UsuarioId", "FechaIngreso", "EsAdministrador")
                SELECT "Id", "Usuario1Id", NOW(), true FROM "Hogares";

                INSERT INTO "MiembrosHogar" ("HogarId", "UsuarioId", "FechaIngreso", "EsAdministrador")
                SELECT "Id", "Usuario2Id", NOW(), false FROM "Hogares"
                WHERE "Usuario2Id" <> "Usuario1Id";
                """);

            // Las invitaciones pendientes apuntaban a "armemos un hogar" y
            // ahora tienen que apuntar a uno concreto. Las que quedaron sin
            // grupo al que referirse se descartan: aceptar una de esas no
            // significaría nada.
            migrationBuilder.AddColumn<int>(
                name: "HogarId",
                table: "InvitacionesHogar",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE "InvitacionesHogar" i
                SET "HogarId" = h."Id"
                FROM "Hogares" h
                WHERE h."Usuario1Id" = i."InvitadorId" OR h."Usuario2Id" = i."InvitadorId";

                DELETE FROM "InvitacionesHogar" WHERE "HogarId" = 0;
                """);

            migrationBuilder.DropIndex(name: "IX_Hogares_Usuario1Id", table: "Hogares");
            migrationBuilder.DropIndex(name: "IX_Hogares_Usuario2Id", table: "Hogares");
            migrationBuilder.DropColumn(name: "AplicaTope150", table: "InvitacionesHogar");
            migrationBuilder.DropColumn(name: "PorcentajeRedondeoApartamento", table: "InvitacionesHogar");
            migrationBuilder.DropColumn(name: "Usuario1Id", table: "Hogares");
            migrationBuilder.DropColumn(name: "Usuario2Id", table: "Hogares");

            migrationBuilder.CreateIndex(
                name: "IX_InvitacionesHogar_HogarId",
                table: "InvitacionesHogar",
                column: "HogarId");

            migrationBuilder.CreateIndex(
                name: "IX_MiembrosHogar_HogarId_UsuarioId",
                table: "MiembrosHogar",
                columns: new[] { "HogarId", "UsuarioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MiembrosHogar_UsuarioId",
                table: "MiembrosHogar",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_InvitacionesHogar_Hogares_HogarId",
                table: "InvitacionesHogar",
                column: "HogarId",
                principalTable: "Hogares",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvitacionesHogar_Hogares_HogarId",
                table: "InvitacionesHogar");

            migrationBuilder.DropTable(
                name: "MiembrosHogar");

            migrationBuilder.DropIndex(
                name: "IX_InvitacionesHogar_HogarId",
                table: "InvitacionesHogar");

            migrationBuilder.DropColumn(
                name: "HogarId",
                table: "InvitacionesHogar");

            migrationBuilder.DropColumn(
                name: "FechaCreacion",
                table: "Hogares");

            migrationBuilder.DropColumn(
                name: "Nombre",
                table: "Hogares");

            migrationBuilder.AddColumn<bool>(
                name: "AplicaTope150",
                table: "InvitacionesHogar",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PorcentajeRedondeoApartamento",
                table: "InvitacionesHogar",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Usuario1Id",
                table: "Hogares",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Usuario2Id",
                table: "Hogares",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Hogares_Usuario1Id",
                table: "Hogares",
                column: "Usuario1Id");

            migrationBuilder.CreateIndex(
                name: "IX_Hogares_Usuario2Id",
                table: "Hogares",
                column: "Usuario2Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Hogares_Usuarios_Usuario1Id",
                table: "Hogares",
                column: "Usuario1Id",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Hogares_Usuarios_Usuario2Id",
                table: "Hogares",
                column: "Usuario2Id",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
