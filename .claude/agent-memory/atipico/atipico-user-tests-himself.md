---
name: atipico-user-tests-himself
description: "El usuario hace sus propias pruebas manuales — verificar solo con build/test, no levantar servidores ni Playwright salvo que lo pida."
metadata:
  type: feedback
---

Ante un cambio de código en Atipico, verificar con `dotnet build` (0 errores) y parar
ahí — no arrancar servidores `dotnet run` ni manejar Playwright para clickear la UI,
salvo que el usuario pida explícitamente verificación en vivo.

**Why:** 2026-08-19, a mitad de sesión: *"yo realizare las pruebas solo ejecuta las
ordenes"*. Lo dijo una vez, pero lo respetó en todos los pedidos siguientes de esa
sesión sin quejarse, así que es una preferencia permanente, no un caso puntual.
Antes en esa misma sesión yo venía autoverificando todo end-to-end con Playwright;
esto revierte ese default.

**How to apply:** compilar, reportar 0 errores y entregar. Si más adelante pide
reproducir un bug en vivo o quiere algo pasado por el navegador, está bien — la
preferencia es sobre no hacerlo por default, no una prohibición.

Combinar con [[atipico-locked-bin-release-build]]: si su API está corriendo, ese
build es `-c Release`.
