# CI/CD: GitHub Actions, Render (QA) y Azure (producción)

Especificación para automatizar compilación, pruebas y despliegue a dos entornos.

- **Estado:** **propuesto**, pendiente de aprobación.
- **Alcance:** CI en cada push, una imagen por commit publicada en un registro, despliegue
  automático a QA en Render y a producción en Azure Container Apps.
- **Fuera de alcance:** separar la base de datos de QA (queda para un ciclo próximo, §3.2),
  migraciones automatizadas (§8), telemetría en producción (ver
  [telemetria-postgres.md](telemetria-postgres.md) §7).
- **Sin migración de esquema:** no toca la base.

---

## 1. Decisiones tomadas

| Tema | Decisión |
|---|---|
| Construcción | **Una sola imagen por commit**, promovida entre entornos |
| Rama de QA | `develop` → Render |
| Rama de producción | `master` → Azure Container Apps |
| Base de datos | **Una sola, compartida** entre QA y producción (por ahora) |
| Registro de imágenes | `ghcr.io` |

## 2. La razón de "construir una vez"

`azd` con Aspire construye las imágenes con el soporte de contenedores del SDK de .NET. **No
usa los Dockerfiles del repositorio.**

Eso importa acá más que en un proyecto cualquiera, porque `Atipico.Web/Dockerfile` documenta
una mina ya pisada: restaurar con `--no-restore` deja fuera los static web assets del
framework de Blazor (`_framework/blazor.web.js`), la imagen publica sin ellos y la aplicación
tira 404 en runtime **sin ningún error de compilación**.

Si Render construyera desde el Dockerfile y Azure con el SDK, QA estaría aprobando un
artefacto distinto del que producción ejecuta. QA dejaría de significar nada.

Por eso: **GitHub Actions construye una vez, desde los Dockerfiles del repositorio, y los dos
entornos despliegan ese mismo digest.**

```
push a develop ──► CI (build + 119 pruebas)
                     └─► construye Api y Web desde los Dockerfiles
                          └─► push a ghcr.io   tags: sha-<sha>  +  qa
                               └─► Render (QA) despliega ese digest

merge a master ──► CI otra vez
                     └─► NO reconstruye: resuelve el digest de `qa`
                          └─► Azure Container Apps despliega ESE digest
                               └─► mueve el tag `prod`
```

## 3. Entornos

### 3.1 Qué es distinto entre QA y producción

| | QA (Render) | Producción (Azure) |
|---|---|---|
| Rama | `develop` | `master` |
| Cómputo | Render Web Service ×2 | Container Apps ×2 |
| Base de datos | **la misma** | **la misma** |
| Bucket R2 | ver §3.3 | `atipico-comprobantes` |
| Réplicas | 1 | 1 (§7) |

### 3.2 La base compartida, y lo que cuesta

Decisión explícita: por ahora QA y producción usan la misma base de Neon. La rama de Neon
queda para un ciclo próximo.

**Consecuencias que hay que tener presentes**, no para revertir la decisión sino para no
descubrirlas por sorpresa:

- Cualquier dato que se cree probando en QA queda en producción. Los pedidos de prueba
  aparecen en la lista real.
- **Una migración aplicada "para QA" está en producción en el mismo instante.** Ver §8.
- El índice `uk_pedido_comensal_activo` es global: un pedido de prueba con un nombre de
  comensal bloquea ese nombre para un pedido real mientras siga activo.

### 3.3 El bucket de R2 tiene que ser el mismo

`R2__Bucket` es configuración de entorno, no un dato de cada fila: `comprobante_pago` guarda
la clave del objeto y la aplicación la resuelve contra el bucket configurado.

Con base compartida y buckets distintos, un comprobante subido desde QA deja el archivo en
`atipico-comprobantes-dev` y la fila en la base que producción también lee. Producción busca
esa clave en `atipico-comprobantes` y no la encuentra: **enlace roto en producción, generado
desde QA.**

