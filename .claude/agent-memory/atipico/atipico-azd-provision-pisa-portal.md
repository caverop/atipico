---
name: atipico-azd-provision-pisa-portal
description: Todo cambio hecho a mano por el portal de Azure sobre atipico-web/atipico-api lo borra el siguiente azd provision; y la convención de nombres para pasarle parámetros de Aspire desde CI.
metadata:
  type: project
---

**Lo que se toca por el portal de Azure no sobrevive.** La infraestructura de `atipico-web` y
`atipico-api` la genera `azd` desde `Atipico.Aspire.AppHost/AppHost.cs` — no hay bicep en el
repo (`git ls-files .azure` está vacío, `.azure` está en `.gitignore`). Cada `azd provision`
aplica ese template y **ARM reemplaza, no fusiona**. El workflow `azure-dev.yml` corre
`provision` en cada push a `main`.

**Why:** así se perdía `atipico.com.bo` en cada deploy. El certificado administrado sobrevivía
(cuelga del Container Apps *Environment*) pero la vinculación no (cuelga del *Container App*,
en `ingress.customDomains`), y el síntoma era un **525 de Cloudflare** — TLS con SNI contra un
origen sin certificado para ese nombre. Diagnóstico completo en
`specs/dominio-personalizado-azure.md`.

**How to apply:** antes de resolver algo "rápido" por el portal sobre estos recursos,
declararlo en `AppHost.cs`. Aplica a lo que queda pendiente en `specs/deploy-azure-aspire.md`
§5.2 (storage para Data Protection) y a reglas de escalado. Precisión útil al depurar:
**`azd deploy` no pisa nada** — los dominios son del Container App, no de la revisión. El
culpable es siempre `provision`, y por eso el patrón se lee como aleatorio.

**Parámetros de Aspire desde CI:** `.azure` no está versionado, así que el runner no tiene el
`config.json` con los valores; `azd` los toma de variables de entorno, con la convención
kebab-case → UPPER_SNAKE con prefijo `AZURE_` (`r2-bucket` → `AZURE_R2_BUCKET`). **Nunca se usa el nombre del parámetro con `azd env set`:** ese comando escribe
`.azure/<entorno>/.env`, que es un dotenv y no admite guiones. `azd env set custom-domain ...`
corrompe el archivo y a partir de ahí *todo* comando de azd falla con
`unexpected character "-" in variable name`; se arregla borrando esa línea a mano. Los
parámetros con guión bajo viven en `config.json` bajo `infra.parameters` y los escribe azd
solo al preguntar. En
`azure-dev.yml` los `secrets` se repiten en el `env:` de cada paso porque no admiten
declararse a nivel de job; las `vars` van una sola vez en el `env:` del job.

**Trampa de documentación:** la doc de Aspire nombra el paquete de custom domains como
`Aspire.Hosting.AzureContainerApps` y **ese nombre da 404 en NuGet**. El real es
`Aspire.Hosting.Azure.AppContainers`. `ConfigureCustomDomain` dejó de ser experimental en
Aspire 13.5, así que en este repo (13.5.2) no hace falta suprimir `ASPIREACADOMAINS001` —
verificado compilando.

Relacionado: [[atipico-neon-deployment]], [[atipico-aspire-run-debug-rebuild]]
