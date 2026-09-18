using System.ComponentModel.DataAnnotations.Schema;
using MoneyBack.Api.Models.Metas;

namespace MoneyBack.Api.Models.Deudas;

/// <summary>
/// Un crédito/préstamo con cuotas, privado de un Usuario o compartido con
/// el Hogar (exactamente uno de UsuarioId/HogarId, ver DeudaConfiguration).
/// A propósito NO tiene CategoriaId propia: en una deuda compartida
/// cualquiera de los dos puede pagar una cuota, y Categoria es siempre
/// privada de un Usuario (ver Categoria.cs) — la categoría se elige al
/// pagar la cuota, no al crear la deuda.
/// </summary>
public class Deuda
{
    public int Id { get; set; }

    public TipoPropiedadDeuda TipoPropiedad { get; set; }

    public int? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public int? HogarId { get; set; }
    public Hogar? Hogar { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public decimal MontoTotal { get; set; }

    public decimal MontoCuota { get; set; }

    public int TotalCuotas { get; set; }

    public int CuotasPagadas { get; set; }

    public bool Activa { get; set; } = true;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public ICollection<PagoDeuda> Pagos { get; set; } = new List<PagoDeuda>();

    [NotMapped]
    public decimal MontoRestante => Math.Max(0, MontoTotal - (MontoCuota * CuotasPagadas));

    [NotMapped]
    public decimal PorcentajePagado =>
        MontoTotal <= 0 ? 0 : Math.Clamp((MontoCuota * CuotasPagadas) / MontoTotal * 100, 0, 100);
}
