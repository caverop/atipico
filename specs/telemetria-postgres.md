# Telemetría de PostgreSQL

Especificación para que las consultas a la base aparezcan como trazas en el dashboard de
Aspire, igual que hoy aparecen las peticiones HTTP.

- **Estado:** **implementado y verificado** contra la aplicación corriendo (§10). Compila, las
  119 pruebas en verde, y los spans de PostgreSQL aparecen anidados bajo cada petición HTTP.
- **Alcance:** instrumentar Npgsql en `Atipico.Api` para que cada comando SQL emita un span
  ligado a la petición que lo originó.
- **Fuera de alcance:** exportar telemetría fuera de desarrollo (§7), persistirla (§7),
  métricas de negocio propias, y cualquier `ActivitySource` escrito a mano.
- **Sin migración:** no toca la base ni el esquema. No hay columnas nuevas.
- **Sin backend nuevo:** se ve en el dashboard de Aspire que ya está funcionando.

---

## 1. Qué pasa hoy

OpenTelemetry ya está instalado y activo. `AddServiceDefaults()` llama a
`ConfigureOpenTelemetry()`, y tanto `Atipico.Api` como `Atipico.Web` lo llaman. Hay
instrumentación de:

- **ASP.NET Core** — peticiones entrantes
- **HttpClient** — llamadas salientes (incluida Web → Api, y la resolución de enlaces de
  Google Maps)
- **Runtime** — GC, hilos, memoria

**No hay ninguna instrumentación de base de datos.** Para una aplicación que es casi
enteramente EF Core sobre PostgreSQL, ese es el hueco grande: se ve que una petición tardó,
pero no en qué consulta se fue el tiempo.

Hoy el SQL sí aparece en los logs de `Microsoft.EntityFrameworkCore.Database.Command`, pero
suelto: una línea de log que no está ligada a la petición HTTP que la originó. Reconstruir
"esta pantalla tardó por esta consulta" hay que hacerlo a ojo, comparando marcas de tiempo.

## 2. Qué agrega

Un span por cada comando ejecutado contra PostgreSQL, anidado dentro del span de la petición
HTTP. En el dashboard, una petición a `/api/pedidos` pasa de ser una barra a ser una barra con
sus consultas adentro, cada una con su duración.

Eso responde tres preguntas que hoy no se pueden responder sin adivinar:

- **¿Qué consulta es la lenta?** Neon está en `us-east-2` y cada ida y vuelta cuesta. En los
  logs de esta semana se vieron comandos de 219 ms a 332 ms. Con spans se ve cuántos van por
  petición, que es lo que realmente importa.
- **¿Cuántas consultas hace esta pantalla?** El patrón de servicio genérico
  (`EntityService<T>`) hace fácil disparar más consultas de las esperadas sin notarlo.
- **¿La lentitud es de la base o de la red?** Hoy las dos se ven igual.

## 3. Decisión de fondo: va en `Atipico.Api`, no en `ServiceDefaults`

El lugar obvio parece `Atipico.Aspire.ServiceDefaults`, que es donde vive
`ConfigureOpenTelemetry()`. **Pero no va ahí**, y el motivo es concreto.

`Npgsql.OpenTelemetry` **arrastra `Npgsql` como dependencia transitiva** (verificado con
`dotnet list package --include-transitive`: `Npgsql.OpenTelemetry 10.0.3` → `Npgsql 10.0.3`).

`Atipico.Web` referencia `ServiceDefaults`. Hoy la Web **no tiene acceso a la base**: solo
habla con la Api por HTTP, y su `.csproj` ni siquiera referencia `Atipico.Infraestructure`.
Meter el paquete en `ServiceDefaults` le metería un driver de PostgreSQL a un proyecto que no
tiene por qué saber que existe PostgreSQL.

Esa frontera es deliberada y conviene sostenerla. Así que el paquete y la línea que lo
enciende van en `Atipico.Api`, que es la composición raíz del proceso que sí habla con la
base — y que ya tiene Npgsql por vía de `Atipico.Infraestructure`. No entra nada nuevo a
ningún proyecto.

El costo de la decisión es que la configuración de telemetría queda en dos lugares: lo común
en `ServiceDefaults`, lo específico de la Api en su `Program.cs`. Es el costo correcto: lo
que es común está en lo común, y lo que es de un solo proyecto está en ese proyecto.

## 4. El cambio

### 4.1 `Atipico.Api/Atipico.Api.csproj`

```xml
<PackageReference Include="Npgsql.OpenTelemetry" Version="10.0.3" />
```

La versión acompaña a la línea de Npgsql / EF Core 10 que ya usa el proyecto.

### 4.2 `Atipico.Api/Program.cs`

Inmediatamente después de `builder.AddServiceDefaults();`:

