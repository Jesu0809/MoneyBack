using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models.DiaADia;
using MoneyBack.Api.Models.Tarjetas;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Endpoints;

public static class MovimientosDiaADiaEndpoints
{
    public static void MapMovimientosDiaADiaEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/movimientos-diaadia").WithTags("DiaADia").RequireAuthorization();

        group.MapGet("/", async (DateTime? desde, DateTime? hasta, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var query = db.MovimientosDiaADia.Include(m => m.Categoria).Where(m => m.UsuarioId == usuarioId);

            if (desde is not null) query = query.Where(m => m.Fecha >= AComoUtc(desde.Value));
            if (hasta is not null) query = query.Where(m => m.Fecha <= AComoUtc(hasta.Value));

            var movimientos = await query
                .OrderByDescending(m => m.Fecha)
                .Select(m => new MovimientoDiaADiaResponse(
                    m.Id, m.CategoriaId, m.Categoria.Nombre, m.Categoria.Icono, m.Categoria.Tipo,
                    m.Monto, m.Fecha, m.Nota, m.Comercio, m.RedondeoAplicado,
                    m.TarjetaCreditoId, m.TarjetaCredito != null ? m.TarjetaCredito.Nombre : null))
                .ToListAsync();

            return Results.Ok(movimientos);
        });

        group.MapGet("/resumen", async (DateTime? desde, DateTime? hasta, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var query = db.MovimientosDiaADia.Include(m => m.Categoria).Where(m => m.UsuarioId == usuarioId);

            if (desde is not null) query = query.Where(m => m.Fecha >= AComoUtc(desde.Value));
            if (hasta is not null) query = query.Where(m => m.Fecha <= AComoUtc(hasta.Value));

            var movimientos = await query.ToListAsync();

            var totalIngresos = movimientos.Where(m => m.Categoria.Tipo == TipoCategoria.Ingreso).Sum(m => m.Monto);
            var totalGastos = movimientos.Where(m => m.Categoria.Tipo == TipoCategoria.Gasto).Sum(m => m.Monto);

            var gastosPorCategoria = movimientos
                .Where(m => m.Categoria.Tipo == TipoCategoria.Gasto)
                .GroupBy(m => m.Categoria)
                .Select(g => new TotalPorCategoria(g.Key.Id, g.Key.Nombre, g.Key.Icono, g.Sum(m => m.Monto)))
                .OrderByDescending(t => t.Total)
                .ToList();

            return Results.Ok(new ResumenDiaADiaResponse(totalIngresos, totalGastos, totalIngresos - totalGastos, gastosPorCategoria));
        });

        group.MapPost("/", async (CrearMovimientoDiaADiaRequest request, ClaimsPrincipal principal, ApplicationDbContext db, PushNotificationSender sender) =>
        {
            if (request.Monto <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["monto"] = ["El monto debe ser mayor a cero."]
                });
            }

