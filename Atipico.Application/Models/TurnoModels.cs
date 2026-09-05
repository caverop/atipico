using Atipico.Domain.Entities;
using Atipico.Domain.Enums;

namespace Atipico.Application.Models
{
    /// <summary>
    /// Un turno de caja tal como lo ve la interfaz. Ver specs/numero-pedido.md §7.1.
    /// </summary>
    public sealed record TurnoDto(
        long Id,
        string Nombre,
        DateTimeOffset AbiertoEn,
        DateTimeOffset? CerradoEn,
        string? CajeroNombre,
        int TotalPedidos);

    /// <summary>
    /// Lo que necesita la grilla en una sola llamada. El turno va UNA VEZ en el sobre y no
    /// repetido en cada fila.
    ///
    /// <see cref="Turno"/> en null distingue los dos estados vacios: "turno abierto sin
    /// pedidos" (lista vacia, encabezado normal) de "no hay turno abierto" (no se dibuja la
    /// grilla, se ofrece abrir uno). Sin este campo el cliente tendria que inferir uno del
    /// otro a partir de una lista vacia, y son cosas distintas. Ver §8.4.
    ///
    /// <see cref="MesasPorPedido"/> mapea Pedido.Id a los numeros de sus mesas asociadas,
    /// ordenados ascendente. Un pedido sin mesas no tiene entrada (se consulta con
    /// GetValueOrDefault). Ver specs/numero-mesa-grilla-pedidos.md §3.
    /// </summary>
    public sealed record GrillaPedidosDto(
        TurnoDto? Turno,
        IReadOnlyList<Pedido> Pedidos,
        IReadOnlyDictionary<long, IReadOnlyList<int>> MesasPorPedido);

    /// <summary>
    /// Abrir turno. El cajero NO viaja en el cuerpo: sale del token (mismo criterio que
    /// ComprobantesController con quien sube el comprobante). Si viniera del cliente,
    /// cualquiera podria abrir un turno a nombre de otro.
    /// </summary>
    public sealed class AbrirTurnoRequest
    {
        public string Nombre { get; set; } = "";
    }

    /// <summary>Un pedido que impide cerrar el turno, con lo justo para ir a buscarlo.</summary>
    public sealed record PedidoVivoDto(long Id, int NumeroTurno, string? Comensal, EstadoPedido Estado);

    /// <summary>
    /// Cuerpo del 409 al intentar cerrar con pedidos vivos. El mensaje generico del trigger
    /// es la red de atras; esta es la respuesta esperada, y trae la lista para que el cajero
    /// sepa a que mesa ir en vez de tener que buscarla.
    ///
    /// Se llama Message, en singular y sin acento, porque asi lo serializa Web
    /// (camelCase -> "message") y es la propiedad que ApiException lee en el cliente.
    /// </summary>
    public sealed record CierreBloqueadoDto(string Message, IReadOnlyList<PedidoVivoDto> Pedidos);
}
