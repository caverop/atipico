# Dominio personalizado `atipico.com.bo` en Container Apps

El dominio se pierde en cada despliegue. Este spec documenta por qué pasa, qué se verificó
contra el entorno real, y el arreglo elegido.

- **Estado:** implementado en el repositorio y **compilando**. Falta el `azd provision` y la
  prueba contra el dominio real: los criterios 2 a 5 de §6 siguen **sin verificar**, y los
  pasos 4 y 5 de §5 (variables en el entorno azd y en GitHub) son a mano.
- **Síntoma:** `https://atipico.com.bo/` devuelve **HTTP 525** después de cada push a `main`.
- **Alcance:** entorno `Atipico` (el único que existe hoy, ver §5.3 de
  [deploy-azure-aspire.md](deploy-azure-aspire.md)). Un solo dominio, el ápice.

---

## 1. Qué se verificó

Todo lo de esta sección se comprobó contra Azure y contra el DNS real el **2026-09-03**, no
se dedujo del código.

| Comprobación | Comando | Resultado |
|---|---|---|
| Vinculación del dominio | `az containerapp show -n atipico-web -g rg-Atipico --query properties.configuration.ingress` | `"customDomains": null` |
| Certificado administrado | `az containerapp env certificate list -n cae-bsn3xi2hasatq -g rg-Atipico` | `atipico.com.bo-rg-atipi-260902203532`, subject `atipico.com.bo`, `Succeeded` — **vivo** |
| DNS del ápice | `nslookup atipico.com.bo` | `172.67.141.228`, `104.21.87.53` (+ AAAA) → **Cloudflare**, no Azure |
| Validación de dominio | `nslookup -type=TXT asuid.atipico.com.bo` | `96F96F62…19381` — presente |
| El sitio por el dominio | `curl -I https://atipico.com.bo/` | **525**, `Server: cloudflare` |
| El sitio por el FQDN de ACA | `curl https://atipico-web.thankfulplant-c1b6b8b5.eastus2.azurecontainerapps.io/` | **302** — la app está sana |
| Infraestructura en el repo | `find . -name "*.bicep"` | no hay ninguno |
| Entorno de azd versionado | `git ls-files .azure` | vacío; `.azure` está en `.gitignore` |

La asimetría entre las dos primeras filas es el diagnóstico entero: **el certificado quedó y
la vinculación no.**

## 2. La causa

En Container Apps esas dos cosas viven en recursos distintos:

- el **certificado administrado** cuelga del *Container Apps Environment* (`cae-bsn3xi2hasatq`);
- la **vinculación** del hostname cuelga del *Container App* (`atipico-web`), dentro de
  `properties.configuration.ingress.customDomains`.

El dominio se ató a mano desde el portal, pero la infraestructura de `atipico-web` **la
genera `azd` a partir de `AppHost.cs`**: no hay bicep en el repositorio. El workflow
[`azure-dev.yml`](../.github/workflows/azure-dev.yml) corre `azd provision` en **cada push a
`main`**, y ese provision aplica un template donde `ingress` no declara `customDomains`. ARM
no fusiona lo que encuentra con lo que el template pide: **reemplaza**. Todo lo que se agregó
por fuera del template desaparece.

El certificado sobrevive porque está en otro recurso, que el template de la app no toca.

Que el síntoma sea un **525** y no un 404 lo confirma desde el otro extremo. Cloudflare está
adelante (proxy activo: el ápice resuelve a IPs de Cloudflare, no al FQDN de ACA) y abre TLS
contra el origen con SNI `atipico.com.bo`. Sin la vinculación, ACA no tiene certificado que
presentar para ese nombre, el handshake falla, y Cloudflare traduce eso a 525 —
"SSL handshake failed" contra el origen. No es un problema de DNS ni del record: el record
está bien y el `asuid` también.

**Precisión que importa para el arreglo:** `azd deploy` **no** rompe nada. Los dominios son
del Container App, no de la revisión, así que publicar una imagen nueva los respeta. El
único que los pisa es `azd provision`. Por eso el sitio aguanta un tiempo y se cae en el
push siguiente, que es lo que hace que el patrón se lea como aleatorio.

