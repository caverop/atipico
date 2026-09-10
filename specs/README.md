# Índice de specs

25 specs, más este índice. Cada feature de Atipico tiene el suyo, y el spec es el
registro permanente de la decisión — no una propuesta que se tira al implementar.

El formato obligatorio está en **[formato-spec.md](formato-spec.md)**: leerlo antes de
escribir uno nuevo.

**Estados** — el vocabulario y sus transiciones están en
[formato-spec.md §4.1](formato-spec.md):

| Estado | Significa |
|---|---|
| `propuesto` | escrito, esperando aprobación. Nada tocado todavía |
| `aprobado` | aprobado, implementación en curso o parcial |
| `implementado` | código escrito y compilando; **puede faltar correr scripts o probar en pantalla** |
| `en-produccion` | corrido contra la base real y verificado por el usuario |
| `descartado` | abandonado; queda como registro del porqué |

Hoy: **4 propuestos · 1 aprobado · 12 implementados · 8 en producción**.

---

## Proceso

| Spec | Estado | De qué se trata |
|---|---|---|
| [formato-spec](formato-spec.md) | `implementado` | El formato de estos documentos: secciones obligatorias, silogismo, diagramas, artefactos |
| [agente-db](agente-db.md) | `aprobado` | Subagente `db` dueño de la base, y el proyecto `Atipico.Database.Tests` que le sirve de herramienta (falta escribirlo) |

## Pedidos y sala

| Spec | Estado | De qué se trata |
|---|---|---|
| [numero-pedido](numero-pedido.md) | `implementado` | Número de pedido por turno de caja, con tabla `turno_caja`. **Faltan correr los scripts** |
| [orden-por-defecto-numero-pedido](orden-por-defecto-numero-pedido.md) | `en-produccion` | La grilla ordena por número de pedido ascendente por defecto |
| [numero-mesa-grilla-pedidos](numero-mesa-grilla-pedidos.md) | `en-produccion` | Columna de número de mesa en la grilla de pedidos |
| [mesa-compartida-por-turno](mesa-compartida-por-turno.md) | `en-produccion` | Una misma mesa puede estar en más de un pedido del mismo turno |
| [tipo-pedido](tipo-pedido.md) | `en-produccion` | Tipo de pedido: en salón, para llevar, delivery |
| [barra-acciones-pedido](barra-acciones-pedido.md) | `implementado` | Barra de acciones fija en `Pedidos/Edit.razor`. Falta la pasada por navegador |
| [reservas](reservas.md) | `propuesto` | Formulario público de reserva de mesa, más pantalla interna para confirmarlas. Migración `016`, detrás del `015` de [reparacion-ck](reparacion-ck-cuenta-metodo.md) ([SCRUM-21](https://caverop.atlassian.net/browse/SCRUM-21)) |

## Delivery

| Spec | Estado | De qué se trata |
|---|---|---|
| [rol-delivery](rol-delivery.md) | `en-produccion` | Rol Delivery: solo ve los pedidos ya preparados |
| [direccion-entrega](direccion-entrega.md) | `en-produccion` | Dirección de entrega en pedidos por delivery |
| [mapa-entrega](mapa-entrega.md) | `implementado` | Mapa de la ubicación de entrega. Falta verlo con un pedido real |
| [enlace-corto-ubicacion](enlace-corto-ubicacion.md) | `implementado` | Resolver enlaces cortos de Google Maps. Falta probarlo con un enlace real |

## Cobro

| Spec | Estado | De qué se trata |
|---|---|---|
| [comprobantes-qr](comprobantes-qr.md) | `implementado` | Comprobantes de pago QR, con subida a R2. Verificado en desarrollo |
| [reparacion-ck-cuenta-metodo](reparacion-ck-cuenta-metodo.md) | `en-produccion` | El `CHECK` canónico ya acepta `QR`. `sql/015_cuenta_metodo_qr.sql` corrido y confirmado contra Neon ([SCRUM-28](https://caverop.atlassian.net/browse/SCRUM-28)) |

## Base de datos

| Spec | Estado | De qué se trata |
|---|---|---|
| [neon-branches-ambientes](neon-branches-ambientes.md) | `implementado` | Branches de Neon: `production`, `qa`, `dev`. §2/§4/§5/§6 superseded por [postgres-local-dev](postgres-local-dev.md) |
| [postgres-local-dev](postgres-local-dev.md) | `implementado` | `dev` salió de Neon: Docker local levantado y poblado en `5433`, contenedor descartable para el agente sin cambios ([SCRUM-29](https://caverop.atlassian.net/browse/SCRUM-29)) |
| [telemetria-postgres](telemetria-postgres.md) | `implementado` | Telemetría de PostgreSQL, verificada contra la app corriendo |
| [script-inicial-completo](script-inicial-completo.md) | `propuesto` | Script de arranque: esquema + catálogo + usuarios de operación |
| [limpieza-datos-prueba](limpieza-datos-prueba.md) | `propuesto` | Limpiar datos de prueba en la base compartida. Auditoría hecha, ejecución pendiente |

## Despliegue e infraestructura

| Spec | Estado | De qué se trata |
|---|---|---|
| [deploy-azure-aspire](deploy-azure-aspire.md) | `en-produccion` | Deploy a Azure con Aspire y `azd`, verificado contra el entorno real |
| [dominio-personalizado-azure](dominio-personalizado-azure.md) | `implementado` | Dominio `atipico.com.bo` en Container Apps. **Falta el `azd provision`** |
| [cicd-github-azure-render](cicd-github-azure-render.md) | `propuesto` | CI/CD: GitHub Actions, Render para QA, Azure para producción |

## Interfaz y pruebas

| Spec | Estado | De qué se trata |
|---|---|---|
| [pruebas-blazor-marca-login](pruebas-blazor-marca-login.md) | `implementado` | Pruebas de componentes Blazor, y la marca del login |
| [favicon-atipico](favicon-atipico.md) | `implementado` | Reemplazar el favicon de la plantilla de Blazor |

---

## Cómo mantener este índice

Se actualiza **en el mismo turno** en que un spec cambia de estado o nace uno nuevo. El
estado que figura acá es el que declara el propio spec: no se infiere ni se adelanta. Si
un spec no lo declara, va `sin declarar` y se pregunta — no se inventa.

Los enlaces son markdown relativo, nunca `[[wikilinks]]`: así funcionan en Obsidian, en
GitHub y leídos como texto plano.
