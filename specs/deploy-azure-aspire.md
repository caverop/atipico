# Deploy a Azure con Aspire y azd

Estado del despliegue de Atipico en Azure Container Apps, qué se arregló para llegar hasta
acá, y qué queda pendiente.

- **Estado:** **funcionando y verificado** contra el entorno real. `atipico-web--0000003` y
  `atipico-api--0000003` en `Running`, login y navegación operativos.
- **Alcance:** un solo entorno (`Atipico`), con credenciales de desarrollo. No hay separación
  dev/prod todavía (§5.3).
- **Infraestructura:** generada por `azd` a partir de `AppHost.cs`. No hay bicep en el
  repositorio; si alguna vez se corre `azd infra gen`, esos archivos pasan a ser la fuente de
  verdad y `AppHost.cs` deja de regenerarlos solo.

---

## 1. Qué hay desplegado

Grupo de recursos `rg-Atipico`, región `eastus2`:

| Recurso | Nombre | Para qué |
|---|---|---|
| Container Apps Environment | `cae-bsn3xi2hasatq` | aloja las dos apps |
| Container Registry | `acrbsn3xi2hasatq` | imágenes construidas por `azd` |
| Log Analytics | `law-bsn3xi2hasatq` | logs de consola y de sistema |
| Managed Identity | `mi-bsn3xi2hasatq` | pull del ACR; se reusa en §5.2 |
| Container App | `atipico-web` | **público** |
| Container App | `atipico-api` | **interno** |

El sufijo `bsn3xi2hasatq` es el token de nombres que `azd` derivó del nombre del entorno y
guardó en `.azure/Atipico/.env`. Mientras ese archivo exista, `azd up` reusa los mismos
recursos en vez de crear otros.

No hay Key Vault. Los secretos quedaron como *secrets del propio Container App*
(`secretRef: jwt--key`, `connectionstrings--defaultconnection`, …), que es lo que hace `azd`
con los parámetros `secret: true`. Para rotar uno hay que re-provisionar, no editar un vault.

---

## 2. Los cuatro problemas que había, y por qué

### 2.1 La API no arrancaba: `ActivationFailed`, exit code 139

```
System.InvalidOperationException: Falta configurar Jwt:Key.
   at Program.<Main>$(String[] args) in Atipico.Api\Program.cs:line 75
```

El contenedor tenía cuatro variables de entorno y ninguna era de la aplicación. La causa es
que `AppHost.cs` no le pasaba nada: los valores estaban puestos en el entorno de `azd`
(`azd env set DB_CONNECTION_STRING=…`, `JWT_KEY=…`), pero **`azd` no reenvía su propio
entorno al contenedor**. Ese `.env` alimenta los parámetros de la infraestructura; lo único
que llega a la aplicación es lo que el AppHost declara.

En local no se notaba porque ahí `Atipico.Api` lee sus user-secrets y su
`appsettings.Development.json`, y ninguno de los dos existe dentro de la imagen publicada.

**Arreglado** declarando parámetros de Aspire y enganchándolos con `WithEnvironment` (§3).

Detalle que vale recordar: los nombres usan **doble guion bajo**, no dos puntos. `Jwt__Key`,
no `Jwt:Key`. Así expresa una sección anidada el proveedor de variables de entorno de .NET, y
`:` ni siquiera es un nombre de variable válido en Linux. Escrito mal, el contenedor arranca
igual pero no ve el valor, que es exactamente el fallo de arriba.

### 2.2 La topología estaba invertida

`WithExternalHttpEndpoints()` estaba solo en la API. Resultado: la API expuesta a internet, y
el frontend —que es lo único que un usuario necesita alcanzar— sin ingress público
(`atipico-web.internal.…`).

**Arreglado**: el modificador pasó a `atipico-web`. La API quedó interna, alcanzable solo
desde adentro del Container Apps Environment.

### 2.3 Data Protection sin permiso de escritura

Con la API ya arriba, cualquier página devolvía la pantalla de error de Producción:

```
System.UnauthorizedAccessException: Access to the path '/var/atipico' is denied.
 ---> System.IO.IOException: Permission denied
```

`Atipico.Web/Program.cs` guarda el llavero de Data Protection en `/var/atipico/keys`. En
`docker-compose.yml` eso funciona porque hay un volumen montado ahí. En Container Apps no hay
nada montado (`volumes: null`) **y el contenedor no corre como root**, así que ni siquiera
puede crear el directorio.

El comentario que había en `Program.cs` decía que si no se monta nada *"escribe dentro del
contenedor… no empeora nada"*. Esa suposición es la que produjo el bug: da por hecho permiso
de escritura en `/var`, que fuera de root no existe.

