# MoneyBack

PWA de finanzas personales para una pareja que ahorra para su apartamento en
Bogotá. Jesús y su pareja la usan a diario; **no es un proyecto de práctica,
es la app con la que manejan su plata de verdad.**

## Cómo está armado

- `src/MoneyBack.Web` — Blazor WebAssembly, desplegado en Firebase Hosting
  (`moneyback-app.web.app`).
- `src/MoneyBack.Api` — ASP.NET Core Minimal API en Fly.io
  (`moneyback-api.fly.dev`), EF Core sobre Postgres en Neon.
- `src/MoneyBack.Api.Tests` — xUnit + `WebApplicationFactory` + EF InMemory.

Desplegar: `fly deploy --now` desde `src/MoneyBack.Api`; para el frontend
`dotnet publish -c Release` y `npx firebase-tools deploy --only hosting`
desde `src/MoneyBack.Web`.

## Reglas que no se rompen

**Nunca correr `dotnet ef database update` contra producción sin que Jesús lo
autorice explícitamente.** Las migraciones se generan, se muestran y se
esperan.

**Avisar antes de desplegar.** La app está en uso; un despliegue a media tarde
la reinicia mientras alguien la está usando.

**`dotnet build` en la raíz NO compila los `.razor`.** Dice "correcta" y deja
pasar errores del frontend. Hay que compilar el proyecto Web explícito:
`dotnet build src/MoneyBack.Web`. Ya se escapó más de un error así.

**Las pruebas usan EF InMemory**, que evalúa en memoria consultas que Postgres
no sabría traducir. Ante una consulta LINQ dudosa, verificarla con
`ToQueryString()` sobre un contexto configurado con `UseNpgsql` (no hace falta
conectarse).

**Fechas: `HoraColombia` o nada.** Postgres guarda `timestamp with time zone`
y solo acepta `DateTimeKind.Utc`; `TimeZoneInfo.ConvertTimeFromUtc` devuelve
`Unspecified`. Mezclar las dos cosas no falla al compilar y EF InMemory no lo
ve —solo Postgres—, así que llega a producción intacto: el resumen semanal
llevaba desde que se escribió muriendo en su primera consulta, cada domingo,
sin enviar uno solo. La regla es `HoraColombia.Hoy()` para razonar sobre el
calendario de acá y `AInstanteUtc`/`InicioDelDiaUtc`/`InicioDelMesUtc` para
todo lo que toque una consulta o una columna. `HoraColombiaTests` tiene un
linter que revisa que ningún servicio arme fechas por su cuenta.

## Convenciones

Entidades mutables en `Models/<Feature>/`, DTOs como records posicionales en
`Dtos/<Feature>Dtos.cs`, endpoints como métodos de extensión `MapXEndpoints()`
registrados uno por línea en `Program.cs`, `principal.GetUsuarioId()` para el
usuario actual, `Results.Forbid()` para dueño equivocado. Migraciones con
nombre en español PascalCase.

Todo el texto que ve el usuario va en español de Colombia. Los comentarios
también, y explican **por qué** algo es así —sobre todo cuando la decisión
protege contra un error concreto— no qué hace la línea.

## Lo que hay que entender del producto

**Los gastos entran solos.** Un atajo de iOS lee los SMS de Davivienda y las
notificaciones de Nu y llama a `/api/atajos/registrar-texto` con un token
propio (no JWT). Ese es el corazón de la app: si eso falla, nadie se entera
hasta que cuadra cuentas.

**La app aprende dónde va cada comercio.** Corregir la categoría de un gasto
del banco guarda esa relación y arrastra los anteriores del mismo sitio que
seguían sin clasificar. Nunca pisa lo que la persona ya clasificó a mano.

**"Sin clasificar" es una categoría real**, no un descarte: existe para que
los gastos que entran solos pidan atención en vez de esconderse en "Otros".

**Los topes avisan por push** al 80% y al pasarse, una vez por categoría y
por mes. Más avisos y la persona silencia la app.

**La sección Vivienda** responde "¿hasta cuánto podemos comprar?" con cifras
reales del mercado colombiano. Viven en `Domain/Subsidios/ParametrosVivienda.cs`,
**fechadas y con fuente**, y la fecha se le muestra al usuario. Cambian cada
año; una cifra vieja presentada como actual puede arruinar una compra de cien
millones.

## Pruebas

Dos proyectos: `MoneyBack.Api.Tests` (xUnit + `WebApplicationFactory` + EF
InMemory) y `MoneyBack.Web.Tests` (bUnit). El segundo arma el mismo árbol de
servicios que `Program.cs` —incluido `FallosDeRedHandler`, sin el cual las
pruebas ven excepciones de red que en la app nunca ocurren— y reemplaza el
cable por `ServidorFalso`, que serializa JSON de verdad para que la
deserialización también quede cubierta.

`CoherenciaVisualTests` vigila reglas que ningún compilador revisa y que ya
fallaron una vez: que todo `<Icon Name>` exista (hay un `default` que dibuja
un círculo vacío, así que un nombre mal escrito no rompe nada), que toda
`var(--color-…)` esté definida (una var() indefinida invalida la declaración
entera y el borde simplemente no se pinta), que el tema oscuro redefina todo
lo del claro, que ningún `Api.Eliminar…Async` se salte la hoja de
confirmación, y que no haya datos personales en los marcadores de posición.

Cuidado con cambiar `CurrentCulture` dentro de una prueba async: al primer
await el cambio se queda pegado en el hilo del pool y contamina lo que corra
después. Hacerlo en un `Thread` propio (ver `CulturaTests`).

## Dónde vive cada cosa

API en Fly `iad` (Virginia), base en Neon `us-east-2` (Ohio): entre ellos hay
~15 ms, por eso `/health` y un login tardan casi lo mismo. **Fly no tiene
región en Colombia ni en Chile**; lo más cerca que ofrece es `iad`, `dfw` y
`gru`, y ninguna queda más cerca de Bogotá que la actual. Mover de región no
es una mejora disponible.

Desde Bogotá, la primera petición cuesta ~0,30 s (0,09 s de TCP + 0,11 s de
TLS) y las siguientes, reusando la conexión, ~0,12 s. Al medir con `curl`
suelto se mide siempre el caso caro; el navegador no.
