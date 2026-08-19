using Atipico.Application.Common.Interfaces;
using Atipico.Application.Models;
using Atipico.Application.Services;
using Atipico.Domain.Entities;
using Atipico.Domain.Interfaces.Repositories;
using Moq;

namespace Atipico.Application.Tests
{
    public class ComprobanteServiceTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly Mock<IRepository<ComprobantePago>> _repositorio = new();
        private readonly Mock<IAlmacenComprobantes> _almacen = new();
        private readonly Mock<IProcesadorImagenComprobante> _procesador = new();

        private static readonly ComprobanteProcesado Procesado = new()
        {
            Contenido = [1, 2, 3, 4],
            TipoContenido = "image/webp",
            HashSha256 = new string('a', 64),
        };

        private ComprobanteService CrearServicio()
        {
            _unitOfWork.Setup(u => u.Repository<ComprobantePago>()).Returns(_repositorio.Object);
            _procesador.Setup(p => p.ProcesarAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Procesado);

            return new ComprobanteService(_unitOfWork.Object, _almacen.Object, _procesador.Object);
        }

        private static RegistrarComprobante Solicitud(long? idReemplaza = null) => new()
        {
            IdCuenta = 418,
            Contenido = new MemoryStream([9, 9, 9]),
            IdSubidoPor = 7,
            IdReemplaza = idReemplaza,
            MotivoReemplazo = idReemplaza is null ? null : "Foto ilegible",
        };

        [Fact]
        public async Task Registrar_GuardaElObjetoAntesDeLaFila()
        {
            var servicio = CrearServicio();
            var orden = new List<string>();

            _almacen.Setup(a => a.GuardarAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback(() => orden.Add("objeto"))
                .Returns(Task.CompletedTask);
            _repositorio.Setup(r => r.Add(It.IsAny<ComprobantePago>()))
                .Callback(() => orden.Add("fila"));

            await servicio.RegistrarAsync(Solicitud());

            // Al reves quedaria una fila prometiendo una evidencia que no existe; en este
            // orden lo peor que puede quedar es un objeto huerfano, que es inofensivo.
            Assert.Equal(["objeto", "fila"], orden);
        }

        [Fact]
        public async Task Registrar_NoEscribeLaFilaSiFallaElAlmacenamiento()
        {
            var servicio = CrearServicio();

            _almacen.Setup(a => a.GuardarAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("R2 caido"));

            await Assert.ThrowsAsync<HttpRequestException>(() => servicio.RegistrarAsync(Solicitud()));

            _repositorio.Verify(r => r.Add(It.IsAny<ComprobantePago>()), Times.Never);
            _unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task Registrar_CopiaElResultadoDelProcesadoYDejaElMontoSinDeterminar()
        {
            var servicio = CrearServicio();
            ComprobantePago? guardado = null;

            _almacen.Setup(a => a.GuardarAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _repositorio.Setup(r => r.Add(It.IsAny<ComprobantePago>()))
                .Callback<ComprobantePago>(c => guardado = c);

            await servicio.RegistrarAsync(Solicitud());

            Assert.NotNull(guardado);
            Assert.Equal(Procesado.HashSha256, guardado!.HashSha256);
            Assert.Equal("image/webp", guardado.TipoContenido);
            Assert.Equal(Procesado.Contenido.Length, guardado.Bytes);
            Assert.Equal(7, guardado.IdSubidoPor);
            // El monto queda sin determinar: lo llenara el OCR leyendolo de la imagen.
            Assert.Null(guardado.Monto);
        }

        [Fact]
        public async Task Registrar_UsaUnaClaveDentroDeLaCarpetaDeLaCuenta()
        {
            var servicio = CrearServicio();
            var claves = new List<string>();

            _almacen.Setup(a => a.GuardarAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, Stream, string, CancellationToken>((clave, _, _, _) => claves.Add(clave))
                .Returns(Task.CompletedTask);

            await servicio.RegistrarAsync(Solicitud());
            await servicio.RegistrarAsync(Solicitud());

            Assert.All(claves, c => Assert.StartsWith("comprobantes/418/", c));
            // Dos comprobantes de la misma cuenta no pueden pisarse.
            Assert.Equal(2, claves.Distinct().Count());
        }

        [Fact]
        public async Task GenerarUrl_DevuelveNullSiElComprobanteNoExiste()
        {
            var servicio = CrearServicio();
            _repositorio.Setup(r => r.GetByIdAsync(It.IsAny<long>())).ReturnsAsync((ComprobantePago?)null);

            var url = await servicio.GenerarUrlAsync(999, TimeSpan.FromMinutes(5));

            Assert.Null(url);
            _almacen.Verify(a => a.GenerarUrlFirmada(It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Never);
        }
    }
}
