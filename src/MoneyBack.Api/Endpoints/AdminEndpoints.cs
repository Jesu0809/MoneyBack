using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MoneyBack.Api.Data;
using MoneyBack.Api.Dtos;
using MoneyBack.Api.Models;
using MoneyBack.Api.Models.Auth;
using MoneyBack.Api.Services;

namespace MoneyBack.Api.Endpoints;

/// <summary>
/// Todo lo administrativo (gestionar código de invitación, revocar sesiones,
/// listar cuentas). Deliberadamente NO expone MetaAhorro/MovimientoMeta ni
/// nada de plata de un hogar: eso solo lo ven los dos usuarios del hogar.
/// </summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization(policy => policy.RequireRole(Roles.SuperAdmin));

        // Un vistazo de si todo está en pie. Lo primero que uno quiere saber
        // al abrir un panel de administración no es la lista de cuentas: es
        // si algo se rompió.
        group.MapGet("/resumen", async (ApplicationDbContext db, UserManager<Usuario> userManager) =>
        {
            var administradores = (await userManager.GetUsersInRoleAsync(Roles.SuperAdmin)).Count;
            var ultimaLlamada = await db.LlamadasAtajo
                .OrderByDescending(l => l.Fecha)
                .Select(l => new { l.Fecha, l.Exito })
                .FirstOrDefaultAsync();

            // Las últimas 24 horas: un fallo de la semana pasada ya no dice
            // nada sobre si el atajo está funcionando hoy.
            var desdeAyer = DateTime.UtcNow.AddDays(-1);

            return Results.Ok(new ResumenAdminResponse(
                Cuentas: await db.Users.CountAsync(),
                Administradores: administradores,
                SesionesActivas: await db.RefreshTokens.CountAsync(t => t.RevocadoEn == null && t.ExpiraEn > DateTime.UtcNow),
                Movimientos: await db.MovimientosDiaADia.CountAsync(),
                MovimientosSinClasificar: await db.MovimientosDiaADia.CountAsync(m => m.Categoria.Nombre == "Sin clasificar"),
                Grupos: await db.Hogares.CountAsync(),
                MetasActivas: await db.MetasAhorro.CountAsync(m => m.Activa),
                UltimaLlamadaAtajo: ultimaLlamada?.Fecha,
                UltimaLlamadaAtajoFueBien: ultimaLlamada?.Exito ?? false,
                LlamadasAtajoFallidas: await db.LlamadasAtajo.CountAsync(l => !l.Exito && l.Fecha >= desdeAyer),
                NotificacionesSinLeer: await db.Notificaciones.CountAsync(n => n.LeidaEn == null)));
        });

        group.MapGet("/usuarios", async (
            ClaimsPrincipal principal, ApplicationDbContext db, UserManager<Usuario> userManager) =>
        {
            var yo = principal.GetUsuarioId();
            var usuarios = await userManager.Users.OrderBy(u => u.FechaCreacion).ToListAsync();

            // Los conteos se traen de una y se cruzan en memoria: son un
            // puñado de cuentas, y una consulta por cuenta serían decenas de
            // viajes a una base que está en otro estado.
            var sesiones = await db.RefreshTokens
                .Where(t => t.RevocadoEn == null && t.ExpiraEn > DateTime.UtcNow)
                .GroupBy(t => t.UsuarioId)
                .Select(g => new { g.Key, Cuantas = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Cuantas);

            var movimientos = await db.MovimientosDiaADia
                .GroupBy(m => m.UsuarioId)
                .Select(g => new { g.Key, Cuantos = g.Count(), Ultimo = g.Max(m => m.Fecha) })
                .ToListAsync();

            var grupos = await db.MiembrosHogar
                .GroupBy(m => m.UsuarioId)
                .Select(g => new { g.Key, Cuantos = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Cuantos);

            var respuesta = new List<CuentaAdminResponse>();
            foreach (var usuario in usuarios)
            {
                var roles = await userManager.GetRolesAsync(usuario);
                var suyos = movimientos.FirstOrDefault(m => m.Key == usuario.Id);

                respuesta.Add(new CuentaAdminResponse(
                    usuario.Id, usuario.Nombre, usuario.Email!, usuario.FechaCreacion, roles.ToList(),
                    SesionesActivas: sesiones.GetValueOrDefault(usuario.Id),
                    Movimientos: suyos?.Cuantos ?? 0,
                    Grupos: grupos.GetValueOrDefault(usuario.Id),
                    UltimaActividad: suyos?.Ultimo,
                    EsMiCuenta: usuario.Id == yo));
            }

            return Results.Ok(respuesta);
        });

        group.MapPost("/usuarios/{id:int}/dar-administracion", async (
            int id, ClaimsPrincipal principal, UserManager<Usuario> userManager,
            RoleManager<IdentityRole<int>> roleManager, ILoggerFactory loggerFactory) =>
        {
            var usuario = await userManager.FindByIdAsync(id.ToString());
            if (usuario is null) return Results.NotFound();

            if (!await roleManager.RoleExistsAsync(Roles.SuperAdmin))
            {
                await roleManager.CreateAsync(new IdentityRole<int>(Roles.SuperAdmin));
            }

            if (await userManager.IsInRoleAsync(usuario, Roles.SuperAdmin))
            {
                return Results.Conflict("Esa cuenta ya es administradora.");
            }

            await userManager.AddToRoleAsync(usuario, Roles.SuperAdmin);

            loggerFactory.CreateLogger("Admin").LogWarning(
                "El usuario {Admin} le dio la administración a la cuenta {Objetivo}.",
                principal.GetUsuarioId(), usuario.Id);

            return Results.NoContent();
        });

        group.MapPost("/usuarios/{id:int}/revocar-sesiones", async (int id, ApplicationDbContext db) =>
        {
            var tokensActivos = await db.RefreshTokens
                .Where(t => t.UsuarioId == id && t.RevocadoEn == null)
                .ToListAsync();

            foreach (var token in tokensActivos) token.RevocadoEn = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new { sesionesRevocadas = tokensActivos.Count });
        });

        // Le pone una contraseña temporal a una cuenta y cierra todas sus
        // sesiones. El administrador nunca ve la contraseña anterior —no
        // existe en ninguna parte, solo su hash— y la temporal se muestra
        // una sola vez, acá mismo, para pasársela a la persona.
        //
        // Se cierran las sesiones a propósito: si alguien entró con la
        // contraseña vieja, cambiarla sin sacarlo no sirve de nada. Y se
        // borran los códigos de recuperación pendientes porque después de
        // un reajuste administrativo ya no se sabe quién los tiene.
        group.MapPost("/usuarios/{id:int}/clave-temporal", async (
            int id,
            ClaimsPrincipal principal,
            UserManager<Usuario> userManager,
            ApplicationDbContext db,
            ILoggerFactory loggerFactory) =>
        {
            var usuario = await userManager.FindByIdAsync(id.ToString());
            if (usuario is null) return Results.NotFound();

            var temporal = GenerarClaveTemporal();

            var token = await userManager.GeneratePasswordResetTokenAsync(usuario);
            var resultado = await userManager.ResetPasswordAsync(usuario, token, temporal);

            if (!resultado.Succeeded)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["clave"] = resultado.Errors.Select(e => e.Description).ToArray()
                });
            }

            var sesiones = await db.RefreshTokens
                .Where(t => t.UsuarioId == usuario.Id && t.RevocadoEn == null)
                .ToListAsync();
            foreach (var sesion in sesiones) sesion.RevocadoEn = DateTime.UtcNow;

            var codigos = await db.CodigosRecuperacion.Where(c => c.UsuarioId == usuario.Id).ToListAsync();
            db.CodigosRecuperacion.RemoveRange(codigos);

            await db.SaveChangesAsync();

            // Queda en el registro del servidor: un cambio de contraseña
            // hecho por otra persona no puede ser invisible.
            loggerFactory.CreateLogger("Admin").LogWarning(
                "El usuario {Admin} le puso clave temporal a la cuenta {Objetivo} y cerró {Sesiones} sesión(es).",
                principal.GetUsuarioId(), usuario.Id, sesiones.Count);

            return Results.Ok(new ClaveTemporalResponse(temporal, sesiones.Count));
        });

        // Quitarle la administración a una cuenta. Hacía falta y no existía:
        // el rol se le daba a la primera cuenta registrada y no había forma
        // de retirárselo, así que una cuenta de prueba que lo recibiera por
        // accidente se quedaba con él para siempre. Pasó.
        group.MapPost("/usuarios/{id:int}/quitar-administracion", async (
            int id,
            ClaimsPrincipal principal,
            UserManager<Usuario> userManager,
            ApplicationDbContext db,
            ILoggerFactory loggerFactory) =>
        {
            var usuario = await userManager.FindByIdAsync(id.ToString());
            if (usuario is null) return Results.NotFound();

            // Quitársela a uno mismo dejaría el sistema sin nadie que pueda
            // devolverla: la única salida sería un secreto de Fly y un
            // reinicio. Se bloquea acá para que no sea un clic de distancia.
            if (id == principal.GetUsuarioId())
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["usuario"] = ["No puedes quitarte la administración a ti mismo. Pídeselo a otro administrador."]
                });
            }

            if (!await userManager.IsInRoleAsync(usuario, Roles.SuperAdmin))
            {
                return Results.Conflict("Esa cuenta no es administradora.");
            }

            await userManager.RemoveFromRoleAsync(usuario, Roles.SuperAdmin);

            // El rol viaja dentro del token de acceso, así que sin cerrar las
            // sesiones seguiría administrando hasta que el suyo caduque.
            var sesiones = await db.RefreshTokens
                .Where(t => t.UsuarioId == usuario.Id && t.RevocadoEn == null)
                .ToListAsync();
            foreach (var sesion in sesiones) sesion.RevocadoEn = DateTime.UtcNow;
            await db.SaveChangesAsync();

            loggerFactory.CreateLogger("Admin").LogWarning(
                "El usuario {Admin} le quitó la administración a la cuenta {Objetivo}.",
                principal.GetUsuarioId(), usuario.Id);

            return Results.Ok(new { sesionesCerradas = sesiones.Count });
        });

        // Qué se lleva por delante borrar una cuenta, ANTES de borrarla.
        //
        // Un borrado sin vista previa es un botón que uno no puede evaluar:
        // "eliminar cuenta" no dice si son cuatro movimientos de prueba o el
        // historial de dos años de alguien. Acá se cuenta todo primero.
        group.MapGet("/usuarios/{id:int}/que-se-borraria", async (
            int id, ClaimsPrincipal principal, ApplicationDbContext db, UserManager<Usuario> userManager) =>
        {
            var usuario = await userManager.FindByIdAsync(id.ToString());
            if (usuario is null) return Results.NotFound();

            var misGrupos = await db.MiembrosHogar
                .Where(m => m.UsuarioId == id)
                .Select(m => new { m.HogarId, m.Hogar.Nombre, Miembros = m.Hogar.Miembros.Count })
                .ToListAsync();

            // Un grupo donde era el último se queda sin nadie: se borra con
            // sus metas. Uno donde queda gente sigue vivo y solo sale.
            var seBorran = misGrupos.Where(g => g.Miembros <= 1).Select(g => g.Nombre).ToList();
            var saleDe = misGrupos.Where(g => g.Miembros > 1).Select(g => g.Nombre).ToList();

            var aportes = await db.MovimientosMeta.CountAsync(m => m.UsuarioId == id);

            // Si aportó plata a un grupo donde queda más gente, borrarlo le
            // cambiaría los totales al otro sin avisarle. Eso no se hace
            // desde un panel: que primero se resuelva entre ellos.
            var aportesEnGrupoCompartido = await db.MovimientosMeta
                .CountAsync(m => m.UsuarioId == id && m.MetaAhorro.Hogar.Miembros.Count > 1);

            string? bloqueo = null;
            if (id == principal.GetUsuarioId())
            {
                bloqueo = "No puedes borrar tu propia cuenta desde acá.";
            }
            else if (await userManager.IsInRoleAsync(usuario, Roles.SuperAdmin)
                     && (await userManager.GetUsersInRoleAsync(Roles.SuperAdmin)).Count <= 1)
            {
                bloqueo = "Es la única cuenta administradora. Dale la administración a otra antes de borrarla.";
            }
            else if (aportesEnGrupoCompartido > 0)
            {
                bloqueo = $"Tiene {aportesEnGrupoCompartido} aporte(s) en metas compartidas con otras personas. "
                          + "Borrarla les cambiaría el total sin avisarles: saca primero esos aportes o a esta cuenta del grupo.";
            }

            return Results.Ok(new QueSeBorrariaResponse(
                usuario.Nombre, usuario.Email!,
                Movimientos: await db.MovimientosDiaADia.CountAsync(m => m.UsuarioId == id),
                Categorias: await db.Categorias.CountAsync(c => c.UsuarioId == id),
                Aportes: aportes,
                Deudas: await db.Deudas.CountAsync(d => d.UsuarioId == id),
                Tarjetas: await db.TarjetasCredito.CountAsync(t => t.UsuarioId == id),
                CobrosFijos: await db.Suscripciones.CountAsync(x => x.UsuarioId == id),
                Presupuestos: await db.Presupuestos.CountAsync(x => x.UsuarioId == id),
                Notificaciones: await db.Notificaciones.CountAsync(n => n.UsuarioId == id),
                GruposQueSeBorran: seBorran,
                GruposDeLosQueSale: saleDe,
                Bloqueo: bloqueo));
        });

        group.MapDelete("/usuarios/{id:int}", async (
            int id, ClaimsPrincipal principal, ApplicationDbContext db,
            UserManager<Usuario> userManager, ILoggerFactory loggerFactory) =>
        {
            var usuario = await userManager.FindByIdAsync(id.ToString());
            if (usuario is null) return Results.NotFound();

            // Las mismas tres barreras que reporta la vista previa, otra vez
            // acá: la vista previa informa, pero quien decide es el servidor.
            // Un cliente viejo o una petición armada a mano no pueden saltarse
            // esto.
            if (id == principal.GetUsuarioId())
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["usuario"] = ["No puedes borrar tu propia cuenta desde acá."]
                });
            }

            if (await userManager.IsInRoleAsync(usuario, Roles.SuperAdmin)
                && (await userManager.GetUsersInRoleAsync(Roles.SuperAdmin)).Count <= 1)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["usuario"] = ["Es la única cuenta administradora. Dale la administración a otra antes de borrarla."]
                });
            }

            if (await db.MovimientosMeta.AnyAsync(m => m.UsuarioId == id && m.MetaAhorro.Hogar.Miembros.Count > 1))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["usuario"] = ["Tiene aportes en metas compartidas con otras personas. Borrarla les cambiaría el total sin avisarles."]
                });
            }

            // Todo o nada: a mitad de camino quedaría una cuenta sin datos o
            // datos sin cuenta, y las dos cosas son peores que no haber
            // empezado.
            //
            // La transacción es condicional porque las pruebas corren sobre
            // EF InMemory, que no las soporta y lanza al pedirla. En Postgres
            // —que es donde importa— siempre hay una.
            var transaccion = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync()
                : null;

            var gruposSolos = await db.MiembrosHogar
                .Where(m => m.UsuarioId == id && m.Hogar.Miembros.Count <= 1)
                .Select(m => m.HogarId)
                .ToListAsync();

            if (gruposSolos.Count > 0)
            {
                var metas = await db.MetasAhorro.Where(m => gruposSolos.Contains(m.HogarId)).Select(m => m.Id).ToListAsync();
                db.MovimientosMeta.RemoveRange(db.MovimientosMeta.Where(m => metas.Contains(m.MetaAhorroId)));
                db.MetasAhorro.RemoveRange(db.MetasAhorro.Where(m => gruposSolos.Contains(m.HogarId)));
                db.InvitacionesHogar.RemoveRange(db.InvitacionesHogar.Where(i => gruposSolos.Contains(i.HogarId)));
                db.MiembrosHogar.RemoveRange(db.MiembrosHogar.Where(m => gruposSolos.Contains(m.HogarId)));
                db.Hogares.RemoveRange(db.Hogares.Where(h => gruposSolos.Contains(h.Id)));
            }

            db.MiembrosHogar.RemoveRange(db.MiembrosHogar.Where(m => m.UsuarioId == id));
            db.MovimientosMeta.RemoveRange(db.MovimientosMeta.Where(m => m.UsuarioId == id));

            // InvitacionHogar es Restrict en las dos puntas: si no se quitan
            // a mano, el borrado falla con un error de llave foránea que no
            // le dice nada a nadie.
            db.InvitacionesHogar.RemoveRange(
                db.InvitacionesHogar.Where(i => i.InvitadorId == id || i.InvitadoId == id));

            db.PagosDeuda.RemoveRange(db.PagosDeuda.Where(p => p.Deuda.UsuarioId == id));
            db.Deudas.RemoveRange(db.Deudas.Where(d => d.UsuarioId == id));
            db.PagosTarjeta.RemoveRange(db.PagosTarjeta.Where(p => p.TarjetaCredito.UsuarioId == id));
            db.TarjetasCredito.RemoveRange(db.TarjetasCredito.Where(t => t.UsuarioId == id));
            db.ConfirmacionesCobro.RemoveRange(db.ConfirmacionesCobro.Where(c => c.Suscripcion.UsuarioId == id));
            db.Suscripciones.RemoveRange(db.Suscripciones.Where(x => x.UsuarioId == id));
            db.MovimientosDiaADia.RemoveRange(db.MovimientosDiaADia.Where(m => m.UsuarioId == id));
            db.ComerciosCategoria.RemoveRange(db.ComerciosCategoria.Where(c => c.UsuarioId == id));
            db.AvisosPresupuesto.RemoveRange(db.AvisosPresupuesto.Where(a => a.UsuarioId == id));
            db.Presupuestos.RemoveRange(db.Presupuestos.Where(x => x.UsuarioId == id));
            db.Categorias.RemoveRange(db.Categorias.Where(c => c.UsuarioId == id));
            db.Notificaciones.RemoveRange(db.Notificaciones.Where(n => n.UsuarioId == id));
            db.SuscripcionesPush.RemoveRange(db.SuscripcionesPush.Where(x => x.UsuarioId == id));
            db.TokensAtajo.RemoveRange(db.TokensAtajo.Where(t => t.UsuarioId == id));
            db.LlamadasAtajo.RemoveRange(db.LlamadasAtajo.Where(l => l.UsuarioId == id));
            db.CodigosRecuperacion.RemoveRange(db.CodigosRecuperacion.Where(c => c.UsuarioId == id));
            db.RefreshTokens.RemoveRange(db.RefreshTokens.Where(t => t.UsuarioId == id));
            db.InvitacionesApp.RemoveRange(db.InvitacionesApp.Where(i => i.CreadoPorUsuarioId == id));

            await db.SaveChangesAsync();

            var resultado = await userManager.DeleteAsync(usuario);
            if (!resultado.Succeeded)
            {
                if (transaccion is not null) await transaccion.RollbackAsync();
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["usuario"] = resultado.Errors.Select(e => e.Description).ToArray()
                });
            }

            if (transaccion is not null)
            {
                await transaccion.CommitAsync();
                await transaccion.DisposeAsync();
            }

            loggerFactory.CreateLogger("Admin").LogWarning(
                "El usuario {Admin} borró la cuenta {Objetivo} ({Correo}).",
                principal.GetUsuarioId(), id, usuario.Email);

            return Results.NoContent();
        });

        group.MapGet("/codigo-invitacion", async (ApplicationDbContext db) =>
        {
            var actual = await db.CodigosInvitacion
                .Where(c => c.Activo)
                .OrderByDescending(c => c.CreadoEn)
                .Select(c => new { c.Id, c.CreadoEn })
                .FirstOrDefaultAsync();

            return actual is null ? Results.NotFound() : Results.Ok(actual);
        });

        group.MapPost("/codigo-invitacion/rotar", async (RotarCodigoInvitacionRequest request, ApplicationDbContext db, ClaimsPrincipal principal) =>
        {
            var nuevoCodigo = request.NuevoCodigo.Trim();
            if (nuevoCodigo.Length < 8)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["nuevoCodigo"] = ["El código de invitación debe tener al menos 8 caracteres."]
                });
            }

            var activos = await db.CodigosInvitacion.Where(c => c.Activo).ToListAsync();
            foreach (var c in activos) c.Activo = false;

            db.CodigosInvitacion.Add(new CodigoInvitacion
            {
                CodigoHash = TokenService.HashearToken(nuevoCodigo),
                CreadoPorUsuarioId = principal.GetUsuarioId()
            });

            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    /// <summary>
    /// Se escribe a mano en el teclado de un celular, así que va sin las
    /// letras y números que se confunden al leer (0/O, 1/I/L) y con guion
    /// en la mitad. Lleva mayúscula, minúscula, dígito y símbolo para pasar
    /// las reglas de Identity sin depender de la suerte del azar.
    /// </summary>
    private const string Mayusculas = "ABCDEFGHJKMNPQRSTUVWXYZ";
    private const string Minusculas = "abcdefghijkmnpqrstuvwxyz";
    private const string Digitos = "23456789";

    private static string GenerarClaveTemporal()
    {
        var caracteres = new List<char>
        {
            Mayusculas[RandomNumberGenerator.GetInt32(Mayusculas.Length)],
            Minusculas[RandomNumberGenerator.GetInt32(Minusculas.Length)],
            Digitos[RandomNumberGenerator.GetInt32(Digitos.Length)],
            '#'
        };

        var alfabeto = Mayusculas + Minusculas + Digitos;
        while (caracteres.Count < 10)
        {
            caracteres.Add(alfabeto[RandomNumberGenerator.GetInt32(alfabeto.Length)]);
        }

        // Se baraja para que el símbolo y los obligatorios no queden siempre
        // en la misma posición.
        for (var i = caracteres.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (caracteres[i], caracteres[j]) = (caracteres[j], caracteres[i]);
        }

        return new string(caracteres.Take(5).ToArray()) + "-" + new string(caracteres.Skip(5).ToArray());
    }
}