## 3. El arreglo elegido

Si la vinculación no está declarada en la misma fuente que genera la infraestructura, se va
a perder de nuevo. Cualquier arreglo que la reponga *después* está peleando contra el
template en vez de arreglarlo.

Aspire tiene API de primera clase: `ConfigureCustomDomain`, dentro de
`PublishAsAzureContainerApp`. El binding pasa a formar parte del template generado, y
entonces `azd provision` lo **mantiene** en vez de borrarlo.

```csharp
var dominio = builder.AddParameter("custom-domain");
var certificado = builder.AddParameter("certificate-name");

builder.AddProject<Projects.Atipico_Web>("atipico-web")
    .WithExternalHttpEndpoints()
    // …lo que ya está…
    .PublishAsAzureContainerApp((infra, app) =>
    {
        app.ConfigureCustomDomain(dominio, certificado);
    });
```

Dos parámetros y no dos literales, por la misma razón que ya documenta `AppHost.cs` para las
credenciales: el valor cambia entre entornos y el archivo tiene que servir para `prod` sin
ramas. El entorno `prod` de §5.3 va a tener otro dominio y otro certificado.

**Valores para el entorno `Atipico`:**

| Parámetro | Valor |
|---|---|
| `custom-domain` | `atipico.com.bo` |
| `certificate-name` | `atipico.com.bo-rg-atipi-260902203532` |

`certificate-name` es el **nombre del recurso**, no el subject ni el thumbprint. El
certificado ya existe y está `Succeeded`, así que se referencia el que hay; no hay que
emitir uno nuevo.

### 3.1 Paquete

`Aspire.Hosting.Azure.AppContainers`, versión **13.5.2** para que empareje con
`Aspire.AppHost.Sdk/13.5.2` del `.csproj`. Verificado en la API de NuGet el 2026-09-03: la
13.5.2 existe (la última es 13.5.3).

> La documentación de Aspire nombra el paquete como `Aspire.Hosting.AzureContainerApps`. **Ese
> nombre no existe en NuGet** — devuelve 404. El correcto es `Aspire.Hosting.Azure.AppContainers`.
> Anotado acá para no volver a perder el rato buscándolo.

`ConfigureCustomDomain` fue experimental (`ASPIREACADOMAINS001`) y **dejó de serlo en Aspire
13.5**. Como el proyecto está en 13.5.2, no debería hacer falta suprimir el diagnóstico. Si
el build igual lo reporta, la supresión va en `.editorconfig`, no con un `#pragma` suelto.

### 3.2 Los parámetros en CI

`.azure` está en `.gitignore`, así que el runner de GitHub Actions no tiene el `config.json`
donde viven los valores locales: **azd los toma de variables de entorno**. La convención se
lee de los seis parámetros que hoy funcionan (`r2-bucket` → `AZURE_R2_BUCKET`,
`db-connection-string` → `AZURE_DB_CONNECTION_STRING`, …): kebab-case a UPPER_SNAKE con
prefijo `AZURE_`.

Ninguno de los dos es secreto, así que van como `vars` de GitHub y no como `secrets`, igual
que `AZURE_R2_BUCKET`:

- `AZURE_CUSTOM_DOMAIN`
- `AZURE_CERTIFICATE_NAME`

**El nombre que se usa es siempre el de la variable, nunca el del parámetro.** `azd env set`
escribe en `.azure/<entorno>/.env`, que es un dotenv y **no admite guiones** en el nombre;
`azd env set custom-domain …` deja el archivo corrupto y a partir de ahí *todo* comando de
azd falla con `unexpected character "-" in variable name`. Se arregla borrando esa línea del
`.env` a mano. Los parámetros propiamente dichos viven en `config.json` bajo
`infra.parameters` y con **guión bajo** (`r2_bucket`, `db_connection_string`), pero ahí los
escribe azd solo, al preguntar; no se editan a mano.

