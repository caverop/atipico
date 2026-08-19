using Atipico.Domain.Enums;
using Atipico.Domain.Interfaces;

namespace Atipico.Domain.Entities
{
    // Solo lectura: respaldada por la vista v_cuenta_qr_evidencia_incompleta
    // (sql/006_comprobante_pago.sql). Cuentas por QR (cualquier estado salvo Anulada)
    // cuyos comprobantes vigentes no suman exactamente el monto. En una base al dia no
    // devuelve filas.
    //
    // Hoy la vista solo marca cuentas sin ningun comprobante; cuando el OCR llene los
    // montos empezara a marcar tambien las que no cuadran. SinMonto dice cuantos
    // comprobantes vigentes siguen sin importe extraido: mientras sea > 0, comparar
    // MontoRespaldado contra Monto no significa nada.
    public class CuentaQrEvidenciaIncompleta : IEntity
    {
        public long Id { get; set; }
        public string? Comensal { get; set; }
        public EstadoCuenta Estado { get; set; }
        public decimal Monto { get; set; }
        public DateTimeOffset? PagadoEn { get; set; }
        public long IdMesero { get; set; }
        public int Comprobantes { get; set; }
        public int SinMonto { get; set; }
        public decimal MontoRespaldado { get; set; }
    }
}
