using System.Globalization;
using System.Security.Claims;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.DiaADia;
using MoneyBack.Api.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MoneyBack.Api.Endpoints;

public static class ReportesEndpoints
{
    public static void MapReportesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/reportes").WithTags("Reportes").RequireAuthorization();

        group.MapGet("/anual", async (int anio, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var (desde, hasta) = RangoDelAnio(anio);

            var movimientos = await db.MovimientosDiaADia
                .Include(m => m.Categoria)
                .Where(m => m.UsuarioId == usuarioId && m.Fecha >= desde && m.Fecha <= hasta)
                .ToListAsync();

            var porMes = Enumerable.Range(1, 12).Select(mes =>
            {
                var delMes = movimientos.Where(m => m.Fecha.ToLocalTime().Month == mes).ToList();
                return new TotalPorMes(
                    mes,
                    delMes.Where(m => m.Categoria.Tipo == TipoCategoria.Ingreso).Sum(m => m.Monto),
                    delMes.Where(m => m.Categoria.Tipo == TipoCategoria.Gasto).Sum(m => m.Monto));
            }).ToList();

            var gastosPorCategoria = movimientos
                .Where(m => m.Categoria.Tipo == TipoCategoria.Gasto)
                .GroupBy(m => m.Categoria)
                .Select(g => new TotalPorCategoria(g.Key.Id, g.Key.Nombre, g.Key.Icono, g.Sum(m => m.Monto)))
                .OrderByDescending(t => t.Total)
                .ToList();

            var totalIngresos = movimientos.Where(m => m.Categoria.Tipo == TipoCategoria.Ingreso).Sum(m => m.Monto);
            var totalGastos = movimientos.Where(m => m.Categoria.Tipo == TipoCategoria.Gasto).Sum(m => m.Monto);

            return Results.Ok(new ResumenAnualResponse(anio, totalIngresos, totalGastos, porMes, gastosPorCategoria));
        });