Se cae **toda** la aplicación, no solo el login, porque Blazor Server cifra con Data
Protection el estado de los componentes prerenderizados
(`ServerComponentSerializer.CreateSerializedServerComponent` → `Protect`). Cada render de
cada página tira antes de emitir HTML.

**Mitigado, no resuelto**: la ruta apunta a `/tmp/atipico-keys`, que sí es escribible. El
arreglo de verdad es §5.2.

### 2.4 Riesgo que no se materializó

`ApiBaseUrl` quedó en `https://atipico-api.internal.…`, el endpoint interno por HTTPS. Había
dudas de que el certificado del ingress interno fuera válido desde adentro del contenedor.
**Funciona.** No hace falta bajar a `api.GetEndpoint("http")`.

---

## 3. Cómo se resuelven las credenciales

Un `builder.AddParameter(...)` en `AppHost.cs` se resuelve en un lugar distinto según dónde
corra el AppHost, sin ramas ni `#if DEBUG`:

| | `aspire run` / F5 | `azd up` |
|---|---|---|
| Origen del valor | `Parameters:<nombre>` de la configuración del AppHost → **user-secrets del AppHost** | el entorno de `azd` activo |
| Dónde termina el secreto | máquina del desarrollador | *secret* del Container App, inyectado como `secretRef` |
| Qué queda en el repositorio | nada | nada |

Parámetros declarados hoy:

| Parámetro | `secret` | Variable que recibe la aplicación |
|---|---|---|
| `db-connection-string` | sí | `ConnectionStrings__DefaultConnection` |
| `jwt-key` | sí | `Jwt__Key` |
| `r2-access-key-id` | sí | `R2__AccessKeyId` |
| `r2-secret-access-key` | sí | `R2__SecretAccessKey` |
| `r2-account-id` | no | `R2__AccountId` |
| `r2-bucket` | no | `R2__Bucket` |

`Jwt__Issuer`, `Jwt__Audience` y `Jwt__ExpiryMinutes` van literales en `AppHost.cs`: no son
secretos ni cambian entre entornos, y hacerlos parámetros solo agregaría preguntas al
`azd up`.

Cargar los valores en local:

```powershell
dotnet user-secrets set "Parameters:jwt-key" "<clave>" --project Atipico.Aspire.AppHost
# …ídem para los otros cinco
```

---

## 4. Comandos

Lo que decide qué comando corre no es si tocaste `AppHost.cs`, sino si cambia la
**infraestructura compartida**:

| Qué cambió | Comando |
|---|---|
| Código C#/Razor de `Atipico.Api` o `Atipico.Web` | `azd deploy` |
| Definición del Container App: variables de entorno, réplicas, ingress | `azd deploy` |
| Parámetros nuevos, recursos nuevos, referencias nuevas entre proyectos | `azd up` |

`azd up` es `package` + `provision` + `deploy`. Sobre infraestructura que no cambió, el
provision es un despliegue incremental que no hace nada: no es más riesgoso que `azd deploy`,
solo más lento.

Diagnóstico cuando algo falla:

```powershell
az containerapp revision list -g rg-Atipico -n atipico-web -o table
az containerapp logs show -g rg-Atipico -n atipico-web --type console --tail 100
az containerapp logs show -g rg-Atipico -n atipico-api --type system  --tail 30
```

La página *"Development Mode / Swapping to Development environment…"* no dice nada por
diseño: es el manejador de errores de Producción. El error real está siempre en el log de
consola.

---

## 5. Pendientes

### 5.1 Rotar `Jwt:Key` — prioridad alta

`Atipico.Api/appsettings.Development.json` está **commiteado** y contiene una clave real,
presente en el historial desde `1d4b786` (2026-08-11). Se verificó que es **la misma clave**
que hoy firma los tokens en Azure. Cualquiera con acceso al repositorio puede emitir tokens
válidos contra la API desplegada.

1. Generar dos claves nuevas y distintas, una para dev y otra para Azure.
2. Sacar el bloque `Jwt` de `appsettings.Development.json` —o dejar solo `Issuer`,
   `Audience` y `ExpiryMinutes`; la `Key` no— y pasarla a los user-secrets de la API.
3. Cargar la de Azure en el parámetro `jwt-key` y re-provisionar.

Borrarla del archivo no la borra del historial: la clave vieja hay que darla por comprometida
pase lo que pase.

De paso: `Jwt__ExpiryMinutes` está en `480` en `AppHost.cs` y en `1440` en
`appsettings.Development.json`. No es un error, pero conviene que la diferencia sea
deliberada.

### 5.2 Llavero de Data Protection en Blob Storage — prioridad alta

Hoy las claves viven en `/tmp` dentro del contenedor. Eso significa:

- **Cada deploy desloguea a todos.** Revisión nueva, llavero nuevo, cookies anteriores
  ilegibles.
- **Con más de una réplica se rompe de forma intermitente.** `maxReplicas` está en **10** y no
  hay reglas de escalado explícitas. Se intentó fijarlo con `WithReplicas(1)`, pero se
  verificó contra la revisión desplegada que **eso no capea `maxReplicas`**: solo fija las
  réplicas del momento. La cookie emitida por una réplica no la puede descifrar otra, y
  aparece `The antiforgery token could not be decrypted` sin patrón reproducible.

Blob Storage es la respuesta canónica para Container Apps: no monta ningún share, y se
autentica con la managed identity que ya existe. `azd` ya inyecta `AZURE_CLIENT_ID` con el
client id de `mi-bsn3xi2hasatq` en los dos contenedores, así que `DefaultAzureCredential`
toma esa identidad sin configuración adicional.

**Paquetes** en `Atipico.Web`:

```
Azure.Extensions.AspNetCore.DataProtection.Blobs
Azure.Identity
```

**Código** en `Atipico.Web/Program.cs`, reemplazando el `PersistKeysToFileSystem` actual. La
bifurcación importa: mantiene funcionando docker-compose y `dotnet run` suelto, que es la
misma filosofía que ya sigue `ApiBaseUrl`.

```csharp
var dp = builder.Services.AddDataProtection().SetApplicationName("Atipico");

var blobUri = builder.Configuration["DataProtection:BlobUri"];
if (!string.IsNullOrWhiteSpace(blobUri))
    // Azure: un único blob compartido por todas las réplicas y estable entre revisiones.
    dp.PersistKeysToAzureBlobStorage(new Uri(blobUri), new DefaultAzureCredential());
else
    // docker-compose / local: el volumen de siempre.
    dp.PersistKeysToFileSystem(new DirectoryInfo(
        builder.Configuration["DataProtection:KeysPath"] ?? "/var/atipico/keys"));
```

**Infraestructura.** Dos caminos:

- *Manual*, con `az`: crear la cuenta de storage y el contenedor de blobs, y darle el rol
  `Storage Blob Data Contributor` a `mi-bsn3xi2hasatq` sobre ese contenedor. Después pasar la
  URL del blob con `WithEnvironment("DataProtection__BlobUri", "…")` en `AppHost.cs`. Es
  rápido, pero la cuenta queda fuera de lo que `azd` gestiona: hay que recrearla a mano en
  cada entorno nuevo.
- *Con Aspire* (`Aspire.Hosting.Azure.Storage`): declarar el storage como recurso en
  `AppHost.cs` y referenciarlo desde `atipico-web`. `azd` lo provisiona y resuelve los
  permisos por entorno. Es más trabajo la primera vez y **se paga solo al crear `prod`**
  (§5.3), porque ahí el storage aparece sin intervención.

Dado que §5.3 está pendiente, conviene la segunda. Cualquiera de las dos cambia
infraestructura, así que va con `azd up`, no `azd deploy`.

Una vez persistido afuera, `WithReplicas(1)` se puede sacar y dejar que ACA escale.

### 5.3 Separar dev de prod — prioridad media

Hoy no hay separación. Se verificó que el entorno `Atipico` usa **la misma base** que los
user-secrets locales y el bucket `atipico-comprobantes-dev`. Un `DELETE` de prueba en
desarrollo toca los datos que sirve el sitio publicado.

```powershell
azd env new prod
azd up                   # pide los 6 parámetros; cargar los valores de producción
azd env select Atipico   # para volver
```

Un entorno nuevo crea su propio grupo de recursos, ACR, Log Analytics y Container Apps
Environment: cuesta aparte. Antes de correrlo hay que tener creados la base de producción, el
bucket de producción y la `Jwt:Key` nueva. Si no, se termina con un entorno llamado "prod"
que apunta a los datos de dev, pagando dos veces por lo mismo.

Orden recomendado: **5.1 → 5.2 → 5.3**. Los dos primeros se arreglan sobre el entorno que ya
funciona; recién con eso verde conviene clonar la configuración a `prod`, para no estar
debuggeando dos cosas a la vez.

### 5.4 Menor

- **`Microsoft.OpenApi` 2.0.0** tiene una vulnerabilidad de severidad alta conocida
  (`NU1903`, GHSA-v5pm-xwqc-g5wc). Aparece como advertencia en cada build.
- Las **revisiones fallidas** (`atipico-api--0000001`, `--b4vzzgx`) siguen en el historial como
  inactivas. No consumen réplicas ni molestan; se pueden borrar cuando estorben.
- Las credenciales de **R2** en Azure son las de desarrollo, igual que la base. Se resuelve
  junto con §5.3.
