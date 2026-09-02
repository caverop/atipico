# TODO

Pendientes del proyecto que ya se discutieron y todavía no se hicieron. Lo que está acá
tiene la dirección aprobada pero la ejecución pendiente; lo que todavía se está pensando
no va acá, va en el spec de su feature.

---

## Evaluar `NetArchTest` para verificar los límites de capas

**Estado:** dirección aprobada el 2026-09-01. **Falta la evaluación, no la instalación.**

**El problema.** Los límites de la arquitectura onion no los verifica nada. Revisados los
`.csproj` al 2026-09-01: no hay `NetArchTest`, ni `TreatWarningsAsErrors`, ni
`AnalysisLevel`, ni `EnforceCodeStyleInBuild`. Un `using Atipico.Infraestructure` dentro
de `Atipico.Domain` compila sin una sola queja. Hoy la arquitectura la sostienen
`CLAUDE.md` y la disciplina, nada más.

**La forma que tendría.** Un test unitario por regla:

```csharp
Types.InCurrentDomain().That().ResideInNamespace("Atipico.Domain")
     .ShouldNot().HaveDependencyOn("Atipico.Infraestructure")
     .GetResult().IsSuccessful
```

**Lo que hay que evaluar antes de escribir una línea:**

1. **¿El repo pasa hoy esas reglas?** Si ya hay violaciones, el test nace en rojo y hay
   que decidir si se corrigen primero o si el test entra acotado.
2. **¿En qué proyecto vive?** Los cuatro proyectos de test espejan `src` 1:1, así que
   ninguno es el lugar obvio para una prueba que cruza todas las capas.
3. **¿Se justifica el mantenimiento a esta escala?** Es una dependencia más y un test que
   hay que actualizar cada vez que se agrega un proyecto.

Va con spec antes que con código, como todo acá.

---

## ~~Unificar los ids del grafo de graphify~~ — hecho el 2026-09-01

Los 35 nodos que conservaban el prefijo `docs_` del layout anterior se renombraron en
`graphify-out/graph.json`. Sin re-extracción: 0 colisiones, 123 nodos y 155 aristas
intactos. Queda anotado acá porque el diagnóstico original decía que hacía falta un
`graphify extract --force`, y no hizo falta.

**Relacionado, ya hecho:** los umbrales de calidad de código (tamaño, complejidad,
nombres, manejo de errores) están en `.claude/agents/dev.md`. El agente `dev` ya tiene la
instrucción de reportar las violaciones de capa que encuentre, sin corregirlas — eso
cubre la detección manual mientras no haya test.