Localmente se usan exactamente los mismos nombres que en CI, y esa es la gracia: un solo
mecanismo en los dos lados.

```powershell
azd env set AZURE_CUSTOM_DOMAIN atipico.com.bo
azd env set AZURE_CERTIFICATE_NAME atipico.com.bo-rg-atipi-260902203532
```

En el workflow van en el bloque `env:` **del job**, y ahí termina. Los pasos `provision` y `deploy` repiten
los `secrets` en su propio `env:` porque los secretos no se pueden declarar a nivel de job;
las `vars` sí, y por eso `AZURE_R2_BUCKET` aparece una sola vez. Estos dos siguen esa mitad
del patrón, no la otra.

**Si esto se omite, `azd provision --no-prompt` falla** por parámetro faltante: el despliegue
se rompe entero en vez de perder el dominio, que al menos es un fallo ruidoso.

## 4. Diseños descartados

**Re-vincular después del provision, en el workflow.** Un paso
`az containerapp hostname bind` después de `azd provision`. Es el cambio más chico y no toca
el código. Se descartó por tres razones: el dominio queda caído en la ventana entre el
provision y el rebind (que es justo cuando la gente nota el deploy); el nombre del
certificado queda hardcodeado en el YAML en vez de ser un parámetro por entorno; y no se
replica solo al crear `prod`. Es reponer lo que el template borra, en lugar de arreglar el
template.

**`azd infra gen`.** Materializar el bicep a disco y editarlo a mano. Funciona, pero el
propio [spec de deploy](deploy-azure-aspire.md) ya advierte el costo, y es el más caro de
todos: los archivos generados pasan a ser la fuente de verdad y `AppHost.cs` deja de
regenerarlos. Todo cambio futuro de infraestructura habría que reflejarlo a mano. Pagar eso
para agregar un dominio es desproporcionado.

**Sacar el dominio de ACA y resolverlo solo en Cloudflare.** No es posible: ACA rutea por
`Host`, así que sin la vinculación devuelve error para ese hostname aunque Cloudflare llegue
bien al origen. La vinculación hace falta sí o sí.

## 5. Plan de cambios

| # | Capa | Archivo | Cambio |
|---|---|---|---|
| 1 | AppHost | `Atipico.Aspire.AppHost/Atipico.Aspire.AppHost.csproj` | `PackageReference` a `Aspire.Hosting.Azure.AppContainers` 13.5.2 |
| 2 | AppHost | `Atipico.Aspire.AppHost/AppHost.cs` | dos parámetros nuevos + `PublishAsAzureContainerApp` con `ConfigureCustomDomain` en `atipico-web` |
| 3 | CI | `.github/workflows/azure-dev.yml` | `AZURE_CUSTOM_DOMAIN` y `AZURE_CERTIFICATE_NAME` en el `env:` del job |
| 4 | Entorno azd | (local, no versionado) | `azd env set AZURE_CUSTOM_DOMAIN …` / `AZURE_CERTIFICATE_NAME …` para poder provisionar desde la máquina |
| 5 | GitHub | (fuera del repo) | crear las dos `vars` del repositorio |

Los pasos 4 y 5 no son código y no los puede hacer el agente: van a mano.

## 6. Criterios de aceptación

1. `dotnet build -c Release` compila sin errores nuevos ni `ASPIREACADOMAINS001`.
2. Después de un `azd provision`, `az containerapp show -n atipico-web -g rg-Atipico --query
   properties.configuration.ingress.customDomains` devuelve una entrada con
   `name: atipico.com.bo` y `bindingType: SniEnabled` — **no `null`**.
3. `curl -I https://atipico.com.bo/` devuelve **302** (o 200 en `/login`), no 525.
4. Un **segundo** `azd provision` seguido deja los dos puntos anteriores igual. Este es el
   criterio que de verdad prueba el arreglo: el fallo original solo aparecía en el
   despliegue siguiente.
5. El certificado sigue siendo el mismo recurso
   (`atipico.com.bo-rg-atipi-260902203532`), no uno nuevo emitido en cada provision.

