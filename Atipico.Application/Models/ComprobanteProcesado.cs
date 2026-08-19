namespace Atipico.Application.Models
{
    /// <summary>
    /// Imagen ya validada, sin metadatos y recodificada, lista para guardarse.
    /// </summary>
    public class ComprobanteProcesado
    {
        public required byte[] Contenido { get; init; }
        public required string TipoContenido { get; init; }

        /// <summary>
        /// SHA-256 en hexadecimal minuscula del contenido YA procesado, no del original:
        /// asi el hash describe exactamente el objeto guardado y sirve tambien para
        /// verificar su integridad. La recodificacion es determinista, de modo que el
        /// mismo archivo subido dos veces produce el mismo hash y lo detecta
        /// uk_comprobante_cuenta_hash (dentro de una cuenta) o v_comprobante_duplicado
        /// (entre cuentas distintas).
        /// </summary>
        public required string HashSha256 { get; init; }

        public int Bytes => Contenido.Length;
    }
}
