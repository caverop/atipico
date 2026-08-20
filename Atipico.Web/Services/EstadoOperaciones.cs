namespace Atipico.Web.Services
{
    /// <summary>
    /// Cuenta las operaciones en curso para que la interfaz muestre que está esperando.
    /// Registrado como scoped: en Blazor Server eso equivale a "por circuito", es decir un
    /// contador propio por usuario conectado.
    ///
    /// No vive en un DelegatingHandler, que sería lo natural, porque IHttpClientFactory
    /// agrupa y reutiliza los handlers fuera del scope del circuito: uno de esos handlers
    /// capturaría el circuito equivocado y la barra de un usuario se encendería por la
    /// operación de otro. Los clientes de API sí son scoped, así que el conteo se hace ahí
    /// (ver EntityApiClientConProgreso y ComprobanteApiClientConProgreso).
    /// </summary>
    public class EstadoOperaciones
    {
        private int _enCurso;

        public bool HayOperaciones => _enCurso > 0;

        public event Action? OnCambio;

        /// <summary>
        /// Envuelve una operación. El decremento va en finally: si una llamada falla, la
        /// barra tiene que apagarse igual o queda encendida para siempre.
        /// </summary>
        public async Task<T> SeguirAsync<T>(Func<Task<T>> operacion)
        {
            Comenzar();
            try
            {
                return await operacion();
            }
            finally
            {
                Terminar();
            }
        }

        public async Task SeguirAsync(Func<Task> operacion)
        {
            Comenzar();
            try
            {
                await operacion();
            }
            finally
            {
                Terminar();
            }
        }

        // ---- Bloqueo de pantalla ----------------------------------------------------
        // Contador aparte del de la barra, a proposito. La barra acompana cualquier llamada,
        // incluidas las cargas de fondo de una pagina que abre; bloquear la pantalla en cada
        // una de esas seria insoportable. El bloqueo es opt-in y se reserva para acciones que
        // el usuario dispara y que no admiten hacer otra cosa mientras corren.

        private int _bloqueos;

        public bool HayBloqueo => _bloqueos > 0;

        public string? MensajeBloqueo { get; private set; }

        public event Action? OnBloqueoCambio;

        public async Task BloquearAsync(Func<Task> accion, string mensaje)
        {
            if (Interlocked.Increment(ref _bloqueos) == 1)
            {
                MensajeBloqueo = mensaje;
                OnBloqueoCambio?.Invoke();
            }

            try
            {
                await accion();
            }
            finally
            {
                // En finally: si la accion falla, la pantalla tiene que desbloquearse igual
                // o el usuario queda encerrado sin poder ni reintentar ni navegar.
                if (Interlocked.Decrement(ref _bloqueos) == 0)
                {
                    MensajeBloqueo = null;
                    OnBloqueoCambio?.Invoke();
                }
            }
        }

        private void Comenzar()
        {
            // Varias operaciones pueden solaparse (una pagina que carga cuatro catalogos en
            // paralelo): la barra se apaga recien cuando termina la ultima, no la primera.
            if (Interlocked.Increment(ref _enCurso) == 1)
                OnCambio?.Invoke();
        }

        private void Terminar()
        {
            if (Interlocked.Decrement(ref _enCurso) == 0)
                OnCambio?.Invoke();
        }
    }
}