## 7. Riesgos y cosas a verificar en el despliegue real

- **Si `ConfigureCustomDomain` referencia el certificado existente o intenta emitir uno.** La
  documentación dice que el parámetro es "el nombre del certificado configurado en el
  portal", lo que implica que lo referencia. No está verificado contra este entorno. Si
  intentara crear uno, el provision fallaría por nombre duplicado — ruidoso, no silencioso.
- **El huevo y la gallina en un entorno nuevo.** En `prod` no va a existir el certificado
  antes del primer provision. El flujo documentado es desplegar primero con el nombre de
  certificado vacío (la vinculación queda sin TLS), crear el certificado administrado, y
  re-provisionar con el nombre. Hay que confirmarlo cuando se cree `prod` (§5.3 de
  [deploy-azure-aspire.md](deploy-azure-aspire.md)); no bloquea este arreglo.
- **Un solo dominio.** `ConfigureCustomDomain` admite uno; llamarlo dos veces pisa el
  anterior ([dotnet/aspire#7143](https://github.com/dotnet/aspire/issues/7143)). Hoy alcanza
  — no hay `www`. Si algún día se quiere `www.atipico.com.bo`, este no es el camino.
- **Cloudflare no se toca.** El modo SSL actual (Full o Full strict) es correcto y es lo que
  hace que el fallo se vea como 525 en vez de servir contenido inseguro. Bajarlo a "Flexible"
  haría desaparecer el 525 **sin arreglar nada** y dejando el tramo Cloudflare↔Azure en
  texto plano. No hacerlo.

## 8. Bitácora

- **2026-09-03** — Diagnóstico. Se descartó DNS como causa: el record `atipico.com.bo` y el
  TXT `asuid` están bien y el certificado nunca se perdió. Lo que se pierde es solo la
  vinculación en el Container App, y la borra `azd provision` porque la infra se genera
  desde `AppHost.cs` sin declararla. El 525 de Cloudflare es consecuencia, no causa.
- **2026-09-03** — Se verificó que el nombre de paquete que da la documentación de Aspire
  (`Aspire.Hosting.AzureContainerApps`) no existe en NuGet.
- **2026-09-03** — Implementados los pasos 1 a 3 de §5. `dotnet build -c Release` correcto,
  0 errores; las 2 advertencias son el `NU1903` preexistente de `Microsoft.OpenApi` (§5.4 de
  [deploy-azure-aspire.md](deploy-azure-aspire.md)). **`ASPIREACADOMAINS001` no apareció**,
  lo que confirma en este proyecto que la API graduó de experimental en 13.5 y que no hace
  falta la supresión en `.editorconfig`.
- **2026-09-03** — **Error propio, corregido.** El paso 4 de §5 decía
  `azd env set custom-domain …`, usando el nombre del parámetro. `azd env set` escribe un
  dotenv y el guión lo invalida: dejó `.azure/Atipico/.env` corrupto y **todo** comando de azd
  empezó a fallar con `unexpected character "-" in variable name`, no solo el provision. Se
  destrabó borrando esa línea del `.env`. Los nombres correctos son los de las variables
  (`AZURE_CUSTOM_DOMAIN`, `AZURE_CERTIFICATE_NAME`), los mismos que CI. Ya quedaron cargados
  en el entorno `Atipico` y verificados con `azd env get-values`.
- **2026-09-03** — Se confirmó que la convención `AZURE_` + UPPER_SNAKE es la correcta sin
  desplegar nada: el pipeline funciona hoy en un runner **sin `config.json`** (`.azure` está
  en `.gitignore`) y con solo esas variables, así que azd resuelve los parámetros desde ahí.
- **2026-09-03** — Corregido el spec: se había planificado repetir las dos variables en el
  `env:` de los pasos `provision` y `deploy` además del `env:` del job. Sobra. Los pasos
  repiten solo los `secrets`, porque esos no admiten declararse a nivel de job; las `vars` sí
  (`AZURE_R2_BUCKET` aparece una vez). El código quedó con la versión corta.
