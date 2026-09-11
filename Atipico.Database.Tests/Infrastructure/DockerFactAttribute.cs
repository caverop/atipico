namespace Atipico.Database.Tests.Infrastructure
{
    // Reemplazo de [Fact]/[Theory] para toda esta suite. Setear `Skip` en el constructor del
    // atributo — que xUnit invoca en tiempo de *descubrimiento*, antes de correr nada — es lo
    // que logra que la prueba aparezca como Skipped (con motivo) y no como Failed, sin
    // agregar ninguna dependencia nueva al proyecto. xUnit v2.9.x no tiene Assert.Skip /
    // SkipUnless dinámico (eso es v3) — este es el mecanismo real disponible acá.
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DockerFactAttribute : FactAttribute
    {
        public DockerFactAttribute()
        {
            if (!DockerAvailability.EstaDisponible)
                Skip = MotivoSkip;
        }

        internal const string MotivoSkip =
            "Docker no está disponible — Atipico.Database.Tests necesita un contenedor " +
            "postgres:18-alpine (specs/agente-db.md §4.1/§4.5).";
    }

    // Mismo mecanismo que DockerFactAttribute, para los [Theory] con MemberData.
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DockerTheoryAttribute : TheoryAttribute
    {
        public DockerTheoryAttribute()
        {
            if (!DockerAvailability.EstaDisponible)
                Skip = DockerFactAttribute.MotivoSkip;
        }
    }
}
