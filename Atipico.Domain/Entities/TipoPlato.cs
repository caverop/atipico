using System;
using System.Collections.Generic;
using System.Text;

namespace Atipico.Domain.Entities
{
    public class TipoPlato
    {
        public long Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string? Observacion { get; set; }

        public DateTimeOffset CreadoEn { get; set; }

        public ICollection<Plato> Platos { get; set; } = new List<Plato>();
    }

}
