# Pruebas de componentes Blazor, y la marca del login

Especificación para corregir el encabezado del login —dice `ATIPICO2`, debe decir
`ATIPICO`— y, sobre todo, para **crear la infraestructura de pruebas de componentes
Blazor que hoy no existe**, que es el cambio real.

- **Estado:** **implementado y verificado** el 2026-09-01. Los seis criterios de
  aceptación cerrados, incluido el de pantalla.
- **Alcance:** un proyecto `Atipico.Web.Tests` con bUnit, la prueba del encabezado del
  login, y el cambio de la palabra.
- **Fuera de alcance:** portar a bUnit cualquier otra página. Este spec crea el andamiaje
  y lo estrena con un caso; el resto se agrega cuando haga falta.

## 1. Problema

`Atipico.Web/Components/Pages/Login.razor:9` dice:

```razor
<h1 class="atipico-brand text-center mb-4">ATIPICO2</h1>
```

El `2` no es una decisión: cuatro líneas más arriba, el mismo archivo declara
`<PageTitle>Iniciar sesión - Atipico</PageTitle>`, sin sufijo. Es la primera pantalla que
ve cualquiera que entra al sistema, y muestra un nombre que no es el del producto.

**El problema de fondo es otro.** La solución tiene cuatro proyectos de prueba —`Domain`,
`Application`, `Infraestructure`, `Api`— y **ninguno para `Atipico.Web`**. No hay bUnit en
ningún `.csproj`. Todo lo que se renderiza en Blazor está hoy sin cobertura automatizada:
la única verificación posible es abrir el navegador y mirar. Por eso un cambio de una
palabra no se puede probar, y por eso este spec trata sobre el andamiaje y no sobre la
palabra.

## 2. Decisión

Crear `Atipico.Web.Tests` con **bUnit** sobre xUnit, registrarlo en `Atipico.slnx`, y
estrenarlo con la prueba del encabezado del login.

### 2.1 Por qué bUnit y no Playwright para esto

Playwright ya funciona en este entorno y se usó para *encontrar* este defecto. Pero como
prueba de regresión no sirve acá: necesita la aplicación levantada, una base de datos y un
navegador. Una prueba que solo corre cuando alguien acuerda de levantar `aspire run` no es
una prueba, es un recordatorio.

bUnit renderiza el componente en memoria, dentro de `dotnet test`, sin servidor ni
navegador. Entra en el mismo `dotnet test -c Release` que ya corre todo lo demás.

**Los dos se reparten el trabajo así:** bUnit para lo que el componente *renderiza*
(texto, estructura, qué se muestra según el estado); Playwright para lo que solo existe
con la aplicación entera viva (login real contra la API, la barra fija de
[barra-acciones-pedido.md](barra-acciones-pedido.md) §6, el layout móvil). Este spec cubre
el primero.

### 2.2 Lo que se descartó

| Idea | Por qué no |
|---|---|
| Cambiar la palabra y no escribir prueba | Es la opción proporcionada al cambio, y fue rechazada a propósito: el valor de esta tarea es el andamiaje, no la palabra |
| Meter la prueba en `Atipico.Api.Tests` | Los cuatro proyectos de prueba espejan `src` 1:1. Romper esa correspondencia por evitar un `.csproj` deja el repo peor de lo que estaba |
| Prueba de Playwright en CI | Requiere base, servidor y navegador en el pipeline. Es una decisión de CI/CD con su propio costo — ver [cicd-github-azure-render.md](cicd-github-azure-render.md) |
| Sacar la marca a una constante compartida | Hoy aparece en dos lugares del mismo archivo. Una constante para dos usos contiguos agrega indirección sin comprar nada |

### 2.3 Qué NO hace la prueba

No verifica el CSS. `atipico-brand` decide tipografía y color, y bUnit renderiza marcado,
no estilos aplicados. La prueba afirma **qué texto hay en el `<h1>`**, nada más. Si mañana
alguien pone `text-transform` o cambia la fuente, esta prueba no se entera, y está bien
que no se entere.

## 3. El cambio

Una línea:

```razor
- <h1 class="atipico-brand text-center mb-4">ATIPICO2</h1>
+ <h1 class="atipico-brand text-center mb-4">ATIPICO</h1>
```

La palabra va en **mayúsculas** porque así está hoy y así la dibuja el diseño de la
tarjeta de login. Este spec cambia el sufijo, no la caja.

## 4. El proyecto de pruebas

- **Nombre:** `Atipico.Web.Tests`, junto a los otros cuatro, registrado en `Atipico.slnx`.
- **Dependencias:** `bunit`, más el mismo stack de xUnit que ya usan los otros proyectos.
  La versión de bUnit tiene que ser compatible con .NET 10; si no lo es, **eso es un
  hallazgo que frena la tarea y se reporta**, no se resuelve bajando el target.