**Mientras la base sea compartida, QA usa `atipico-comprobantes`**, el mismo que producción.
Es incómodo pero es lo coherente: media separación es peor que ninguna. Cuando llegue la rama
de Neon, los buckets se separan junto con la base y no antes.

## 4. Los workflows

### 4.1 `ci.yml` — en cada push y cada PR

```yaml
name: CI
on:
  push:
    branches: [develop, master]
  pull_request:

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore Atipico.slnx
      - run: dotnet build Atipico.slnx --no-restore
      - run: dotnet test Atipico.slnx --no-build
```

Las 119 pruebas no necesitan base de datos ni Aspire: corren con `--no-build` sin servicios.
Comprobado.

`Atipico.Infraestructure.Tests` lee archivos de `sql/` subiendo directorios; funciona porque
`checkout` trae el repositorio entero.

### 4.2 `qa.yml` — push a `develop`

1. Espera a que CI pase.
2. `docker/build-push-action` construye `Atipico.Api/Dockerfile` y `Atipico.Web/Dockerfile`
   con `context: .` (los dos Dockerfiles esperan el árbol completo).
3. Publica en `ghcr.io/caverop/atipico-api` y `ghcr.io/caverop/atipico-web`, con tags
   `sha-<sha>` (inmutable) y `qa` (móvil).
4. Dispara el despliegue en Render apuntando a ese digest.

### 4.3 `prod.yml` — push a `master`

1. Espera a que CI pase.
2. **No construye nada.** Resuelve el tag `qa` a su digest y lo imprime en el log.
3. `azure/login@v2` con OIDC.
4. `az containerapp update --image ghcr.io/...@sha256:...` para Api y Web.
5. Mueve el tag `prod` a ese digest.

## 5. Azure: provisionar una vez, desplegar siempre

`azd` sirve para **crear** la infraestructura leyendo el AppHost, no para desplegar en cada
push — desplegar con `azd` volvería a construir la imagen y rompería el §2.

**Una sola vez, a mano:**

```bash
azd auth login
azd init
azd up
```

Eso crea el grupo de recursos, el entorno de Container Apps, Log Analytics y las dos
aplicaciones.

**Después, en cada release**, GitHub Actions solo cambia la imagen. Sin `azd` en el pipeline.

### 5.1 El AppHost necesita declarar la base

Hoy la cadena de conexión sale de user-secrets en desarrollo y de variables de entorno en
Render. El AppHost no la conoce, así que `azd` no tiene qué provisionar.

```csharp
var db = builder.AddConnectionString("atipico-db");

builder.AddProject<Projects.Atipico_Api>("atipico-api")
    .WithReference(db);
```

Es el `AddConnectionString` que veníamos postergando. Acá deja de ser opcional.

### 5.2 Container Apps tiene que poder leer de ghcr.io

Las imágenes son privadas. Hay que registrar en cada Container App una credencial de registro
con un PAT de GitHub con permiso `read:packages`.

La alternativa es Azure Container Registry, donde Container Apps se autentica con identidad
administrada y no hace falta ningún secreto — pero cuesta unos 5 USD/mes en Basic y además
Render necesitaría credenciales de ACR. Con `ghcr.io` el costo es cero y el secreto es uno
solo. **Se elige `ghcr.io`.**

## 6. Secretos

**En GitHub** (`AZURE_*` van sin contraseña, por OIDC federado):

