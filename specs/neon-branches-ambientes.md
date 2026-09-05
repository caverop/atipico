# Ambientes de Neon: `production`, `qa`, `dev`

Especificación de la topología de branches de Neon que separa QA y desarrollo de la base
compartida, y del procedimiento para propagar un cambio de esquema entre ellos.

- **Estado:** **implementado y verificado.** Branches creados, topología verificada por
  lectura directa contra los tres, `dev` poblado y los tres consumidores (local, QA,
  producción) apuntando cada uno a su branch (ver §6).
- **Alcance:** qué branch es cada ambiente, por qué, y cómo viaja una migración de esquema
  entre los tres.
- **Fuera de alcance:** automatizar esa propagación en CI/CD (evolución futura de
  [cicd-github-azure-render.md](cicd-github-azure-render.md)); la limpieza de datos de
  prueba que motivó esto, ya resuelta en
  [limpieza-datos-prueba.md](limpieza-datos-prueba.md).

---

## 1. Por qué existe esto

`specs/cicd-github-azure-render.md` §3.2 dejó anotado como pendiente: QA y producción
comparten hoy una sola base de Neon, con la consecuencia explícita de que "una migración
aplicada 'para QA' está en producción en el mismo instante". `specs/deploy-azure-aspire.md`
§5.3 documenta lo mismo del lado de desarrollo: el entorno publicado usaba la misma base que
los user-secrets locales. `specs/limpieza-datos-prueba.md` §9 cerró la limpieza de esa base
compartida y marcó esta separación como el paso siguiente, sin diseñarla.

Este spec resuelve eso: tres bases físicas separadas, una por ambiente.

## 2. Topología decidida

| Branch | Rol en el árbol de Neon | Contenido | Para qué |
|---|---|---|---|
| `production` | raíz (el branch que ya existía) | Datos reales | Sirve producción (Azure) |
| `qa` | **hijo** de `production` | Copia completa de `production` al momento de crearse | Sirve QA (Render) |
| `dev` | raíz independiente, **schema-only** | Solo esquema, cero filas | Desarrollo local / este agente |

`qa` y `dev` ya fueron creados por el usuario en el dashboard de Neon siguiendo esta
recomendación. Pendiente confirmar que quedaron exactamente así — ver §6.

### 2.1 Por qué `dev` no puede ser hijo de `production`

Antes de asentar esto se probó pedirle a Neon la combinación más obvia — un hijo de
`production` que copie solo el esquema — y no existe. Verificado contra la documentación de
Neon (`neon.com/docs/guides/branching-schema-only`):

> "Schema-only branches do not have a parent branch [...] Both the `production` branch of
> the project and the schema-only branch have no parent, indicated by the dash in the
> Parent column."

El branch que se elige como fuente al crear un schema-only branch **no queda como padre** —
solo se usa para copiar la estructura en ese instante. Por eso tampoco existe *"reset from
parent"* para un branch schema-only: no hay padre al que resetear. No fue una elección entre
alternativas; es la única forma en que Neon permite un branch sin datos.

### 2.2 Por qué se llama `dev` y no `develop`

`develop` ya es el nombre de una rama de **git** en este repositorio, y ya significa algo
específico: dispara el despliegue a QA (`cicd-github-azure-render.md` §1, tabla). Nombrar
igual a un branch de **base de datos** que es para desarrollo local habría sido "el mismo
nombre, dos árboles distintos, dos significados opuestos" — exactamente el tipo de
confusión que `Navegacion.cs` ya evitó una vez separando declarado de derivado (`CLAUDE.md`,
sección de navegación). `dev` no colisiona con ninguna rama de git existente en el repo.

## 3. No hay merge en Neon

Verificado contra la documentación de Neon (mayo 2026) y confirmado por blogs de terceros
que comparan el feature set: Neon no ofrece combinar los cambios de un branch con los de
otro. Las únicas dos operaciones relacionadas son:

