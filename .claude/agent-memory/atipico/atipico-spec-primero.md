---
name: atipico-spec-primero
description: "Flujo de trabajo en Atipico — spec, luego plan, luego aprobación, luego código; ante un cambio, se actualiza primero el spec y después el plan."
metadata:
  type: feedback
---

El usuario trabaja Atipico con este orden y lo hace cumplir, sin que haga falta
pedirlo: **spec → plan → aprobación → código**. Cuando pide un cambio sobre algo ya
implementado, la corrección va igual en ese orden: *"primero modifica el spec luego
el plan"*. Lo ha pedido repetidamente: *"realiza el plan y diagramas previamente"*,
*"primeramente actualiza el spec"*, *"primero el spec y luego planifica los
cambios"*, *"revisa el spec y dame tu punto final antes de implementarlo"*.

**Formato del documento.** Vive en `specs/formato-spec.md` (SCRUM-26) — leerlo antes
de escribir un spec, no reconstruirlo de memoria. Cada feature tiene el suyo en
`specs/<feature>.md`, en español, con secciones numeradas. El usuario pidió
explícitamente el 2026-09-09 —*"graba en tu comportamiento lo siguiente para los
specs"*— que todo spec lleve: resumen del estado actual al principio, resumen en
pocas palabras al final, un apartado de **silogismo**, al menos un **diagrama**
(prefiere secuencia), y una **lista de artefactos** nuevos / a modificar / a
eliminar. Y que sea conciso: explicar con gráficos antes que con párrafos.

Dos matices que él aceptó al proponérselos, y que no hay que volver a discutir: el
diagrama es *del tipo que corresponda* (secuencia solo donde hay interacción real —
este dominio es sobre todo máquinas de estado), y el silogismo va sobre la **decisión
discutible** del spec, con una cláusula de reapertura que lo vuelva falsable.

**Un archivo por spec, siempre. Sin carpetas y sin umbral de tamaño.** Se evaluaron
las dos variantes y se descartaron las dos: carpeta-siempre rompe los enlaces
relativos entre specs, las citas desde el código y los ids de graphify sin ganancia
en 19 de 23 casos; y carpeta-por-umbral no la puede disparar nada, porque no hay
hooks (viven en `settings.json`, que no se toca) y el CI no mira `specs/`. Lo que
reemplaza al corte en archivos es una línea `**Contenido:** §1–§11 el diseño · §12 el
plan · §13 la bitácora` en la cabecera, obligatoria pasando 8 secciones — la inventó
solo `numero-pedido.md`, el más grande, y §7 del formato la generaliza.

**Cómo se comporta el usuario acá, y vale para la próxima:** propuso carpeta-por-spec,
aceptó la contrapropuesta por umbral, y después preguntó *"si el umbral no te favorece
podemos quitar eso, qué opinás"* — dejando caer su propia idea al ver el argumento. Le
sirve más una opinión fundada en datos del repo que un sí. Verificar antes de opinar
(acá: contar specs, buscar hooks, mirar el CI) y decir que no cuando corresponde.

El spec no es solo diseño ni una propuesta que se tira después: lleva una bitácora
con los diseños descartados, los errores corregidos y lo verificado empíricamente.
Es el registro de la tarea.

**Why:** los specs son el registro duradero del *porqué*, para que las decisiones no
se vuelvan a discutir meses después. El usuario los revisa antes de comprometerse al
código y ha cambiado de dirección en fase de spec más de una vez — mucho más barato
que hacerlo con el código escrito. Además ya pasó dos veces en la feature de turnos (`specs/numero-pedido.md`)
que el código se escribió antes de que el spec lo registrara, y el usuario lo marcó
explícitamente la segunda.

**How to apply:** ante una feature nueva, no empezar editando código — escribir el
spec primero y decirlo. Ante un pedido de cambio sobre una feature con spec,
actualizar `specs/<feature>.md` (diseño + reglas de negocio + criterios de
aceptación) y después la sección del plan, antes de tocar código. Al revisar una
decisión anterior, actualizar la sección afectada del spec en el mismo turno que el
cambio de código. Si el código ya se escribió, decirlo plano: el spec queda como
registro de lo hecho, no como propuesta previa.

Este es el mismo orden que ya declara `.claude/agents/atipico.md`; la memoria agrega
el formato del documento y las frases textuales con que el usuario lo pide.

Los specs viven en `specs/` (renombrada desde `docs/` el 2026-08-31).

Relacionado: [[atipico-improvement-plan]], [[atipico-user-tests-himself]].
