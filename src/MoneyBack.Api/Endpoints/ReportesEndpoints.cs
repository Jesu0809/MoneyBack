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
            var totalIngresos = movimientos.Where(m => m.Categoria.Tipo == TipoCategoria.Ingreso).Sum(m => m.Monto);
            var totalGastos = movimientos.Where(m => m.Categoria.Tipo == TipoCategoria.Gasto).Sum(m => m.Monto);

            var documento = Document.Create(contenedor =>
            {
                contenedor.Page(pagina =>
                {
                    pagina.Size(PageSizes.A4);
                    pagina.Margin(30);
                    pagina.DefaultTextStyle(estilo => estilo.FontSize(10));

                    pagina.Header().Column(col =>
                    {
                        col.Item().Text("Reporte MoneyBack").FontSize(18).Bold();
                        col.Item().Text(DescripcionRango(desde, hasta)).FontSize(11);
                    });

                    pagina.Content().PaddingTop(15).Table(tabla =>
                    {
                        tabla.ColumnsDefinition(columnas =>
                        {
                            columnas.RelativeColumn(2);
                            columnas.RelativeColumn(2);
                            columnas.RelativeColumn(3);
                            columnas.RelativeColumn(2);
                            columnas.RelativeColumn(4);
                        });

                        tabla.Header(encabezado =>
                        {
                            encabezado.Cell().Text("Fecha").Bold();
                            encabezado.Cell().Text("Tipo").Bold();
                            encabezado.Cell().Text("Categoría").Bold();
                            encabezado.Cell().Text("Monto").Bold();
                            encabezado.Cell().Text("Nota").Bold();
                        });

                        foreach (var m in movimientos)
                        {
                            tabla.Cell().Text(m.Fecha.ToLocalTime().ToString("yyyy-MM-dd"));
                            tabla.Cell().Text(m.Categoria.Tipo == TipoCategoria.Ingreso ? "Ingreso" : "Gasto");
                            tabla.Cell().Text(m.Categoria.Nombre);
                            tabla.Cell().Text(FormatoPesos(m.Monto));
                            tabla.Cell().Text(m.Nota ?? "");
                        }
                    });

                    pagina.Footer().PaddingTop(15).Column(col =>
                    {
                        col.Item().LineHorizontal(0.5f);
                        col.Item().Row(fila =>
                        {
                            fila.RelativeItem().Text($"Total ingresos: {FormatoPesos(totalIngresos)}").Bold();
                            fila.RelativeItem().AlignRight().Text($"Total gastos: {FormatoPesos(totalGastos)}").Bold();
                        });
                    });
                });
            });

            return Results.File(documento.GeneratePdf(), "application/pdf", $"moneyback-{NombreArchivo(desde, hasta)}.pdf");
        });
    }

    private static string DescripcionRango(DateTime? desde, DateTime? hasta) =>
        desde is not null && hasta is not null
            ? $"Del {desde.Value:d MMM yyyy} al {hasta.Value:d MMM yyyy}"
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