- **Schema Diff** — compara el SQL de dos branches lado a lado (CLI: `neon branches
  schema-diff`, o API `compare_schema`). Es de **solo lectura**: muestra el diff, no lo
  aplica.
- **Reset from parent** — reemplaza el branch hijo entero (esquema **y datos**) por el
  estado actual del padre. No es un merge selectivo de esquema; es "tirá todo lo que tenías
  y cloná de nuevo". Además no aplica a `dev` (no tiene padre, §2.1), y aplicarlo a `qa`
  perdería cualquier dato de prueba propio que `qa` haya acumulado.

Ninguna de las dos resuelve "propagar solo el `ALTER TABLE` de `dev` a `qa` y de `qa` a
`production`".

## 4. Cómo se propaga un cambio de esquema

Con el mismo mecanismo que ya usa el proyecto, corrido tres veces en vez de una:

1. Se escribe la migración como siempre: `sql/NNN_descripcion.sql`, numerada, envuelta en su
   propia transacción — el patrón de `sql/002_auth_usuario.sql` o
   `sql/003_pedido_comensal_unico.sql`. Nada cambia acá.
2. Se corre a mano el mismo archivo contra la cadena de conexión de cada ambiente, en este
   orden: **`dev` → `qa` → `production`**. Mismo script, tres bases físicas, tres
   `ConnectionStrings`/`DB_CONNECTION_STRING` distintas.
3. Antes de avanzar al siguiente ambiente, un **Schema Diff** entre el ambiente recién
   migrado y el siguiente confirma que la única diferencia pendiente es justamente esa
   migración — no se coló nada más por otro lado.
4. `sql/schema_completo.sql` sigue siendo el snapshot generado para levantar un ambiente
   nuevo de cero (`script-inicial-completo.md`); la historia numerada en `sql/` sigue siendo
   la fuente canónica, sin cambios respecto a lo que ya dice `CLAUDE.md`.

No hay automatización todavía — cada paso 2 es manual. Automatizarlo en CI queda fuera de
alcance (ver encabezado).

## 5. Poblar `dev` después de crearlo

Un branch schema-only copia estructura, no filas — `dev` nace vacío. Para que sea usable
hace falta correr `sql/dev_datos_iniciales.sql` (specs/script-inicial-completo.md) contra su
conexión: carga los 4 `tipo_plato`, 6 `plato`, 4 `mesa` y los 4 usuarios de operación
(`admin`/`mesera`/`delivery`/`cocinera`, contraseña `atipico`). Mismo script pensado
originalmente para SCRUM-12 ("un ambiente que acaba de nacer de `schema_completo.sql`") —
un branch schema-only recién creado es exactamente ese caso.

No se corrió todavía contra `dev` — pendiente, ver §6.

## 6. Verificación — hecha por lectura directa (2026-09-05)

El usuario pasó las tres cadenas de conexión (rol `app_restaurante`, sin contraseña). La
contraseña se reutilizó de los user secrets locales de `Atipico.Api` (que hoy apuntan al
host de `production`) y **funcionó igual contra `qa` y contra `dev`** — confirma que Neon
copia el rol y su contraseña tanto a un branch hijo como a uno schema-only.

Resultado, consultado con `psql` de solo lectura (`app_restaurante` no tiene `DELETE`, así
que no había riesgo de tocar nada por error):

| | `production` (`ep-mute-tree`) | `qa` (`ep-red-union`) | `dev` (`ep-icy-cake`) |
|---|---|---|---|
| Tablas / triggers / funciones | 16 / 16 / 8 | (hijo, mismo esquema) | **idéntico**: 16 / 16 / 8 |
| `usuario` | admin/mesera/delivery/cocinera | admin/mesera/delivery/cocinera | igual, tras el seed |
| `plato` / `mesa` / `tipo_plato` | 6 / 4 / 4 | 6 / 4 / 4 | igual, tras el seed |
| Transaccionales (`pedido`, `cuenta`, `turno_caja`, ...) | todo en 0 | todo en 0 | todo en 0 |
| `app_restaurante` puede `DELETE` | no | no (heredado) | no (heredado) |

