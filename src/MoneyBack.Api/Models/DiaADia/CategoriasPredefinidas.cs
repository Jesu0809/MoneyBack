namespace MoneyBack.Api.Models.DiaADia;

public static class CategoriasPredefinidas
{
    public static List<Categoria> ParaNuevoUsuario(int usuarioId) =>
    [
        new() { UsuarioId = usuarioId, Nombre = "Comida", Tipo = TipoCategoria.Gasto, Icono = "🍔" },
        new() { UsuarioId = usuarioId, Nombre = "Transporte", Tipo = TipoCategoria.Gasto, Icono = "🚗" },
        new() { UsuarioId = usuarioId, Nombre = "Vivienda", Tipo = TipoCategoria.Gasto, Icono = "🏠" },
        new() { UsuarioId = usuarioId, Nombre = "Entretenimiento", Tipo = TipoCategoria.Gasto, Icono = "🎬" },
        new() { UsuarioId = usuarioId, Nombre = "Salud", Tipo = TipoCategoria.Gasto, Icono = "🏥" },
        new() { UsuarioId = usuarioId, Nombre = "Otros gastos", Tipo = TipoCategoria.Gasto, Icono = "📦" },
        new() { UsuarioId = usuarioId, Nombre = "Salario", Tipo = TipoCategoria.Ingreso, Icono = "💼" },
        new() { UsuarioId = usuarioId, Nombre = "Otros ingresos", Tipo = TipoCategoria.Ingreso, Icono = "💰" },
    ];
}