- **Referencia:** a `Atipico.Web`.

`Login.razor` tiene `@inject` y un `@code` con `Error` y el POST a `/auth/login`. La
prueba **no** ejercita el envío: renderiza y mira el encabezado. Lo que haga falta
registrar en los servicios de prueba para que el componente renderice es parte de la
implementación, no de este spec.

## 5. Criterios de aceptación

- [x] ✅ CA-1 — Existe `Atipico.Web.Tests` en la solución y `dotnet test -c Release` lo
  ejecuta junto a los otros cuatro proyectos.
- [x] ✅ CA-2 — Una prueba de bUnit renderiza `Login` y afirma que el `<h1>` contiene
  exactamente `ATIPICO`.
- [x] ✅ CA-3 — Esa prueba **falla** contra el código actual (que dice `ATIPICO2`), y falla
  por la assertion del texto, no por un error de renderizado o de configuración.
- [x] ✅ CA-4 — Con el cambio aplicado, la prueba pasa y **ninguna de las pruebas existentes
  se rompe**.
- [x] ✅ CA-5 — La página de login servida muestra `ATIPICO` en el encabezado. Se verifica
  con Playwright contra la aplicación corriendo, no con bUnit.
- [x] ✅ CA-6 — `<PageTitle>` sigue diciendo `Iniciar sesión - Atipico`, sin tocar.

## 6. Plan

1. **Spec** — este documento. *(hecho)*
2. **Pruebas** *(hecho)* — agente `qa`: crea `Atipico.Web.Tests`, lo registra en la solución, escribe
   la prueba de CA-2 y la deja **en rojo por la razón de CA-3**. No toca `Login.razor`.
3. **Implementación** *(hecho)* — agente `dev`: cambia la palabra y pone la prueba en verde. No toca
   las pruebas.
4. **Verificación en pantalla** *(hecho)* — Playwright contra el login, para CA-5.

El orden importa: si la prueba se escribe después del cambio, nunca se comprueba que
falle, y una prueba que nunca se vio fallar no prueba nada.

## 7. Bitácora

**Cómo se encontró.** No se encontró leyendo código: el `grep` inicial de `atipico2` no
devolvió nada porque el texto está en mayúsculas y la búsqueda era sensible a la caja. El
defecto apareció al abrir el login con Playwright y leer el snapshot de accesibilidad, que
mostró `heading "ATIPICO2" [level=1]`. Es un argumento a favor de mirar la pantalla y no
solo el repositorio.

**Se propuso hacerlo sin spec y sin pruebas.** La recomendación inicial fue cambiar la
palabra y verificar con Playwright, por desproporción: un token de marcado contra un
proyecto de pruebas nuevo. Se rechazó a propósito. El razonamiento que la revierte es que
la desproporción es real solo si se mira la palabra; mirada como *"Blazor no tiene ninguna
cobertura automatizada"*, el proyecto de pruebas es la tarea y la palabra es la excusa
para estrenarlo.

### bUnit 2.x: dos nombres que cambiaron

La incompatibilidad con .NET 10 que §4 anticipaba **no se dio**: `bunit` 2.9.0 publica un
target `net10.0` propio sobre `Microsoft.AspNetCore.Components` 10.0.10, sin bajar el TFM.
Dos renombres de la versión 2.x que valen como precedente para la próxima prueba: la clase
base es `Bunit.BunitContext` —no `TestContext`, que se renombró para no chocar con xunit
v3— y el método es `Render<T>()`, con `RenderComponent<T>()` como alias.

**No hizo falta registrar ningún servicio.** El briefing al agente `qa` afirmaba que
`Login.razor` tiene `@inject`; es falso. Tiene dos `[SupplyParameterFromQuery]` (`Error`,
`ReturnUrl`), y `BunitContext` ya trae registrados el `NavigationManager` falso y el
proveedor de parámetros de query. `@layout Layout.EmptyLayout` tampoco estorba: bUnit
renderiza el componente directo, sin pasar por el layout.

### Release compila, pero no es lo que sirve el navegador

Al terminar la implementación el login **seguía mostrando `ATIPICO2`**, y el cambio estaba
bien. Con `aspire run` levantado hay que compilar en `-c Release` porque `bin\Debug` está
bloqueado —esa es la regla del proyecto y se respetó—, pero Aspire sirve justamente desde
`bin\Debug`: la DLL en uso era 12 minutos anterior al cambio y no hay hot reload activo.

En el menú del recurso, **`Reiniciar` no alcanza**: relanza el mismo binario viejo. Lo que
cierra el ciclo es **`Rebuild`**, que recompila en Debug y reinicia. Recién ahí la pantalla
cambió. Solo se reconstruyó `atipico-web`; `atipico-api` no se tocó.