        group.MapGet("/exportar/excel", async (DateTime? desde, DateTime? hasta, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var movimientos = await ObtenerMovimientosAsync(desde, hasta, principal, db);

            using var workbook = new XLWorkbook();
            var hoja = workbook.Worksheets.Add("Movimientos");

            hoja.Cell(1, 1).Value = "Fecha";
            hoja.Cell(1, 2).Value = "Tipo";
            hoja.Cell(1, 3).Value = "Categoría";
            hoja.Cell(1, 4).Value = "Monto";
            hoja.Cell(1, 5).Value = "Nota";
            hoja.Row(1).Style.Font.Bold = true;

            var fila = 2;
            foreach (var m in movimientos)
            {
                hoja.Cell(fila, 1).Value = m.Fecha.ToLocalTime().ToString("yyyy-MM-dd");
                hoja.Cell(fila, 2).Value = m.Categoria.Tipo == TipoCategoria.Ingreso ? "Ingreso" : "Gasto";
                hoja.Cell(fila, 3).Value = m.Categoria.Nombre;
                hoja.Cell(fila, 4).Value = m.Monto;
                hoja.Cell(fila, 5).Value = m.Nota ?? "";
                fila++;
            }

            hoja.Cell(fila, 3).Value = "Total ingresos";
            hoja.Cell(fila, 4).Value = movimientos.Where(m => m.Categoria.Tipo == TipoCategoria.Ingreso).Sum(m => m.Monto);
            hoja.Cell(fila + 1, 3).Value = "Total gastos";
            hoja.Cell(fila + 1, 4).Value = movimientos.Where(m => m.Categoria.Tipo == TipoCategoria.Gasto).Sum(m => m.Monto);
            hoja.Range(fila, 3, fila + 1, 4).Style.Font.Bold = true;

            hoja.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return Results.File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"moneyback-{NombreArchivo(desde, hasta)}.xlsx");
        });

        group.MapGet("/exportar/pdf", async (DateTime? desde, DateTime? hasta, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var movimientos = await ObtenerMovimientosAsync(desde, hasta, principal, db);
            var ingresos = movimientos.Where(m => m.Categoria.Tipo == TipoCategoria.Ingreso).ToList();
            var gastos = movimientos.Where(m => m.Categoria.Tipo == TipoCategoria.Gasto).ToList();
            var totalIngresos = ingresos.Sum(m => m.Monto);
            var totalGastos = gastos.Sum(m => m.Monto);
            var balance = totalIngresos - totalGastos;

            var porCategoria = gastos
                .GroupBy(m => m.Categoria.Nombre)
                .Select(g => (Nombre: g.Key, Total: g.Sum(m => m.Monto), Cuantos: g.Count()))
                .OrderByDescending(g => g.Total)
                .ToList();

            var generadoEl = DateTime.UtcNow.AddHours(-5); // Bogotá: el servidor corre en UTC.

            var documento = Document.Create(contenedor =>
            {
                contenedor.Page(pagina =>
                {
                    pagina.Size(PageSizes.A4);
                    pagina.Margin(34);
                    pagina.DefaultTextStyle(e => e.FontSize(9.5f).FontColor(Tinta));

                    pagina.Header().Element(c => Encabezado(c, desde, hasta, generadoEl));

                    pagina.Content().PaddingTop(18).Column(col =>
                    {
                        col.Spacing(18);

                        col.Item().Element(c => Resumen(c, totalIngresos, totalGastos, balance));

                        if (movimientos.Count == 0)
                        {
                            // Un PDF con una tabla vacía parece un archivo roto.
                            col.Item().PaddingTop(40).AlignCenter().Text(
                                "No hay movimientos registrados en este período.")
                                .FontSize(11).FontColor(Gris);
                            return;
                        }

                        if (porCategoria.Count > 0)
                        {
                            col.Item().Element(c => EnQueSeFue(c, porCategoria, totalGastos));
                        }

                        col.Item().Element(c => Detalle(c, movimientos));
                    });

                    pagina.Footer().Element(Pie);
                });
            });

            return Results.File(documento.GeneratePdf(), "application/pdf", $"moneyback-{NombreArchivo(desde, hasta)}.pdf");
        });
    }

    // --- Paleta. Los mismos verdes y rojos de la app, para que el PDF se
    // --- reconozca como suyo y no como la salida de una herramienta aparte.
    private static readonly Color Verde = Color.FromHex("#2f6f5e");
    private static readonly Color VerdeSuave = Color.FromHex("#e4f0ec");
    private static readonly Color Rojo = Color.FromHex("#b5493f");
    private static readonly Color RojoSuave = Color.FromHex("#fbe9e7");
    private static readonly Color Tinta = Color.FromHex("#22201c");
    private static readonly Color Gris = Color.FromHex("#756f66");
    private static readonly Color Linea = Color.FromHex("#e8e5df");
    private static readonly Color Cebra = Color.FromHex("#faf9f7");

    private static void Encabezado(IContainer contenedor, DateTime? desde, DateTime? hasta, DateTime generadoEl)
    {
        contenedor.Column(col =>
        {
            col.Item().Row(fila =>
            {
                fila.RelativeItem().Column(izq =>
                {
                    izq.Item().Text("MoneyBack").FontSize(20).Bold().FontColor(Verde);
                    izq.Item().PaddingTop(1).Text(DescripcionRango(desde, hasta)).FontSize(11).FontColor(Tinta);
                });

                fila.ConstantItem(150).AlignRight().Column(der =>
                {
                    der.Item().AlignRight().Text("Generado").FontSize(7.5f).FontColor(Gris);
                    der.Item().AlignRight().Text(FechaLarga(generadoEl)).FontSize(9).FontColor(Gris);
                });
            });

            col.Item().PaddingTop(10).LineHorizontal(1.4f).LineColor(Verde);
        });
    }

    /// <summary>
    /// Lo primero que uno busca al abrir un reporte es el balance. Antes
    /// estaba al pie de la última página, después de cientos de filas.
    /// </summary>
    private static void Resumen(IContainer contenedor, decimal ingresos, decimal gastos, decimal balance)
    {
        contenedor.Row(fila =>
        {
            fila.Spacing(10);
            fila.RelativeItem().Element(c => Cifra(c, "Ingresos", FormatoPesos(ingresos), Verde, VerdeSuave));
            fila.RelativeItem().Element(c => Cifra(c, "Gastos", FormatoPesos(gastos), Rojo, RojoSuave));
            fila.RelativeItem().Element(c => Cifra(
                c, "Balance",
                // El mismo signo menos tipográfico que llevan las filas del
                // detalle: con el guion ASCII de FormatoPesos, el balance se
                // veía distinto a todo lo demás del documento.
                (balance < 0 ? "−" : "") + FormatoPesos(Math.Abs(balance)),
                balance >= 0 ? Verde : Rojo,
                balance >= 0 ? VerdeSuave : RojoSuave));
        });
    }

    private static void Cifra(IContainer contenedor, string rotulo, string valor, Color color, Color fondo) =>
        contenedor.Background(fondo).Padding(12).Column(col =>
        {
            col.Item().Text(rotulo.ToUpperInvariant()).FontSize(7.5f).Bold().FontColor(Gris).LetterSpacing(0.08f);
            col.Item().PaddingTop(3).Text(valor).FontSize(15).Bold().FontColor(color);
        });

    /// <summary>
    /// La pregunta que sigue al balance es en qué se fue. Con una barra al
    /// lado del número se ve de un vistazo cuál categoría pesa, algo que una
    /// columna de cifras no muestra.
    /// </summary>
    private static void EnQueSeFue(
        IContainer contenedor,
        List<(string Nombre, decimal Total, int Cuantos)> porCategoria,
        decimal totalGastos)
    {
        contenedor.Column(col =>
        {
            col.Item().Text("En qué se fue").FontSize(12).Bold();
            col.Item().PaddingTop(8).Column(filas =>
            {
                filas.Spacing(6);
                foreach (var (nombre, total, cuantos) in porCategoria.Take(10))
                {
                    var porcentaje = totalGastos > 0 ? (float)(total / totalGastos) : 0f;
                    filas.Item().Row(fila =>
                    {
                        fila.ConstantItem(130).Text(nombre).FontSize(9.5f);
                        fila.ConstantItem(34).Text($"{porcentaje * 100:0}%").FontSize(8.5f).FontColor(Gris);
                        fila.RelativeItem().AlignMiddle().Height(7).Background(Cebra)
                            .Row(barra =>
                            {
                                // Las barras con 0% no pueden pedir ancho 0:
                                // QuestPDF rechaza un RelativeItem sin peso.
                                barra.RelativeItem(Math.Max(porcentaje, 0.0001f)).Background(Verde);
                                barra.RelativeItem(Math.Max(1 - porcentaje, 0.0001f));
                            });
                        fila.ConstantItem(90).AlignRight().Text(FormatoPesos(total)).FontSize(9.5f).Bold();
                        fila.ConstantItem(52).AlignRight().Text($"{cuantos} mov.").FontSize(8).FontColor(Gris);
                    });
                }

                if (porCategoria.Count > 10)
                {
                    var resto = porCategoria.Skip(10).Sum(c => c.Total);
                    filas.Item().PaddingTop(2).Text(
                        $"y {porCategoria.Count - 10} categorías más por {FormatoPesos(resto)}")
                        .FontSize(8.5f).FontColor(Gris);
                }
            });
        });
    }

    private static void Detalle(IContainer contenedor, List<MovimientoDiaADia> movimientos)
    {
        var hayDonde = movimientos.Any(m => !string.IsNullOrWhiteSpace(m.Comercio) || !string.IsNullOrWhiteSpace(m.Nota));

        contenedor.Column(col =>
        {
            col.Item().Text($"Movimiento por movimiento ({movimientos.Count})").FontSize(12).Bold();

            col.Item().PaddingTop(8).Table(tabla =>
            {
                tabla.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(66);   // Fecha
                    c.RelativeColumn(3);    // Categoría
                    if (hayDonde) c.RelativeColumn(4);
                    c.ConstantColumn(82);   // Monto
                });

                tabla.Header(encabezado =>
                {
                    static IContainer Celda(IContainer c) =>
                        c.BorderBottom(1).BorderColor(Tinta).PaddingVertical(5).PaddingHorizontal(4);

                    encabezado.Cell().Element(Celda).Text("Fecha").FontSize(8).Bold();
                    encabezado.Cell().Element(Celda).Text("Categoría").FontSize(8).Bold();
                    if (hayDonde) encabezado.Cell().Element(Celda).Text("Dónde").FontSize(8).Bold();
                    encabezado.Cell().Element(Celda).AlignRight().Text("Monto").FontSize(8).Bold();
                });

                var fila = 0;
                foreach (var m in movimientos)
                {
                    // Cebra: con doscientas filas seguidas el ojo se salta de
                    // renglón y se lee el monto de otro movimiento.
                    var fondo = fila++ % 2 == 1 ? Cebra : Colors.White;
                    var esIngreso = m.Categoria.Tipo == TipoCategoria.Ingreso;

                    IContainer Celda(IContainer c) =>
                        c.Background(fondo).BorderBottom(0.5f).BorderColor(Linea)
                         .PaddingVertical(4).PaddingHorizontal(4);

                    tabla.Cell().Element(Celda).Text(FechaCorta(m.Fecha.ToLocalTime())).FontSize(8.5f);
                    tabla.Cell().Element(Celda).Text(m.Categoria.Nombre).FontSize(8.5f);
                    if (hayDonde)
                    {
                        tabla.Cell().Element(Celda).Text(m.Comercio ?? m.Nota ?? "—").FontSize(8.5f).FontColor(Gris);
                    }
                    tabla.Cell().Element(Celda).AlignRight()
                        // Con el signo se distingue un ingreso de un gasto sin
                        // tener que leer la categoría — y sirve impreso en
                        // blanco y negro, donde el color no dice nada.
                        .Text((esIngreso ? "+" : "−") + FormatoPesos(m.Monto))
                        .FontSize(8.5f).Bold().FontColor(esIngreso ? Verde : Rojo);
                }
            });
        });
    }

    private static void Pie(IContainer contenedor) =>
        contenedor.PaddingTop(10).Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Linea);
            col.Item().PaddingTop(5).Row(fila =>
            {
                fila.RelativeItem().Text("MoneyBack").FontSize(8).FontColor(Gris);
                fila.RelativeItem().AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(e => e.FontSize(8).FontColor(Gris));
                    t.Span("Página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            });
        });

    /// <summary>
    /// Los meses se escriben a mano y no con formato de fecha: el servidor
    /// corre en UTC y con cultura invariante, así que "MMM" daba "Dec" y
    /// "Aug" en un reporte que solo se lee en español.
    /// </summary>
    private static readonly string[] MesesLargos =
    [
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"
    ];

    private static readonly string[] MesesCortos =
        ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];

    private static string FechaLarga(DateTime f) =>
        $"{f.Day} de {MesesLargos[f.Month - 1]} de {f.Year}, {(f.Hour % 12 == 0 ? 12 : f.Hour % 12)}:{f.Minute:D2} {(f.Hour < 12 ? "a.m." : "p.m.")}";

    private static string FechaCorta(DateTime f) => $"{f.Day:D2} {MesesCortos[f.Month - 1]} {f.Year % 100:D2}";

    private static string DescripcionRango(DateTime? desde, DateTime? hasta) =>
        desde is not null && hasta is not null
            ? $"Del {desde.Value.Day} de {MesesLargos[desde.Value.Month - 1]} al {hasta.Value.Day} de {MesesLargos[hasta.Value.Month - 1]} de {hasta.Value.Year}"
            : "Todo el historial";

    private static string FormatoPesos(decimal valor)
    {
        var redondeado = Math.Round(valor, 0, MidpointRounding.AwayFromZero);
        var digitos = Math.Abs(redondeado).ToString("F0", CultureInfo.InvariantCulture);
        var agrupado = string.Empty;
        for (var i = 0; i < digitos.Length; i++)
        {
            if (i > 0 && (digitos.Length - i) % 3 == 0) agrupado += ".";
            agrupado += digitos[i];
        }
        return (redondeado < 0 ? "-$" : "$") + agrupado;
    }

    private static async Task<List<MovimientoDiaADia>> ObtenerMovimientosAsync(
        DateTime? desde, DateTime? hasta, ClaimsPrincipal principal, ApplicationDbContext db)
    {
        var usuarioId = principal.GetUsuarioId();
        var query = db.MovimientosDiaADia.Include(m => m.Categoria).Where(m => m.UsuarioId == usuarioId);

        if (desde is not null) query = query.Where(m => m.Fecha >= AComoUtc(desde.Value));
        if (hasta is not null) query = query.Where(m => m.Fecha <= AComoUtc(hasta.Value));

        return await query.OrderBy(m => m.Fecha).ToListAsync();
    }

    private static (DateTime desde, DateTime hasta) RangoDelAnio(int anio) => (
        new DateTime(anio, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        new DateTime(anio, 12, 31, 23, 59, 59, DateTimeKind.Utc));

    private static string NombreArchivo(DateTime? desde, DateTime? hasta) =>
        desde is not null && hasta is not null
            ? $"{desde.Value:yyyy-MM-dd}_a_{hasta.Value:yyyy-MM-dd}"
            : "reporte";

    private static DateTime AComoUtc(DateTime valor) =>
        valor.Kind == DateTimeKind.Utc ? valor : DateTime.SpecifyKind(valor, DateTimeKind.Utc);
}
