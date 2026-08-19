namespace Atipico.Application.Common.Interfaces
{
    /// <summary>
    /// Puerto hacia el almacenamiento de objetos donde viven las imagenes de los
    /// comprobantes. La capa de aplicacion no conoce el proveedor: en la base se guarda
    /// solo la clave, nunca una URL, para que cambiar de proveedor sea reconfiguracion
    /// y no una migracion de datos.
    /// </summary>
    public interface IAlmacenComprobantes
    {
        Task GuardarAsync(string clave, Stream contenido, string tipoContenido, CancellationToken ct = default);

        /// <summary>
        /// URL de vida corta para que el navegador descargue el objeto directamente,
        /// sin que los bytes pasen por la API. El bucket es privado: sin firma no hay acceso.
        /// </summary>
        string GenerarUrlFirmada(string clave, TimeSpan duracion);
    }
}
