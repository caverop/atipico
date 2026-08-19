using Atipico.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atipico.Domain.Entities
{
    /// <summary>
    /// Evidencia de un pago por QR. Una cuenta admite varios: el primer QR puede no
    /// cubrir el total por saldo insuficiente, o el monto digitarse mal y completarse
    /// con un segundo pago. Cada fila respalda una parte de <see cref="Cuenta.Monto"/>.
    /// Solo-insercion: tg_comprobante_inmutable rechaza UPDATE y DELETE.
    /// </summary>
    public class ComprobantePago : IEntity
    {
        public long Id { get; set; }

        /// <summary>
        /// Parte del monto de la cuenta que respalda este comprobante. NULL mientras no se
        /// haya determinado: la cifra la extraera el OCR de la propia imagen, no se teclea.
        /// </summary>
        public decimal? Monto { get; set; }

        /// <summary>Clave del objeto en el bucket, no una URL: el proveedor puede cambiar.</summary>
        public string StorageKey { get; set; } = null!;

        /// <summary>Detecta el mismo archivo reusado en otra cuenta (v_comprobante_duplicado).</summary>
        public string HashSha256 { get; set; } = null!;

        public string TipoContenido { get; set; } = null!;
        public int Bytes { get; set; }

        public DateTimeOffset CreadoEn { get; set; }

        /// <summary>ck_comprobante_reemplazo lo exige cuando IdReemplaza no es null.</summary>
        public string? MotivoReemplazo { get; set; }

        public long IdCuenta { get; set; }
        public Cuenta Cuenta { get; set; } = null!;

        public long IdSubidoPor { get; set; }
        public Usuario SubidoPor { get; set; } = null!;

        /// <summary>
        /// NULL = comprobante que suma. No NULL = corrige al que apunta, y ese deja de
        /// sumar. Es la unica forma de enmendar un error en una tabla solo-insercion.
        /// </summary>
        public long? IdReemplaza { get; set; }
        public ComprobantePago? Reemplaza { get; set; }
    }
}
