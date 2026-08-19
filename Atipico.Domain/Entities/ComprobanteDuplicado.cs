using Atipico.Domain.Interfaces;

namespace Atipico.Domain.Entities
{
    // Solo lectura: respaldada por la vista v_comprobante_duplicado
    // (sql/006_comprobante_pago.sql). Un mismo archivo registrado en cuentas
    // distintas — cobrar en efectivo y adjuntar el screenshot de un pago QR
    // anterior. Dentro de una misma cuenta ya lo impide uk_comprobante_cuenta_hash.
    // En una operacion sana no devuelve filas.
    public class ComprobanteDuplicado : IEntity
    {
        public long Id { get; set; }
        public long IdCuenta { get; set; }
        public string HashSha256 { get; set; } = null!;
        public decimal? Monto { get; set; }
        public DateTimeOffset CreadoEn { get; set; }
        public long IdSubidoPor { get; set; }
    }
}