```csharp
// Instrumentacion de PostgreSQL. Va aca y no en ServiceDefaults a proposito: el paquete
// arrastra Npgsql, y ServiceDefaults lo comparte con Atipico.Web, que no toca la base y no
// tiene por que enterarse de que existe PostgreSQL (ver specs/telemetria-postgres.md §3).
//
// ConfigureOpenTelemetry* agrega sobre el proveedor que ya armo AddServiceDefaults(); no lo
// reemplaza. Si se usara AddOpenTelemetry().WithTracing(...) el resultado seria el mismo,
// pero leerlo daria a entender que aca arranca la configuracion, y no es asi.
builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddNpgsql());
builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddNpgsqlInstrumentation());
```

Y los `using` correspondientes. **Hacen falta tres**, y ninguno es deducible del error que da
su ausencia:

```csharp
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
```

Cada uno resuelve una cosa distinta. Comprobado quitándolos de a uno y compilando:

| `using` | Sin él falla |
|---|---|
| `Npgsql` | `AddNpgsql()` y `AddNpgsqlInstrumentation()` |
| `OpenTelemetry.Trace` | `ConfigureOpenTelemetryTracerProvider` |
| `OpenTelemetry.Metrics` | `ConfigureOpenTelemetryMeterProvider` |

Lo contraintuitivo es el primero: `AddNpgsql()` **no** está en `OpenTelemetry.Trace` como el
resto de las instrumentaciones, sino en el espacio de nombres del propio driver. El error que
da —`"TracerProviderBuilder" no contiene una definición para "AddNpgsql"`— no lo sugiere.

La segunda línea, la de métricas, agrega contadores del **pool de conexiones** (conexiones
abiertas, en uso, esperando). Es barata y responde una pregunta que con Neon aparece tarde y
mal: si el pool se está quedando corto. Se puede omitir si se quiere el cambio mínimo, pero
no hay motivo para hacerlo.

## 5. Qué datos viajan, y cuáles no

Esto importa porque `pedido` guarda **nombre del comensal, dirección de entrega y coordenadas
GPS de la casa del cliente**.

**Los valores de los parámetros no viajan.** Todo el acceso a datos es por EF Core, que emite
SQL parametrizado. El span lleva la plantilla, no los valores:

```sql
SELECT u.id, u.activo, ... FROM usuario AS u WHERE u.nombre_usuario = @request_NombreUsuario
```

Se ve en los logs actuales de la aplicación, donde EF ya imprime
`[Parameters=[@request_NombreUsuario='?']]` — con `?` porque el registro de datos sensibles
está apagado (`EnableSensitiveDataLogging` no está activado en `AppDbContext`).

**Regla que hay que sostener:** si algún día se activa `EnableSensitiveDataLogging` para
depurar, los valores pasan a estar en los logs **y** en las trazas. Está bien hacerlo en
local, pero no debe quedar encendido, y menos aún el día que la telemetría salga hacia un
backend externo.

Mientras la telemetría no salga de la máquina (§7), este punto es teórico. Queda escrito
porque deja de serlo en el momento que eso cambie.

### 5.1 Lo que sí viaja: metadatos de conexión

Verificado sobre un span real (`aspire otel spans atipico-api --format Json`), el atributo
`db.npgsql.data_source` lleva **la cadena de conexión sin la contraseña**:

```
Host=ep-mute-tree-...-pooler.c-4.us-east-2.aws.neon.tech;Port=5432;
Database=atipico-db;Username=app_restaurante;SSL Mode=Require;Channel Binding=Require
```

Npgsql redacta la contraseña —comprobado, no aparece— pero **el host de Neon, la base y el
usuario sí van en cada span**. Lo mismo con `server.address`.

No son datos de comensales, así que no cambia la conclusión del §5. Pero es infraestructura
identificable, y conviene saberlo antes de mandar telemetría a un tercero: el endpoint exacto
de la base queda registrado del otro lado.

## 6. Dónde se ve

En el dashboard de Aspire, que ya está corriendo: pestaña **Trazas**, entrar a cualquier
petición. Los spans de PostgreSQL aparecen anidados bajo el span HTTP.

No hace falta ningún backend nuevo, ni Grafana, ni Prometheus, ni contenedor extra. El
dashboard de Aspire es un receptor OTLP completo y ya está escuchando en `:21254`.

**Limitación conocida:** el dashboard guarda en memoria. Al frenar el AppHost se pierde todo.
Para esta especificación alcanza —la pregunta es "¿por qué esta pantalla tarda?", que se
responde en el momento— pero es el motivo por el que persistir es un tema aparte (§7).

## 7. Lo que esto explícitamente no arregla

**Fuera de desarrollo, la telemetría no se exporta a ningún lado.** `ServiceDefaults` solo
enciende el exportador si existe `OTEL_EXPORTER_OTLP_ENDPOINT`:

```csharp
var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
```

Aspire la define sola. **`docker-compose.yml` y `render.yaml` no la definen.** O sea que en
producción se instrumenta todo y se descarta.

Esta especificación **no cambia eso**, a propósito. Elegir un backend y persistir la
telemetría es una decisión con ataduras (proveedor, costo, y el asunto de datos personales
del §5) que merece su propia especificación. Este cambio da valor el mismo día sin tomar
ninguna de esas decisiones.

Consecuencia honesta: en producción este cambio agrega trabajo que se tira. Es medible pero
chico —Npgsql arma el `Activity` y lo descarta al no haber quien lo escuche— y es el mismo
costo que ya se paga por las otras tres instrumentaciones.

## 8. Pruebas

**No hay pruebas automatizadas para esto, y es correcto que no las haya.**

Lo que se agrega son dos líneas de configuración de un componente de terceros. Una prueba que
las cubriera estaría verificando que Npgsql y OpenTelemetry hacen lo que documentan, no que
Atipico hace lo que debe. `Atipico.Api.Tests` prueba comportamiento de controladores; esto no
es comportamiento.

Lo que sí corresponde es que **las 119 pruebas actuales sigan en verde**, que es la
verificación de que el paquete no rompe la resolución de dependencias ni el arranque.

## 9. Riesgos

**Ruido en las trazas.** Cada petición pasa a tener varios spans hijos. Es el objetivo, pero
las pantallas con muchas consultas se van a ver cargadas. Si molesta, se filtra en el
dashboard, no en el código.

**Conflicto de versiones de Npgsql.** ~~El paquete pide `Npgsql 10.0.3`. Si
`Npgsql.EntityFrameworkCore.PostgreSQL` está atado a una versión distinta, NuGet unifica hacia
arriba.~~ **No se materializó:** ambos resuelven a `10.0.3` exacto, así que no hay unificación
que hacer. Cero advertencias `NU1605`/`NU1608`/`MSB3277` en la compilación completa.

**Fuga de Npgsql hacia `Atipico.Web`** (el riesgo que motiva el §3). **Verificado que no
ocurre:** `dotnet list Atipico.Web package --include-transitive` no devuelve ninguna
referencia a Npgsql. La frontera se sostiene.

**Ninguno de los dos es bloqueante.** Este cambio es reversible borrando dos líneas y un
`PackageReference`.

## 10. Verificación — hecha

No hace falta el dashboard: el CLI lee la misma API de telemetría desde consola, y eso sirve
igual y se puede repetir.

```bash
aspire otel spans atipico-api
```

Resultado real de la verificación:

```
22:29:23.621 OK 0.45s atipico-api: POST api/auth/login   291f77f
22:29:23.757 OK 0.29s atipico-api: postgresql            7f0319e
22:29:57.220 OK 0.28s atipico-api: POST api/auth/login   d346562
22:29:57.226 OK 0.27s atipico-api: postgresql            0076b0a
```

- ✅ **Cada petición HTTP tiene su span de PostgreSQL**, disparado 6 ms después.
- ✅ **Anidado, no suelto**: en el JSON, el `parentSpanId` del span `postgresql` es el
  `spanId` del span HTTP.
- ✅ **El SQL va parametrizado**: `WHERE u.nombre_usuario = @request_NombreUsuario`, sin
  valores literales (§5).
- ✅ **Métricas**: `Npgsql` aparece como medidor en el dashboard, junto a los de ASP.NET Core.
- ✅ También hay un span **`CONNECT atipico-db`** para la apertura de conexión, que no estaba
  previsto en esta especificación y resultó ser lo más informativo (§10.1).

Para ver los atributos completos de un span, incluido el SQL:

```bash
aspire otel spans atipico-api --search postgresql --format Json --limit 1
```

### 10.1 Lo primero que mostró

En la primera petición tras arrancar:

```
22:28:52.191 OK 4.99s atipico-api: GET api/usuarios
22:28:54.609 OK 1.94s atipico-api: CONNECT atipico-db
```

**Casi 2 de los 5 segundos se fueron en abrir la conexión a Neon**, no en consultar. Eso es
latencia de arranque en frío contra `us-east-2` más el handshake TLS, y es exactamente la
clase de cosa que los logs de EF no muestran: ellos empiezan a contar cuando el comando ya
tiene conexión.

No es un problema a resolver ahora —es el costo conocido de una base administrada lejos— pero
que se vea desde el primer día es justamente lo que esta especificación buscaba.

### 10.2 Si no aparece nada

Revisar, en este orden: que estén los **tres** `using` del §4.2, y que las líneas estén
**después** de `AddServiceDefaults()` — antes no hay proveedor sobre el cual configurar.
