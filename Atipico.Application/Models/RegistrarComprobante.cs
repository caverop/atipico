namespace Atipico.Application.Models
{
    /// <summary>
    /// Alta de un comprobante. Una cuenta admite varios: el primer QR puede no cubrir el
    /// total por saldo insuficiente, o el monto digitarse mal y completarse con un segundo
    /// pago. No lleva monto: la cifra la extraera el OCR de la propia imagen.
    /// </summary>
    public class RegistrarComprobante
    {
        public required long IdCuenta { get; init; }
        public required Stream Contenido { get; init; }

        /// <summary>Usuario autenticado que registra, tomado del token y no del cuerpo.</summary>
        public required long IdSubidoPor { get; init; }

        /// <summary>
        /// Null = comprobante que suma. Con valor = corrige al que apunta, y ese deja de
        /// sumar. Unica forma de enmendar un error en una tabla solo-insercion.
        /// </summary>
        public long? IdReemplaza { get; init; }

        public string? MotivoReemplazo { get; init; }
    }
}
