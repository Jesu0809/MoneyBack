using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyBack.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MetasLibresConSuPropioRedondeo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El orden importa: primero se crean las columnas nuevas, después
            // se copia lo que había, y solo al final se borra lo viejo. EF
            // genera los DropColumn de primeras, lo que habría borrado el
            // tipo de cada meta y los porcentajes del hogar antes de poder
            // leerlos — y con ellos, cómo estaba repartido el ahorro de una
            // pareja que ya lleva meses usando esto.

            migrationBuilder.AddColumn<bool>(
                name: "EsFondoEmergencia",
                table: "MetasAhorro",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Icono",
                table: "MetasAhorro",
                type: "text",
                nullable: false,
                defaultValue: "🎯");

            migrationBuilder.AddColumn<decimal>(
                name: "PorcentajeRedondeo",
                table: "MetasAhorro",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            // Tipo 1 era Emergencia; 0 era Apartamento.
            migrationBuilder.Sql("""
                UPDATE "MetasAhorro" SET "EsFondoEmergencia" = true WHERE "Tipo" = 1;
                UPDATE "MetasAhorro" SET "Icono" = '🛟' WHERE "Tipo" = 1;
                UPDATE "MetasAhorro" SET "Icono" = '🏠' WHERE "Tipo" = 0;
                """);

            // El reparto del vuelto pasa del hogar a cada meta, conservando
            // exactamente los porcentajes que la pareja tenía configurados.
            migrationBuilder.Sql("""
                UPDATE "MetasAhorro" m
                SET "PorcentajeRedondeo" = CASE
                        WHEN m."Tipo" = 1 THEN h."PorcentajeRedondeoEmergencia"
                        ELSE h."PorcentajeRedondeoApartamento"
                    END
                FROM "Hogares" h
                WHERE m."HogarId" = h."Id";
                """);

            migrationBuilder.DropColumn(name: "Tipo", table: "MetasAhorro");
            migrationBuilder.DropColumn(name: "PorcentajeRedondeoEmergencia", table: "InvitacionesHogar");
            migrationBuilder.DropColumn(name: "PorcentajeRedondeoApartamento", table: "Hogares");
            migrationBuilder.DropColumn(name: "PorcentajeRedondeoEmergencia", table: "Hogares");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsFondoEmergencia",
                table: "MetasAhorro");

            migrationBuilder.DropColumn(
                name: "Icono",
                table: "MetasAhorro");

            migrationBuilder.DropColumn(
                name: "PorcentajeRedondeo",
                table: "MetasAhorro");

            migrationBuilder.AddColumn<int>(
                name: "Tipo",
                table: "MetasAhorro",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PorcentajeRedondeoEmergencia",
                table: "InvitacionesHogar",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PorcentajeRedondeoApartamento",
                table: "Hogares",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PorcentajeRedondeoEmergencia",
                table: "Hogares",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
