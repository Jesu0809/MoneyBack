namespace MoneyBack.Api.Models.DiaADia;

public static class CategoriasPredefinidas
{
    /// <summary>
    /// (Nombre, Tipo, Icono) de cada categoría básica. Se usa tanto para
    /// sembrar cuentas nuevas al registrarse como para que cuentas ya
    /// existentes puedan agregar las que les falten desde /categorias.
    /// </summary>
    public static readonly (string Nombre, TipoCategoria Tipo, string Icono)[] Definiciones =
    [
        ("Mercado", TipoCategoria.Gasto, "🛒"),
        ("Comida", TipoCategoria.Gasto, "🍔"),
        ("Transporte", TipoCategoria.Gasto, "🚗"),
        ("Vivienda", TipoCategoria.Gasto, "🏠"),
        ("Servicios", TipoCategoria.Gasto, "💡"),
        ("Entretenimiento", TipoCategoria.Gasto, "🎬"),
        ("Salud", TipoCategoria.Gasto, "🏥"),
        ("Ropa", TipoCategoria.Gasto, "👕"),
        ("Otros gastos", TipoCategoria.Gasto, "📦"),
        ("Salario", TipoCategoria.Ingreso, "💼"),
        ("Bonos", TipoCategoria.Ingreso, "🎁"),
        ("Otros ingresos", TipoCategoria.Ingreso, "💰"),
    ];

    public static List<Categoria> ParaNuevoUsuario(int usuarioId) =>
        Definiciones
            .Select(d => new Categoria { UsuarioId = usuarioId, Nombre = d.Nombre, Tipo = d.Tipo, Icono = d.Icono })
            .ToList();
}
