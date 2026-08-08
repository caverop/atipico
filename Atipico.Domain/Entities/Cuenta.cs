using Atipico.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atipico.Domain.Entities
{
    public class Cuenta
    {
        public long Id { get; set; }
        public string? Comensal { get; set; }
        public EstadoCuenta Estado { get; set; } = EstadoCuenta.Abierta;
        public MetodoPago? MetodoPago { get; set; }
        public decimal Monto { get; set; }

        public DateTimeOffset CreadoEn { get; set; }
        public DateTimeOffset? PagadoEn { get; set; }

        public DateTimeOffset? AnuladoEn { get; set; }
        public long? IdAnuladoPor { get; set; }
        public Usuario? AnuladoPor { get; set; }
        public string? MotivoAnulacion { get; set; }

        public long IdMesero { get; set; }
        public Usuario Mesero { get; set; } = null!;

        public long? IdCajero { get; set; }
        public Usuario? Cajero { get; set; }

        public ICollection<DetalleCuenta> DetalleCuentas { get; set; } = new List<DetalleCuenta>();
    }
}
