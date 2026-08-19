using Atipico.Domain.Interfaces;

namespace Atipico.Domain.Entities
{
    // Solo lectura: respaldada por la vista v_comprobante_vigente
    // (sql/006_comprobante_pago.sql). Comprobantes que siguen contando, es decir
    // los que nadie reemplazo. La suma de sus Monto es lo que se contrasta contra
    // el Monto de la cuenta.
    public class ComprobanteVigente : IEntity
    {
        public long Id { get; set; }
        public long IdCuenta { get; set; }
        /// <summary>NULL mientras el OCR no lo haya extraido.</summary>
        public decimal? Monto { get; set; }
        public string StorageKey { get; set; } = null!;
        public string HashSha256 { get; set; } = null!;
        public long IdSubidoPor { get; set; }
        public DateTimeOffset CreadoEn { get; set; }
    }
}
