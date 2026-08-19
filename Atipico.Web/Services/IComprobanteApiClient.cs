using Atipico.Domain.Entities;

namespace Atipico.Web.Services
{
    // IEntityApiClient<T> no sirve aca: es CRUD JSON sobre una ruta generica, y esto es
    // multipart/form-data con un archivo, mas dos endpoints que no encajan en ese contrato.
    public interface IComprobanteApiClient
    {
        Task<List<ComprobantePago>> GetPorCuentaAsync(long idCuenta);

        Task<string?> GetUrlAsync(long idComprobante);

        Task<ComprobantePago?> RegistrarAsync(
            long idCuenta,
            Stream contenido,
            string nombreArchivo,
            string tipoContenido,
            long? idReemplaza = null,
            string? motivoReemplazo = null);
    }
}
