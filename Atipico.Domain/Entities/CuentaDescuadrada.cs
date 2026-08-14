using Atipico.Domain.Enums;
using Atipico.Domain.Interfaces;

namespace Atipico.Domain.Entities
{
    // Solo lectura: respaldada por la vista v_cuenta_descuadrada (sql/script_inicial.sql).
    // Cuentas cuyo Monto declarado no coincide con la suma de su detalle de facturacion.
    // En una base sana esta consulta no devuelve filas.
    public class CuentaDescuadrada : IEntity
    {
        public long Id { get; set; }
        public EstadoCuenta Estado { get; set; }
        public decimal Monto { get; set; }
        public decimal SumaDetalle { get; set; }
        public DateTimeOffset? PagadoEn { get; set; }
    }
}
