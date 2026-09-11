using System.Diagnostics;

namespace Atipico.Database.Tests.Infrastructure
{
    // Sin Docker, la suite se saltea; no falla (specs/agente-db.md §4.5). No es una
    // preferencia: el 2026-09-09 el demonio de Docker estaba caído al empezar la sesión de
    // ese spec, y si `dotnet test` en la raíz se pone rojo en una máquina sin Docker, la
    // suite se ignora en una semana y el proyecto queda peor que antes de tenerla.
    //
    // Se shellea a `docker info` en vez de usar la API interna de Testcontainers.NET a
    // propósito: es la misma señal que usaría un humano para confirmar si Docker está
    // arriba, y no ata este chequeo a un detalle de implementación de una librería externa.
    internal static class DockerAvailability
    {
        private static readonly Lazy<bool> _disponible = new(Verificar);

        public static bool EstaDisponible => _disponible.Value;

        private static bool Verificar()
        {
            try
            {
                var psi = new ProcessStartInfo("docker", "info")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var proceso = Process.Start(psi);
                if (proceso is null)
                    return false;

                // 5s alcanza de sobra: `docker info` contra un demonio arriba responde en
                // milisegundos. Si tarda más que eso, tratarlo como caído es la lectura
                // correcta — no vale la pena que la suite entera cuelgue esperando.
                if (!proceso.WaitForExit(5000))
                {
                    try { proceso.Kill(entireProcessTree: true); } catch { /* best effort */ }
                    return false;
                }

                return proceso.ExitCode == 0;
            }
            catch
            {
                // El binario `docker` ni siquiera está en el PATH, u otro fallo al lanzar el
                // proceso. Mismo resultado que "Docker no disponible": no hay pruebas que
                // correr, no es un error de la suite.
                return false;
            }
        }
    }
}
