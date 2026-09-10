---
name: atipico-no-commit-sin-aprobacion
description: "No commitear sin aprobación explícita del usuario, aunque el spec ya esté aprobado y el trabajo esté verificado. Dicho el 2026-09-10 durante la implementación de Atipico.Database.Tests."
metadata:
  type: feedback
---

**No commitear ni pushear sin que el usuario lo apruebe explícitamente**, aunque el
spec detrás del cambio ya esté `aprobado` y el trabajo esté verificado (tests en verde,
build limpio). Dicho el 2026-09-10, en medio de implementar `Atipico.Database.Tests` a
partir de `specs/agente-db.md`.

**Why:** hasta ese momento el patrón de la sesión había sido commitear y pushear cada
cambio en cuanto quedaba verificado —funcionó bien para specs y para la migración `015`,
donde cada paso era chico y reversible—. Para código nuevo de más peso (un proyecto de
test entero, con Testcontainers, tocando la solución) el usuario quiere revisar antes de
que quede en el historial. La aprobación del *spec* no es la misma aprobación que la del
*commit*: son dos gates distintos.

**How to apply:** implementar, correr los tests, dejar todo verificado y listo en el
working tree — pero terminar el turno con `git status` mostrando los cambios sin
commitear, y pedir el visto bueno antes de `git add`/`commit`/`push`. Si el usuario
después dice "commiteá" o similar, ahí sí se aplica el flujo normal de
[[atipico-spec-primero]]. Esto no reemplaza esa memoria — la acota: el spec sigue yendo
antes que el código, pero el código terminado tampoco se commitea solo porque compiló.

Relacionado: [[atipico-spec-primero]].
