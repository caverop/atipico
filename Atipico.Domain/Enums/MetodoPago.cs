using System;
using System.Collections.Generic;
using System.Text;

namespace Atipico.Domain.Enums
{
    // Unicos metodos en alcance: Efectivo y Qr. Tarjeta/Transferencia (y antes, Yape/Plin)
    // quedaron fuera. La restriccion ck_cuenta_metodo desplegada en la base sigue permitiendo
    // EFECTIVO/TARJETA/TRANSFERENCIA/QR (sql/script_inicial.sql no se edita una vez aplicado,
    // ver el comentario de mantenimiento al inicio del archivo) — es un superconjunto inofensivo,
    // TARJETA/TRANSFERENCIA simplemente no son alcanzables desde este enum. "Qr" se mapea a
    // 'QR' via UpperSnakeCaseEnumConverter, que ya coincide con el valor real de la base.
    public enum MetodoPago
    {
        Efectivo,
        Qr
    }
}