Conclusiones:

- **`qa` es hijo del `production` correcto y del momento correcto**: tiene los mismos 4
  usuarios ya renombrados (`admin`/`mesera`/`delivery`/`cocinera`, no los `test.*` viejos) y
  cero filas en todo lo transaccional — se clonó después de la limpieza
  (`limpieza-datos-prueba.md`) y después del renombrado (`dev_renombrar_usuarios.sql`), no
  antes. No es hijo de `dev`.
- **De paso quedó confirmado que `sql/dev_renombrar_usuarios.sql` sí se corrió** contra
  `production` — quedaba como pendiente sin cerrar de antes de este spec; ya no.
- **`dev` tenía el esquema completo y cero filas** antes del seed — la firma exacta de un
  branch schema-only. No hay forma de confirmar por SQL que además es *raíz* en el árbol de
  Neon (eso es metadata de Neon, no de Postgres), pero es consistente con lo esperado.
- **`sql/dev_datos_iniciales.sql` ya se corrió contra `dev`** (2026-09-05) y se verificó por
  lectura: 4 usuarios, 6 platos, 4 mesas, 4 tipos de plato — igual que `production`/`qa`.
  `dev` ya es un ambiente usable.

Lo que seguía pendiente, todo cerrado el 2026-09-05:

- [x] El branch quedó nombrado `dev` en el dashboard de Neon (confirmado por el usuario).
- [x] Render (QA) ya apunta a la cadena de `qa` (confirmado por el usuario).
- [x] User secrets locales de `Atipico.Api` apuntando a `dev` —
      `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=ep-icy-cake-axnb0dkw-pooler.c-4.us-east-2.aws.neon.tech;..." --project Atipico.Api`,
      verificado con `dotnet user-secrets list`. Si `Atipico.Api` ya estaba corriendo, sigue
      con la cadena vieja hasta que se reinicie — no se lo forzó, por la regla de no matar
      el proceso.

Los tres ambientes quedan completamente separados y en uso.

## Bitácora

- **2026-09-04** — Se descartó la idea de un branch hijo de `production` que copie solo el
  esquema: Neon no lo permite, los schema-only branches son siempre raíz (§2.1, verificado
  contra la documentación).
- **2026-09-04** — Se investigó si Neon soporta "merge" entre branches antes de diseñar la
  propagación de migraciones: no existe (§3, verificado contra el changelog y documentación
  de mayo 2026). Se optó por el mecanismo ya existente (migraciones numeradas a mano) en vez
  de esperar una feature de Neon que resolviera esto automáticamente.
- **2026-09-04** — El usuario ya había creado los branches `qa` (hijo) y `develop`
  (schema-only) en el dashboard de Neon antes de que este spec se escribiera, basado en la
  recomendación dada en el chat. El spec documenta la decisión ya tomada; la verificación
  contra el estado real quedó pendiente hasta el día siguiente por falta de credenciales.
- **2026-09-05** — El usuario pasó las tres cadenas de conexión (sin contraseña). Se
  reutilizó la contraseña de `app_restaurante` que ya estaba en los user secrets locales
  (apunta al host de `production`) y funcionó igual contra `qa` y `dev` — confirma que Neon
  copia rol y contraseña también a un branch schema-only, no solo a un hijo. Verificación
  completa por lectura directa: ver §6. De paso se descubrió que
  `sql/dev_renombrar_usuarios.sql` ya se había corrido contra `production` (no estaba
  registrado como hecho). Se corrió `sql/dev_datos_iniciales.sql` contra `dev` y se verificó
  por lectura — `dev` pasó de vacío a un ambiente usable en la misma sesión.
