using Atipico.Domain.Enums;
using Atipico.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atipico.Domain.Entities
{
    public class Mesa : IEntity
    {
        public long Id { get; set; }
        public int Numero { get; set; }
        public int Capacidad { get; set; }
        public EstadoMesa Estado { get; set; } = EstadoMesa.Libre;

        public DateTimeOffset CreadoEn { get; set; }

        public ICollection<PedidoMesa> PedidoMesas { get; set; } = new List<PedidoMesa>();
    }
}
