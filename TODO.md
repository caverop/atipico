# TODO

Pendientes del proyecto que ya se discutieron y todavía no se hicieron. Lo que está acá
tiene la dirección aprobada pero la ejecución pendiente; lo que todavía se está pensando
no va acá, va en el spec de su feature.

---

## ~~Commitear el trabajo del 2026-09-14/15~~ — hecho el 2026-09-15

**Estado:** hecho el 2026-09-15 por el agente, con la aprobación del usuario (*"si hazlo"*).
Todo lo de la tabla de abajo entró en ese commit.

**Qué hay sin versionar** (verificado con `git status`):

| Estado | Ruta | Qué |
|---|---|---|
| nuevo | `.agents/skills/actualizar-grafo/SKILL.md` | la skill que ejecuta la actualización del grafo cuando el usuario la pide |
| nuevo | `.agents/skills/auditar-specs/SKILL.md` | la skill de la auditoría de specs |
| nuevo | `specs/auditoria-specs.md` | el spec de la auditoría, `implementado` |
| modificado | `specs/README.md` | el índice: 27 specs y la fila de la auditoría |
| modificado | `specs/formato-spec.md` | la corrección del estado contradictorio + §11.5 |
| modificado | `specs/deploy-azure-aspire.md` | el estado puesto en el vocabulario (`en producción`) |
| modificado | `.claude/agent-memory/atipico/atipico-grafo-gemini-background.md` | el truncado a 20.000 caracteres y el método por subagentes |
| modificado | `.claude/agents/atipico.md` | el guard de la skill `graphify`: consultar sí, reconstruir solo a pedido |

**Qué no entra:** `graphify-out/` y `specs/grafo/` están gitignoreados por diseño — el grafo y
el vault son artefactos locales de esta máquina, no del repositorio. `.agents/` es un
directorio nuevo, así que necesita `git add .agents/` explícito.

**Contexto de por qué el grafo cambió de tamaño:** se reconstruyó de cero el 2026-09-15 con 7
subagentes que leen los specs enteros, y pasó de 627 nodos a **1302** (1618 aristas, 132
comunidades). La causa de la diferencia es que la ruta API trunca cada archivo a 20.000
caracteres. El detalle está en `.agents/skills/actualizar-grafo/SKILL.md` §8.5 y §8.6.

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
