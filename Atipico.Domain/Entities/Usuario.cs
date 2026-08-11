using Atipico.Domain.Enums;
using Atipico.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using System.Text.Json.Serialization;

namespace Atipico.Domain.Entities
{
    public class Usuario : IEntity
    {
        public long Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string NombreUsuario { get; set; } = null!;

        [JsonIgnore]
        public string PasswordHash { get; set; } = null!;

        // Contraseña en texto plano solo de paso: entra por JSON al crear/editar
        // un usuario, se hashea en el controlador y nunca se persiste ni se serializa.
        [NotMapped]
        [JsonPropertyName("password")]
        public string? Password { get; set; }

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
