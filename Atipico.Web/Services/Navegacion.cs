namespace Atipico.Web.Services
{
    /// <summary>Un link del menu, con los roles que pueden verlo.</summary>
    public record EnlaceNav(string Texto, string Url, params string[] Roles);

    /// <summary>
    /// Un grupo de links bajo un titulo ("Carta", "Caja", ...).
    /// </summary>
    public record SeccionNav(string Titulo, IReadOnlyList<EnlaceNav> Enlaces)
    {
        /// <summary>
        /// Roles que ven la seccion: la union de los roles de sus links, calculada y no
        /// declarada a mano. Antes eran dos listas separadas y se desincronizaron: la
        /// seccion "Pedidos" se mostraba a Cocinero pero su unico link era Admin,Mesero,
        /// asi que un cocinero veia el titulo con nada debajo. Derivandolo, ese estado
        /// no se puede volver a escribir.
        /// </summary>
        public string RolesCsv { get; } =
            string.Join(",", Enlaces.SelectMany(e => e.Roles).Distinct());
    }

    /// <summary>
    /// Fuente unica del menu. La consumen el sidebar (<c>NavMenu</c>) y la hoja "Mas" de
    /// la barra inferior en movil: son dos dibujos del mismo arbol, y duplicar quince
    /// links en dos componentes garantizaba que se separaran.
    ///
    /// Esto NO es autorizacion: la API vuelve a validar cada accion por su cuenta
    /// (<c>CreateRoles</c>/<c>UpdateRoles</c>/<c>DeleteRoles</c>). Aca solo se decide que
    /// se dibuja.
    /// </summary>
    public static class Navegacion
    {
        private const string Admin = "Admin";
        private const string Mesero = "Mesero";
        private const string Cajero = "Cajero";

        public static readonly IReadOnlyList<SeccionNav> Secciones =
        [
            new("Personal",
            [
                new("Usuarios", "usuarios", Admin),
            ]),
            new("Carta",
            [
                new("Tipos de plato", "tipos-plato", Admin),
                new("Platos", "platos", Admin),
            ]),
            new("Salón",
            [
                new("Mesas", "mesas", Admin),
            ]),
            new("Pedidos",
            [
                new("Pedidos", "pedidos", Admin, Mesero),
                new("Pedido <-> Mesa", "pedido-mesas", Admin),
                new("Pedido <-> Plato", "pedido-platos", Admin),
            ]),
            new("Caja",
            [
                new("Cuentas", "cuentas", Admin, Cajero),
                // Ojo: el comentario original decia "el mesero tambien", pero el link
                // siempre estuvo bajo Admin,Cajero. Se conserva tal cual para no ampliar
                // permisos por accidente en un refactor de maquetado.
                new("Evidencia de pagos QR", "reportes/evidencia-incompleta", Admin, Cajero),
                new("Detalle de cuentas", "detalle-cuentas", Admin, Cajero),
                new("Conciliación de cuentas", "reportes/cuentas-descuadradas", Admin, Cajero),
                new("Comprobantes duplicados", "reportes/comprobantes-duplicados", Admin, Cajero),
            ]),
        ];

        /// <summary>
        /// Pestañas de la barra inferior (solo movil). Son los destinos que se tocan
        /// varias veces por turno; todo lo demas vive detras de "Mas".
        /// </summary>
        public static readonly IReadOnlyList<EnlaceNav> Pestanas =
        [
            new("Pedidos", "pedidos", Admin, Mesero),
            new("Caja", "cuentas", Admin, Cajero),
        ];
    }
}
