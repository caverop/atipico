---
name: atipico-todo-netarchtest
description: "Los pendientes del proyecto viven en TODO.md en la raíz, no en memoria. Puntero al de NetArchTest (evaluar pruebas de arquitectura), aprobado el 2026-09-01."
metadata:
  type: project
---

**Los pendientes del proyecto se anotan en `TODO.md`, en la raíz del repo.** No en
memoria: el usuario decide cuándo se retoma cada cosa y necesita verlos en su árbol de
archivos, versionados y revisables en un diff. Esta nota es un puntero, **no una copia** —
si el detalle vive en dos lados se desincroniza, que es el defecto que este repo ya
documentó tres veces ([[atipico-graphify-y-obsidian]] tiene la traza).

Va en la raíz y no en `specs/` por dos motivos: no es un spec, y `specs/` es el corpus
que escanea graphify — un `TODO.md` ahí entraría al grafo como nodos de diseño.

Al 2026-09-01 el único pendiente anotado es **evaluar `NetArchTest`** para verificar
mecánicamente los límites de capas. Dirección aprobada, evaluación pendiente: hoy nada
impide que `Atipico.Domain` referencie `Atipico.Infraestructure` (verificado: no hay
`NetArchTest` ni analizadores en ningún `.csproj`). Los criterios de evaluación están en
`TODO.md`; leelo de ahí antes de actuar, puede haber cambiado.

Lo relacionado que sí se hizo: los umbrales de calidad de código viven en
`.claude/agents/dev.md`, secciones "Cómo se ve el código que entregás" y "Errores".
