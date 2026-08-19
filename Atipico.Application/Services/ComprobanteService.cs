using Atipico.Application.Common.Interfaces;
using Atipico.Application.Interfaces.Services;
using Atipico.Application.Models;
using Atipico.Domain.Entities;
using Atipico.Domain.Interfaces.Repositories;

namespace Atipico.Application.Services
{
    public class ComprobanteService : IComprobanteService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRepository<ComprobantePago> _repositorio;
        private readonly IAlmacenComprobantes _almacen;
        private readonly IProcesadorImagenComprobante _procesador;

        public ComprobanteService(
            IUnitOfWork unitOfWork,
            IAlmacenComprobantes almacen,
            IProcesadorImagenComprobante procesador)
        {
            _unitOfWork = unitOfWork;
            _repositorio = unitOfWork.Repository<ComprobantePago>();
            _almacen = almacen;
            _procesador = procesador;
        }

        public async Task<ComprobantePago> RegistrarAsync(RegistrarComprobante solicitud, CancellationToken ct = default)
        {
            var procesado = await _procesador.ProcesarAsync(solicitud.Contenido, ct);

            // UUID v7: ordenado por tiempo como un ULID y no adivinable, sin sumar un
            // paquete solo para generarlo. Va dentro de la carpeta de la cuenta para que
            // los varios comprobantes de una misma cuenta no colisionen.
            var clave = $"comprobantes/{solicitud.IdCuenta}/{Guid.CreateVersion7():n}.webp";

            // (1) El objeto primero. Si falla el paso (2) queda un objeto huerfano, que es
            // inofensivo y lo barre una regla de ciclo de vida; al reves quedaria una fila
            // prometiendo una evidencia que no existe.
            using (var contenido = new MemoryStream(procesado.Contenido, writable: false))
            {
                await _almacen.GuardarAsync(clave, contenido, procesado.TipoContenido, ct);
            }

            // (2) La fila. Las reglas de negocio (que la cuenta sea QR, que un reemplazo
            // apunte a la misma cuenta, que no se repita el archivo) las impone la base:
            // tg_comprobante_inmutable y uk_comprobante_cuenta_hash.
            var comprobante = new ComprobantePago
            {
                IdCuenta = solicitud.IdCuenta,
                // Monto queda en null a proposito: lo llenara el OCR (docs §3.1.1).
                StorageKey = clave,
                HashSha256 = procesado.HashSha256,
                TipoContenido = procesado.TipoContenido,
                Bytes = procesado.Bytes,
                IdSubidoPor = solicitud.IdSubidoPor,
                IdReemplaza = solicitud.IdReemplaza,
                MotivoReemplazo = solicitud.MotivoReemplazo,
            };

            _repositorio.Add(comprobante);
            await _unitOfWork.SaveChangesAsync();

            return comprobante;
        }

        public async Task<IEnumerable<ComprobantePago>> ObtenerPorCuentaAsync(long idCuenta)
        {
            var comprobantes = await _repositorio.FindAsync(c => c.IdCuenta == idCuenta);

            return comprobantes.OrderByDescending(c => c.CreadoEn).ThenByDescending(c => c.Id);
        }

        public async Task<string?> GenerarUrlAsync(long idComprobante, TimeSpan duracion)
        {
            var comprobante = await _repositorio.GetByIdAsync(idComprobante);

            return comprobante is null ? null : _almacen.GenerarUrlFirmada(comprobante.StorageKey, duracion);
        }
    }
}
