using Atipico.Domain.Interfaces;

namespace Atipico.Domain.Entities
{
    /// <summary>
    /// Una fila por apertura de caja. Es a la vez el contador de pedidos del turno y el
    /// periodo al que pertenecen: por eso reiniciar la numeracion es INSERTAR una fila y no
    /// poner en cero un contador unico. Sin una fila que represente el periodo no hay clave
    /// contra la cual imponer UNIQUE, y despues de un reinicio no quedaria forma de saber
    /// que pedidos eran de que turno. Ver specs/numero-pedido.md §2.1.
    ///
    /// No hay horarios: el reinicio es manual, a criterio del cajero. No existe corte de
    /// jornada, ni ventana de turno, ni el problema de los turnos que cruzan la medianoche.
    /// </summary>
    public class TurnoCaja : IEntity
    {
        public long Id { get; set; }

        /// <summary>Texto libre: "Ejecutivo", "A la carta", o lo que el local use.</summary>
        public string Nombre { get; set; } = null!;

        public DateTimeOffset AbiertoEn { get; set; }

        /// <summary>Null mientras el turno esta abierto. uk_turno_caja_abierto permite uno solo asi.</summary>
        public DateTimeOffset? CerradoEn { get; set; }

        /// <summary>
        /// Lo administra tg_pedido_numero_turno (sql/010_turno_caja.sql), no la aplicacion.
        /// Se lee, nunca se escribe: mismo trato que ActualizadoEn con fn_touch.
        /// </summary>
        public int UltimoNumero { get; set; }

        /// <summary>
        /// Anulable solo por compatibilidad con procesos de soporte; todo turno abierto desde
        /// la aplicacion lleva cajero (RN-7).
        /// </summary>
        public long? IdCajero { get; set; }

        public Usuario? Cajero { get; set; }

        public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
    }
}