            var usuarioId = principal.GetUsuarioId();
            var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Id == request.CategoriaId && c.UsuarioId == usuarioId);
            if (categoria is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["categoriaId"] = ["La categoría no existe."]
                });
            }

            TarjetaCredito? tarjeta = null;
            if (request.TarjetaCreditoId is not null)
            {
                if (categoria.Tipo == TipoCategoria.Ingreso)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["tarjetaCreditoId"] = ["Un ingreso no se puede pagar con tarjeta de crédito."]
                    });
                }

                tarjeta = await db.TarjetasCredito.FirstOrDefaultAsync(t => t.Id == request.TarjetaCreditoId && t.UsuarioId == usuarioId);
                if (tarjeta is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["tarjetaCreditoId"] = ["La tarjeta no existe."]
                    });
                }
            }

            var movimiento = new MovimientoDiaADia
            {
                UsuarioId = usuarioId,
                CategoriaId = categoria.Id,
                Monto = request.Monto,
                Fecha = request.Fecha ?? DateTime.UtcNow,
                Nota = request.Nota,
                TarjetaCreditoId = tarjeta?.Id
            };
            db.MovimientosDiaADia.Add(movimiento);

            if (categoria.Tipo == TipoCategoria.Gasto)
            {
                await RedondeoService.AplicarSiCorrespondeAsync(movimiento, db);
            }

            await db.SaveChangesAsync();

            // Después de guardar, nunca antes: avisar de un tope por un gasto
            // que no alcanzó a registrarse sería mentira.
            if (categoria.Tipo == TipoCategoria.Gasto)
            {
                await AlertasPresupuestoService.RevisarAsync(usuarioId, categoria.Id, db, sender);
            }

            return Results.Created($"/api/movimientos-diaadia/{movimiento.Id}", new MovimientoDiaADiaResponse(
                movimiento.Id, categoria.Id, categoria.Nombre, categoria.Icono, categoria.Tipo,
                movimiento.Monto, movimiento.Fecha, movimiento.Nota, movimiento.Comercio, movimiento.RedondeoAplicado,
                tarjeta?.Id, tarjeta?.Nombre));
        });

        group.MapPut("/{id:int}", async (int id, ActualizarMovimientoDiaADiaRequest request, ClaimsPrincipal principal, ApplicationDbContext db, PushNotificationSender sender) =>
        {
            if (request.Monto <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["monto"] = ["El monto debe ser mayor a cero."]
                });
            }

            var usuarioId = principal.GetUsuarioId();
            var movimiento = await db.MovimientosDiaADia.FirstOrDefaultAsync(m => m.Id == id && m.UsuarioId == usuarioId);
            if (movimiento is null) return Results.NotFound();

            var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Id == request.CategoriaId && c.UsuarioId == usuarioId);
            if (categoria is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["categoriaId"] = ["La categoría no existe."]
                });
            }

            TarjetaCredito? tarjeta = null;
            if (request.TarjetaCreditoId is not null)
            {
                if (categoria.Tipo == TipoCategoria.Ingreso)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["tarjetaCreditoId"] = ["Un ingreso no se puede pagar con tarjeta de crédito."]
                    });
                }

                tarjeta = await db.TarjetasCredito.FirstOrDefaultAsync(t => t.Id == request.TarjetaCreditoId && t.UsuarioId == usuarioId);
                if (tarjeta is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["tarjetaCreditoId"] = ["La tarjeta no existe."]
                    });
                }
            }

            var categoriaAnterior = movimiento.CategoriaId;

            movimiento.CategoriaId = categoria.Id;
            movimiento.Monto = request.Monto;
            movimiento.Nota = request.Nota;
            movimiento.TarjetaCreditoId = tarjeta?.Id;
            if (request.Fecha is not null) movimiento.Fecha = request.Fecha.Value;

            // Corregir la categoría de un gasto con comercio es la persona
            // enseñando dónde va ese comercio. Se guarda para que el atajo no
            // vuelva a preguntar por el mismo sitio.
            var (comercioAprendido, reclasificados) = categoriaAnterior != categoria.Id
                ? await AprenderComercioAsync(movimiento, categoria, usuarioId, db)
                : (null, 0);

            // El redondeo NO se recalcula a propósito. Ese aporte ya entró a la
            // meta del hogar y no guarda referencia al gasto que lo originó, así
            // que no hay forma confiable de encontrarlo y ajustarlo. Y aunque la
            // hubiera: devolver plata de una meta compartida porque alguien
            // corrigió una categoría sorprendería a la pareja más de lo que
            // ayudaría. Corregir el gasto deja el aporte donde está.
            await db.SaveChangesAsync();

            // La categoría nueva acaba de recibir este monto y puede haber
            // cruzado su tope. La anterior bajó, y de eso no hay nada que
            // avisar: nadie necesita saber que dejó de estar en problemas.
            if (categoriaAnterior != categoria.Id && categoria.Tipo == TipoCategoria.Gasto)
            {
                await AlertasPresupuestoService.RevisarAsync(usuarioId, categoria.Id, db, sender);
            }

            return Results.Ok(new MovimientoActualizadoResponse(
                new MovimientoDiaADiaResponse(
                    movimiento.Id, categoria.Id, categoria.Nombre, categoria.Icono, categoria.Tipo,
                    movimiento.Monto, movimiento.Fecha, movimiento.Nota, movimiento.Comercio, movimiento.RedondeoAplicado,
                    tarjeta?.Id, tarjeta?.Nombre),
                comercioAprendido,
                reclasificados));
        });

        // Rescate para cuando el atajo no alcanzó a registrar: si el API
        // estaba caído o la automatización no se disparó, ese gasto se
        // pierde y nadie se entera hasta que cuadra cuentas. Acá se pegan los
        // mensajes del banco —los que sean— y entran todos de una.
        //
        // Autenticado con la sesión normal, no con el token del atajo: esto
        // lo usa una persona desde la app, no una automatización.
        group.MapPost("/desde-sms", async (
            TextoBancoRequest request, ClaimsPrincipal principal,
            ApplicationDbContext db, PushNotificationSender sender) =>
        {
            var usuarioId = principal.GetUsuarioId();

            var categorias = await db.Categorias
                .Where(c => c.UsuarioId == usuarioId && c.Activa)
                .ToListAsync();

            var mensajes = InterpretadorTexto.SepararMensajes(request.Texto);
            if (mensajes.Count == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["texto"] = ["Pega al menos un mensaje del banco."]
                });
            }

            var resultados = new List<ResultadoMensajeBanco>();
            var categoriasAfectadas = new HashSet<int>();

            foreach (var mensaje in mensajes)
            {
                var interpretacion = InterpretadorTexto.Interpretar(
                    mensaje, categorias, categoriaForzada: CategoriasPredefinidas.SinClasificar);

                if (!interpretacion.Exito)
                {
                    resultados.Add(new ResultadoMensajeBanco(Recortar(mensaje), false, 0, null, interpretacion.Razon));
                    continue;
                }

                var categoria = interpretacion.Categoria!;
                var comercio = InterpretadorTexto.ExtraerComercio(mensaje);

                if (comercio is not null)
                {
                    var clave = InterpretadorTexto.Normalizar(comercio);
                    var aprendido = await db.ComerciosCategoria
                        .Include(c => c.Categoria)
                        .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId && c.Comercio == clave);

                    if (aprendido is not null && aprendido.Categoria.Activa) categoria = aprendido.Categoria;
                }

                // Los duplicados son el riesgo real de pegar a mano: puede que
                // el atajo sí registrara alguno y la persona pegue todos por
                // si acaso. Mismo comercio, mismo monto y mismo día se toma
                // como el mismo gasto — cobrar dos veces lo mismo destruye la
                // confianza en los números mucho más que perder uno.
                var hoy = DateTime.UtcNow.Date;
                var yaEsta = await db.MovimientosDiaADia.AnyAsync(m =>
                    m.UsuarioId == usuarioId
                    && m.Monto == interpretacion.Monto
                    && m.Comercio == comercio
                    && m.Fecha >= hoy);

                if (yaEsta)
                {
                    resultados.Add(new ResultadoMensajeBanco(
                        Recortar(mensaje), false, interpretacion.Monto, comercio, "Ya estaba registrado."));
                    continue;
                }

                var movimiento = new MovimientoDiaADia
                {
                    UsuarioId = usuarioId,
                    CategoriaId = categoria.Id,
                    Monto = interpretacion.Monto,
                    Nota = comercio,
                    Comercio = comercio
                };
                db.MovimientosDiaADia.Add(movimiento);

                if (categoria.Tipo == TipoCategoria.Gasto)
                {
                    await RedondeoService.AplicarSiCorrespondeAsync(movimiento, db);
                    categoriasAfectadas.Add(categoria.Id);
                }

                resultados.Add(new ResultadoMensajeBanco(
                    Recortar(mensaje), true, interpretacion.Monto, comercio, categoria.Nombre));
            }

            await db.SaveChangesAsync();

            foreach (var categoriaId in categoriasAfectadas)
            {
                await AlertasPresupuestoService.RevisarAsync(usuarioId, categoriaId, db, sender);
            }

            return Results.Ok(new RegistroDesdeSmsResponse(
                resultados.Count(r => r.Registrado), resultados));
        });

        group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal principal, ApplicationDbContext db) =>
        {
            var usuarioId = principal.GetUsuarioId();
            var movimiento = await db.MovimientosDiaADia.FirstOrDefaultAsync(m => m.Id == id && m.UsuarioId == usuarioId);
            if (movimiento is null) return Results.NotFound();

            db.MovimientosDiaADia.Remove(movimiento);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    /// <summary>
    /// Guarda que este comercio va en esta categoría y arrastra los gastos
    /// anteriores del mismo sitio que seguían sin clasificar.
    ///
    /// Se guía por el Comercio que informó el banco, nunca por la Nota: la
    /// nota es texto libre, y aprender de ella significaba que corregir un
    /// gasto anotado "almuerzo con Ana" creara un "comercio" con ese nombre.
    /// Un movimiento escrito a mano no tiene comercio, así que no enseña nada.
    ///
    /// Solo toca los que están sin clasificar: esos no tienen una decisión
    /// detrás que respetar, así que moverlos es completar lo que faltaba. Un
    /// gasto que la persona ya clasificó a mano se queda como está, aunque sea
    /// del mismo comercio — a veces se compra el mercado y a veces un regalo
    /// en el mismo lugar.
    /// </summary>
    private static async Task<(string? Comercio, int Reclasificados)> AprenderComercioAsync(
        MovimientoDiaADia movimiento, Categoria categoria, int usuarioId, ApplicationDbContext db)
    {
        if (string.IsNullOrWhiteSpace(movimiento.Comercio)) return (null, 0);

        var clave = InterpretadorTexto.Normalizar(movimiento.Comercio);
        if (clave.Length < 3) return (null, 0);

        var existente = await db.ComerciosCategoria
            .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId && c.Comercio == clave);

        if (existente is null)
        {
            db.ComerciosCategoria.Add(new ComercioCategoria
            {
                UsuarioId = usuarioId,
                Comercio = clave,
                CategoriaId = categoria.Id
            });
        }
        else
        {
            existente.CategoriaId = categoria.Id;
            existente.FechaAprendido = DateTime.UtcNow;
        }

        var sinClasificar = await db.Categorias
            .Where(c => c.UsuarioId == usuarioId && c.Nombre == CategoriasPredefinidas.SinClasificar)
            .Select(c => c.Id)
            .FirstOrDefaultAsync();

        if (sinClasificar == 0) return (movimiento.Comercio, 0);

        // Se traen los pendientes y se comparan acá, no en SQL, porque la
        // comparación tiene que ser la misma que produjo la llave: sin tildes
        // y sin mayúsculas. De otro modo "Café Juan" y "Cafe Juan" apuntarían
        // a lo mismo aprendido pero no se arrastrarían entre sí, y la persona
        // vería unos moverse y otros no sin ninguna razón visible. Son los
        // gastos sin clasificar de una sola cuenta: caben de sobra en memoria.
        var pendientes = await db.MovimientosDiaADia
            .Where(m => m.UsuarioId == usuarioId
                && m.Id != movimiento.Id
                && m.CategoriaId == sinClasificar
                && m.Comercio != null)
            .ToListAsync();

        var delMismoSitio = pendientes.Where(m => InterpretadorTexto.Normalizar(m.Comercio!) == clave).ToList();
        foreach (var pendiente in delMismoSitio) pendiente.CategoriaId = categoria.Id;

        return (movimiento.Comercio, delMismoSitio.Count);
    }

    /// <summary>
    /// Los query params desde/hasta llegan sin offset (Kind=Unspecified) y
    /// Npgsql exige Kind=Utc explícito para comparar contra una columna
    /// timestamptz — sin esto, cualquier filtro de fecha tira 500.
    /// </summary>
    private static string Recortar(string texto) =>
        texto.Length <= 70 ? texto : texto[..70] + "…";

    private static DateTime AComoUtc(DateTime valor) =>
        valor.Kind == DateTimeKind.Utc ? valor : DateTime.SpecifyKind(valor, DateTimeKind.Utc);
}
