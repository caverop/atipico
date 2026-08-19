using Atipico.Application.Common.Exceptions;
using Atipico.Application.Common.Interfaces;
using Atipico.Application.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using System.Security.Cryptography;

namespace Atipico.Infraestructure.Imaging
{
    /// <summary>
    /// Valida, limpia y recodifica la imagen de un comprobante antes de guardarla.
    /// ImageSharp es manejado puro: no agrega dependencias nativas al contenedor.
    /// Ojo con la licencia (Six Labors Split License) si la facturacion crece; la
    /// alternativa sin esa condicion es SkiaSharp, a costa de librerias nativas.
    /// </summary>
    public class ImageSharpProcesadorComprobante : IProcesadorImagenComprobante
    {
        // Suficiente para leer el monto y el numero de operacion. No se comprime como
        // una miniatura: en un comprobante la legibilidad vale mas que el tamano.
        private const int LadoMaximo = 2000;
        private const int Calidad = 85;

        public async Task<ComprobanteProcesado> ProcesarAsync(Stream original, CancellationToken ct = default)
        {
            Image imagen;
            try
            {
                // Load inspecciona los magic bytes: esta es la validacion real del
                // archivo, no la extension ni el Content-Type, que los declara el cliente.
                imagen = await Image.LoadAsync(original, ct);
            }
            catch (UnknownImageFormatException ex)
            {
                throw new ComprobanteInvalidoException(
                    "El archivo no es una imagen. Suba una captura o foto del comprobante.", ex);
            }
            catch (InvalidImageContentException ex)
            {
                throw new ComprobanteInvalidoException(
                    "La imagen esta corrupta o incompleta. Vuelva a subirla.", ex);
            }

            using (imagen)
            {
                // Primero la orientacion, y recien despues se borra el EXIF. Un telefono
                // guarda la foto vertical como apaisada mas una marca de rotacion; si se
                // quita la marca sin aplicarla, el comprobante queda de lado e ilegible.
                imagen.Mutate(x => x.AutoOrient());

                // Se relee el tamano porque AutoOrient pudo intercambiar ancho y alto.
                if (Math.Max(imagen.Width, imagen.Height) > LadoMaximo)
                {
                    imagen.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(LadoMaximo, LadoMaximo),
                    }));
                }

                // Las fotos tomadas con telefono llevan coordenadas GPS en el EXIF.
                imagen.Metadata.ExifProfile = null;
                imagen.Metadata.XmpProfile = null;
                imagen.Metadata.IptcProfile = null;

                using var salida = new MemoryStream();
                await imagen.SaveAsWebpAsync(salida, new WebpEncoder { Quality = Calidad }, ct);

                var contenido = salida.ToArray();

                return new ComprobanteProcesado
                {
                    Contenido = contenido,
                    TipoContenido = "image/webp",
                    HashSha256 = Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant(),
                };
            }
        }
    }
}
