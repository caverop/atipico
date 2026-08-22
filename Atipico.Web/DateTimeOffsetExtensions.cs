namespace Atipico.Web
{
    public static class DateTimeOffsetExtensions
    {
        // Blazor Server renderiza del lado del servidor: DateTimeOffset.ToLocalTime() usa la
        // zona horaria del SO del servidor (UTC en un contenedor Docker típico), no la del
        // navegador del usuario — mostraba una hora distinta a la real. Bolivia no tiene
        // horario de verano, asi que un offset fijo UTC-4 alcanza y evita depender de que el
        // contenedor tenga la base de datos de zonas horarias instalada (TimeZoneInfo si la
        // necesita, y una imagen minima puede no traerla).
        private static readonly TimeSpan BoliviaOffset = TimeSpan.FromHours(-4);

        public static DateTimeOffset ToBoliviaTime(this DateTimeOffset value) => value.ToOffset(BoliviaOffset);

        /// <summary>
        /// La fecha calendario en Bolivia. Existe para que no vuelva a colarse
        /// <c>DateOnly.FromDateTime(DateTime.UtcNow)</c>, que parece correcto y no lo es: con
        /// UTC-4 la fecha UTC rueda a las 20:00 hora boliviana, asi que entre las 20:00 y la
        /// medianoche "hoy" en UTC ya es mañana acá.
        /// </summary>
        public static DateOnly ToBoliviaDate(this DateTimeOffset value) =>
            DateOnly.FromDateTime(value.ToBoliviaTime().DateTime);
    }
}
