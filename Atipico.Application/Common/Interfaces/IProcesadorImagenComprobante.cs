using Atipico.Application.Common.Exceptions;
using Atipico.Application.Models;

namespace Atipico.Application.Common.Interfaces
{
    /// <summary>
    /// Puerto hacia el procesamiento de imagen. Se hace del lado del servidor y ANTES de
    /// que el objeto exista, porque un comprobante es evidencia: hay que validar que sea
    /// realmente una imagen, quitarle el EXIF (las fotos de telefono llevan coordenadas
    /// GPS) y calcular su hash. Con subida directa al bucket estas garantias se volverian
    /// una verificacion posterior sobre algo ya guardado.
    /// </summary>
    public interface IProcesadorImagenComprobante
    {
        /// <exception cref="ComprobanteInvalidoException">El contenido no es una imagen aceptada.</exception>
        Task<ComprobanteProcesado> ProcesarAsync(Stream original, CancellationToken ct = default);
    }
}
