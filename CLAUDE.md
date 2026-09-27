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

## Deuda conocida

Cero pruebas de interfaz. 17 pantallas, ~165 pruebas, todas del backend. Ya
causó tres errores en producción que las pruebas no podían ver. Es lo próximo
que vale la pena hacer.
