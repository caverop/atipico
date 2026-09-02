---
name: atipico-locked-bin-release-build
description: "Con Atipico.Api corriendo el build Debug falla por bin bloqueado; usar -c Release para compilar y testear sin tocar su proceso."
metadata:
  type: feedback
---

El usuario suele dejar `Atipico.Api` corriendo mientras trabajo. Eso bloquea
`Atipico.Api\bin\Debug\net10.0\*.dll`, y cualquier `dotnet build` o `dotnet test`
que tenga a `Atipico.Api` como ProjectReference falla con `MSB3027`/`MSB3021`
("El archivo se ha bloqueado por: Atipico.Api (PID)").

**Cómo seguir sin matarle el proceso:**
- `dotnet test -c Release` / `dotnet build -c Release` — escribe en `bin\Release\`,
  que no está bloqueado. Funciona para la suite completa.
- `dotnet msbuild <proj>.csproj -t:Compile` — compila sin copiar a `bin`, útil para
  verificar que un proyecto compila.

**Lo que NO hay que hacer:** pasar `-p:BaseIntermediateOutputPath=...` para redirigir
`obj/`. Rompe el restore de NuGet y además hace que `obj/Release` preexistente se
incluya como código fuente, con una avalancha de `CS0579 atributo duplicado`.

**Why:** matarle el proceso es su decisión, no mía, y el build Release da el mismo
resultado sin tocarlo.

**How to apply:** ante `MSB3027` con "bloqueado por Atipico.Api", pasar a `-c Release`
y seguir; avisarle al final que su API sigue corriendo con el código viejo y que la
tiene que reiniciar para ver los cambios.

Relacionado: [[atipico-docker-port-zombie-gotcha]], [[atipico-user-tests-himself]].