| Secreto | Para qué |
|---|---|
| `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | `azure/login@v2` |
| `RENDER_API_KEY` | disparar el despliegue de QA |
| `GITHUB_TOKEN` | publicar en ghcr.io (lo provee Actions) |

**En Container Apps** (secretos de la aplicación, no del pipeline):
`ConnectionStrings__DefaultConnection`, `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`,
`Jwt__ExpiryMinutes`, `R2__AccountId`, `R2__AccessKeyId`, `R2__SecretAccessKey`, `R2__Bucket`,
y `ApiBaseUrl` en la Web.

Es la misma lista que `render.yaml` marca con `sync: false`.

> **Antes de sembrar las credenciales de R2 en Azure: rotarlas.** Están comprometidas desde una
> sesión anterior y no hay confirmación de que se hayan rotado. Copiarlas a un segundo sistema
> propaga el problema en vez de cerrarlo. Es el momento natural para hacerlo.

## 7. El llavero de Data Protection

Container Apps escala a varias réplicas por defecto. Con más de una, el llavero deja de ser
compartido y vuelve `The antiforgery token could not be decrypted` — pero intermitente, según
qué réplica atienda, que es mucho peor de diagnosticar.

**Por ahora: `minReplicas = maxReplicas = 1`.** Es coherente con la escala real (un cajero) y
no agrega infraestructura.

Cuando haga falta escalar, la salida en Azure es `PersistKeysToAzureBlobStorage` +
`ProtectKeysWithAzureKeyVault`, que `azd` puede provisionar. Es la misma discusión que en
Render costaba un disco pago; en Azure sí tiene una respuesta limpia. Queda anotada, no se
hace ahora.

## 8. Migraciones: siguen a mano, y ahora es más delicado

Los `sql/NNN_*.sql` se ejecutan a mano y esta especificación **no cambia eso**.

Pero con base compartida el riesgo sube: **no existe "aplicar la migración solo en QA".** En
cuanto el script corre, producción tiene el esquema nuevo mientras sigue ejecutando el código
viejo.

**Regla de trabajo:** las migraciones se aplican **antes** del merge a `master` y **solo si son
compatibles hacia atrás** — columnas nuevas anulables, constraints que el código viejo ya
respeta. Una migración que rompa el código en producción no puede ir antes que el despliegue.
Con base compartida, no hay forma de ordenarlas de otra manera.

CLAUDE.md ya advierte que un valor de enum sin su `CHECK` correspondiente *"falla
silenciosamente hasta que se ejercita en producción"*. Con base compartida, QA no protege de
eso.

Automatizar migraciones es candidato natural para el ciclo siguiente, junto con la rama de
Neon.

## 9. Limitaciones conocidas

**El tag `qa` es móvil.** Si alguien empuja a `develop` entre que QA aprueba y que se hace el
merge a `master`, se promueve la imagen más nueva, no la aprobada. El workflow **imprime el
digest que está promoviendo** para que se pueda verificar. La solución completa es promover
por digest explícito, y se puede agregar después si molesta.

**`main` está de más.** El repositorio tiene `master` (por defecto), `main` y
`caverop-separar-backend-frontend`. Con el modelo `develop`/`master`, `main` es ruido y
conviene borrarla antes de empezar, para que nadie empuje ahí por costumbre.

**La API de despliegue de Render con imagen fija hay que verificarla** contra el plan
contratado antes de escribir el workflow: `render.yaml` hoy declara `dockerfilePath`, y pasar
a imagen preconstruida cambia el tipo de servicio.

**`NU1903`.** `Microsoft.OpenApi 2.0.0` tiene una vulnerabilidad alta conocida. Hoy es solo
advertencia; el día que se active auditoría estricta, CI se cae. Conviene resolverlo antes de
que el pipeline lo vuelva bloqueante.

## 10. Orden de implementación

Cada paso deja el sistema en un estado usable. Se puede parar en cualquiera.

1. **`ci.yml`.** No depende de nada, no ata a nada, y hoy no hay ninguna red de seguridad.
2. **Borrar la rama `main`** y crear `develop` desde `master`.
3. **Construcción y publicación a ghcr.io** desde `develop`, sin desplegar todavía. Verificar
   que las imágenes arrancan (`docker run` local contra la imagen publicada) — sobre todo la
   Web, por lo de los static web assets del §2.
4. **QA en Render desde el registro.** Cambiar `render.yaml` a imagen preconstruida.
5. **`AddConnectionString` en el AppHost** (§5.1).
6. **`azd up` a mano**, una vez. Verificar producción con una réplica.
7. **`prod.yml`**, promoviendo el digest.

Los pasos 1 a 4 dan valor sin tocar Azure. Los pasos 5 a 7 son la mitad de Azure y se pueden
hacer en otro momento.
