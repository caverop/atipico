using Atipico.Domain.Enums;
using Atipico.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atipico.Domain.Entities
{
    public class Usuario : IEntity
    {
        public long Id { get; set; }
        public string Nombre { get; set; } = null!;
        public RolUsuario Rol { get; set; }
        public bool Activo { get; set; } = true;

        public DateTimeOffset CreadoEn { get; set; }
        public DateTimeOffset ActualizadoEn { get; set; }

        // Un mismo usuario llega desde cinco FK distintas: atiende, cobra y anula.
        public ICollection<Pedido> PedidosAtendidos { get; set; } = new List<Pedido>();
        public ICollection<Cuenta> CuentasAtendidas { get; set; } = new List<Cuenta>();
        public ICollection<Cuenta> CuentasCobradas { get; set; } = new List<Cuenta>();
        public ICollection<Cuenta> CuentasAnuladas { get; set; } = new List<Cuenta>();
        public ICollection<PedidoPlato> PlatosAnulados { get; set; } = new List<PedidoPlato>();
    }
}
