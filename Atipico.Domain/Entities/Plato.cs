using Atipico.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atipico.Domain.Entities
{
    public class Plato : IEntity
    {
        public long Id { get; set; }
        public string Nombre { get; set; } = null!;
        public decimal Precio { get; set; }
        public bool Activo { get; set; } = true;

        public DateTimeOffset CreadoEn { get; set; }
        public DateTimeOffset ActualizadoEn { get; set; }

        public long IdTipoPlato { get; set; }
        public TipoPlato TipoPlato { get; set; } = null!;

        public ICollection<PedidoPlato> PedidoPlatos { get; set; } = new List<PedidoPlato>();
    }
}
