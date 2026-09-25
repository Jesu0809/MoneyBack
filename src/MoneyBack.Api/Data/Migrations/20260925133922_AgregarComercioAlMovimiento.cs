using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyBack.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarComercioAlMovimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Comercio",
                table: "MovimientosDiaADia",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            // Los gastos que ya habían entrado por el atajo guardaron el
            // comercio en la nota, porque la columna no existía. Se copia
            // para que sigan aprendiendo igual que antes; si no, lo ya
            // registrado quedaría mudo y corregirlo no enseñaría nada.
            //
            // Solo los de "Sin clasificar": esa categoría la pone únicamente
            // el atajo del banco, así que ahí la nota siempre es un comercio.
            // En cualquier otra, la nota bien puede ser algo que la persona
            // escribió a mano, y tomarla por un comercio es justo lo que esta
            // columna vino a evitar.
            migrationBuilder.Sql("""
                UPDATE "MovimientosDiaADia" m
                SET "Comercio" = m."Nota"
                FROM "Categorias" c
                WHERE m."CategoriaId" = c."Id"
                  AND c."Nombre" = 'Sin clasificar'
                  AND m."Nota" IS NOT NULL
                  AND m."Comercio" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Comercio",
                table: "MovimientosDiaADia");
        }
    }
}
