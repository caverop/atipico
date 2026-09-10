---
name: atipico-grafo-con-el-commit
description: "Al commitear un cambio en specs/ hay que actualizar el grafo de graphify en el mismo turno; por qué un hook de git no puede reemplazarlo."
metadata:
  type: feedback
---

Cuando se commitea un cambio en `specs/`, el grafo de graphify se actualiza **en el
mismo turno**. Lo pidió el usuario el 2026-09-09: *"cada vez que un spec cambie y se
detecte que este cambio se comitee, actualiza el grafo de specs"*.

**Why:** el grafo se atrasa en silencio —llegó a 15 documentos y 8 días de deriva sin
que nada avisara— y no es cosmético: al ponerlo al día destapó una colisión de
numeración de migraciones contra `013_mesa_compartida_por_turno.sql`, que ya estaba
corrida en producción. Esa colisión estaba anotada en `reparacion-ck-cuenta-metodo.md`
días antes como hallazgo "reportado y no arreglado" y nadie la había vuelto a leer. El
grafo es lo que devuelve a la superficie lo que quedó escrito donde nadie mira.

**How to apply:** después de commitear specs, correr la skill `/graphify` desde la raíz
del repo. Antes de despachar subagentes, mirar el hit-rate de la caché y decir el costo
si da 0 (ver [[atipico-graphify-y-obsidian]] para esa trampa y las demás). Si el usuario
commiteó los specs por su cuenta desde su terminal —ya pasó en la sesión del
2026-09-09, commit `2116d75`— la regla no se disparó: al retomar trabajo sobre specs,
comparar `graphify-out/manifest.json` contra los `.md` de `specs/` y avisar si hay
deriva.

**Un hook de git no puede hacerlo, y no hay que volver a proponerlo.** Corre sin agente,
y sin clave de Gemini la extracción semántica la hace el agente anfitrión. Se intentó y
se descartó. Los hooks de Claude Code viven en `.claude/settings.json`, que este
proyecto prohíbe editar.

**El `post-commit` que solo deja una marca: propuesto y descartado el 2026-09-09.** La
idea era un hook de shell puro que tocara `graphify-out/needs_update` (convención real
del paquete: la escribe `watch.py`, la lee `cli.py:924`) cuando el commit tocó
`specs/*.md`. Se descartó por tres razones acumuladas, la última decisiva:

1. Vive en `.git/hooks/`, que no se versiona ni viaja en un clon. Con
   `core.hooksPath` el archivo llegaría a GitHub, pero la activación se guarda en
   `.git/config`, que tampoco se versiona: siempre queda un paso manual por máquina.
2. **`graphify-out/` está gitignoreado** — verificado, 0 archivos suyos en
   `origin/develop`. El grafo es un artefacto local de una sola máquina, así que ni
   una GitHub Action ni un hook versionado tendrían grafo que marcar en otro clon.
3. **Un hook solo puede avisar, nunca actualizar** (la extracción semántica necesita
   agente), y el aviso ya lo da `detect_incremental`, que detecta *más*: contenido
   cambiado, specs sin commitear, specs llegados por `git pull`, caché invalidada. El
   hook detecta un evento; el chequeo detecta el estado, que es lo que importa.

**Y el dato que lo cierra: el grafo tiene un solo lector, el agente.** El usuario
confirmó que nunca abre `graph.html` por su cuenta. Entonces el grafo solo necesita
estar al día cuando el agente lo va a usar, que es justo cuando chequea. Un commit del
usuario deja el grafo atrasado en una ventana en la que nadie lo mira.

La regla vive en `.claude/agents/atipico.md`, sección "El grafo se actualiza con el
commit". Relacionado: [[atipico-spec-primero]], [[atipico-graphify-y-obsidian]].
