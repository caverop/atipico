using System;
using System.Collections.Generic;
using System.Text;

namespace Atipico.Domain.Enums
{
    // Yape/Plin quedaron fuera de alcance: la restriccion ck_cuenta_metodo desplegada en la
    // base solo permite EFECTIVO/TARJETA/TRANSFERENCIA/QR (sql/script_inicial.sql no se edita
    // una vez aplicado, ver el comentario de mantenimiento al inicio del archivo), y QR no
    // tiene equivalente en este enum.
    public enum MetodoPago
    {
        Efectivo,
        Tarjeta,
        Transferencia
    }
}
