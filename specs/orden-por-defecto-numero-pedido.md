# Orden por defecto de la grilla: número de pedido ascendente

Especificación funcional y técnica para SCRUM-18 (Jira, proyecto `atipico`): "Ordenar por
defecto la grilla por numero de numero de pedido de forma ascendente". Documento de referencia
previo a la implementación: recoge las decisiones tomadas y su porqué, para no volver a
discutirlas al escribir el código.

- **Estado:** **en producción**. Confirmado por el usuario en pantalla; pruebas en verde (§6).
- **Origen:** [SCRUM-18](https://caverop.atlassian.net/browse/SCRUM-18), tipo Task, sin
  descripción en el ticket. El título original decía "por numero de mesa"; se corrigió durante
  el análisis (ver §5, Bitácora) a "número de pedido" — el N° que ya identifica cada fila
  (`p.NumeroTurno`), no la columna Mesa de SCRUM-16.
- **Alcance:** que `/pedidos` (`Pedidos/Index.razor`) abra ordenada por N° ascendente (el
  pedido 1 primero), en vez de por fecha de creación descendente como hoy.
- **Fuera de alcance:** no se toca el mecanismo de ordenamiento en sí (`EntityTable`, el switch
  de `_pedidosOrdenados`, la cadena de desempate) — todo eso ya existe y ya soporta N°. Tampoco
  se toca la columna "Mesa" ni su propio caso de orden (`specs/numero-mesa-grilla-pedidos.md`).

---

## 1. Contexto: qué es hoy el orden por defecto, y por qué

`Pedidos/Index.razor` ya tiene una columna "N°" (`p.NumeroTurno`, el correlativo del turno,
`specs/numero-pedido.md`) y ya sabe ordenar por ella — el `case "N°"` del switch de
`_pedidosOrdenados` existe desde antes de SCRUM-16. Lo único que falta es que sea **el orden
con el que la grilla abre**, en vez de tener que elegirlo a mano cada vez.

Hoy el estado inicial es:

```csharp
// Orden por defecto: primero Creado, luego Estado, por ultimo Comensal.
private string _ordenColumna = "Creado";
private bool _ordenDesc = true;
```

Es decir, la grilla abre mostrando primero los pedidos más recientes del turno. SCRUM-18 pide
que abra mostrando primero el pedido 1, luego el 2, etc.

**Por qué no hace falta cambiar nada del mecanismo de orden.** `NumeroTurno` es único dentro
del turno que la grilla muestra (`uk_pedido_numero_turno` sobre `(id_turno_caja,
numero_turno)`, y la grilla siempre lista los pedidos de UN turno) — no hay dos filas con el
mismo N°. La cadena de desempate que sigue al criterio elegido (`ThenByDescending(DiaCreado)`,
`ThenBy(Estado)`, `ThenBy(Comensal)`, `ThenBy(Id)`) se sigue ejecutando cuando N° es la columna
activa —igual que ya pasa hoy si alguien elige "N°" a mano en el encabezado— pero no cambia
nada: el orden ya queda completamente determinado por N° antes de llegar a esos criterios. No
hace falta excluir a N° de esa cadena para que el resultado sea correcto.

## 2. Decisión: cambiar el estado inicial, y la dirección por defecto al volver a N°

### 2.1 El cambio pedido

```csharp
private string _ordenColumna = "N°";
private bool _ordenDesc = false;
```

Con esto la grilla abre ordenada por N° ascendente. Nada más del switch, de `Aplicar`, ni de
la cadena de desempate necesita tocarse (§1).

### 2.2 Un ajuste relacionado, no pedido literalmente por el ticket, pero necesario para que no quede una inconsistencia

`Ordenar(string columna)` decide la dirección con la que arranca una columna **recién
elegida** (no la que ya estaba activa):

```csharp
// Misma columna: invierte. Otra columna: arranca ascendente, salvo Creado y N°, donde lo
// que se quiere ver primero es siempre lo mas reciente.
private void Ordenar(string columna)
{
    if (_ordenColumna == columna) { _ordenDesc = !_ordenDesc; return; }

    _ordenColumna = columna;
    _ordenDesc = columna is "Creado" or "N°";
}
```

Ese comentario y esa condición son de **antes** de este ticket, de cuando el orden por defecto
era Creado descendente: tenía sentido que volver a N° a mano también arrancara descendente
("lo más reciente primero"), para que se sintiera parecido a Creado.

Con N° ascendente como el orden con el que la grilla **siempre abre**, dejar esa excepción
crea una inconsistencia concreta: alguien que ordena por Estado y después vuelve a hacer clic
en "N°" para "volver a como estaba" cae en **descendente** — lo contrario de lo que acaba de
ver al entrar a la pantalla. Se saca "N°" de esa lista:

```csharp
_ordenDesc = columna is "Creado";
```

Volver a N° desde cualquier otra columna arranca ahora ascendente, igual que el estado con el
que abre la pantalla. "Creado" se queda en la excepción sin cambios — ese comportamiento no lo
toca este ticket y sigue siendo el correcto para esa columna (ver más abajo).

### 2.3 Por qué esto no reabre nada de "Creado"

El único otro miembro de esa excepción, "Creado", no se toca: nadie pidió cambiar su
comportamiento, y el razonamiento que lo puso ahí (ver lo más reciente primero al elegirlo a
mano) sigue siendo válido para esa columna en particular — solo dejó de aplicar a N° porque N°
cambió de rol, de "columna ordenable más" a "el orden con el que abre la pantalla".

## 3. Interfaz: diff completo

`Pedidos/Index.razor`, sección `@code`:

```diff
- // Orden por defecto: primero Creado, luego Estado, por ultimo Comensal.
- private string _ordenColumna = "Creado";
- private bool _ordenDesc = true;
+ // Orden por defecto: primero N° (SCRUM-18), luego Creado, Estado y por ultimo Comensal.
+ private string _ordenColumna = "N°";
+ private bool _ordenDesc = false;
```

```diff
- // Misma columna: invierte. Otra columna: arranca ascendente, salvo Creado y N°, donde lo
- // que se quiere ver primero es siempre lo mas reciente.
+ // Misma columna: invierte. Otra columna: arranca ascendente, salvo Creado, donde lo que se
+ // quiere ver primero es siempre lo mas reciente. N° ya no es una excepcion: el orden con el
+ // que la grilla abre (§2.2, SCRUM-18) es justamente N° ascendente, asi que volver a el a
+ // mano arranca igual, no al reves.
  private void Ordenar(string columna)
  {
      if (_ordenColumna == columna) { _ordenDesc = !_ordenDesc; return; }

      _ordenColumna = columna;
-     _ordenDesc = columna is "Creado" or "N°";
+     _ordenDesc = columna is "Creado";
  }
```

Ningún otro archivo cambia: no hay backend, no hay SQL, no hay Domain/Infraestructura
involucrados — es un cambio de dos valores iniciales y una condición, todo dentro de
`Pedidos/Index.razor`.

## 4. Qué NO cambia

- El resto de los filtros y el resto de las columnas: sin cambios.
- La columna "Mesa" (SCRUM-16) y su propio caso de orden: sin cambios — sigue excluida de la
  cadena de desempate por su propio motivo (`specs/numero-mesa-grilla-pedidos.md` §4.2), que no
  tiene relación con este ticket.
- El `<select>` "Ordenar por" de la vista móvil (`EntityTable.razor`): usa el mismo contrato
  `OnSort`/`SortHeader` genérico, así que hereda el nuevo valor por defecto sin tocarse.
- La API: la grilla siempre trajo todos los pedidos del turno sin ordenar (`ArmarGrillaAsync`
  no ordena nada); el ordenamiento es enteramente responsabilidad del cliente, como ya explica
  el comentario de `EntityTable.razor` sobre por qué el componente no ordena por sí mismo.

## 5. Plan de implementación

| # | Capa | Archivo | Cambio |
|---|---|---|---|
| 1 | Web | `Components/Pages/Pedidos/Index.razor` | `_ordenColumna`/`_ordenDesc` iniciales (§2.1); condición de `Ordenar` (§2.2) |
| 2 | Pruebas | ver §6 | — |

## 6. Plan de pruebas

No hay una prueba hoy que fije el orden por defecto de la grilla (`Atipico.Web.Tests` no tiene
tests de `Pedidos/Index.razor` anteriores a `PedidosIndexMesaTests.cs` de SCRUM-16, y ese
archivo no ejercita el estado inicial de orden — construye sus casos alrededor de la columna
Mesa). Casos a agregar:

1. **Orden por defecto, sin hacer clic en nada.** Renderizar la grilla con pedidos cuyo
   `NumeroTurno` no coincida con el orden de `CreadoEn` (para que la aserción no pueda pasar
   "por accidente" con el criterio viejo) y verificar que las filas salen en orden de N°
   ascendente.
2. **Volver a "N°" desde otra columna arranca ascendente.** Clic en "Estado" (o cualquier otra
   columna) y después en "N°": el resultado tiene que ser ascendente, no descendente — este es
   el caso que motiva el cambio de §2.2, y sin un test se puede volver a romper sin que nada lo
   note.
3. **"N°" ya activo se sigue invirtiendo con un segundo clic**, igual que cualquier columna:
   activo ascendente por defecto, un clic pasa a descendente.
4. Revisar que ningún test existente asuma el orden viejo. Búsqueda hecha al escribir este
   spec: no se encontró ninguna aserción en `Atipico.Web.Tests` sobre el orden por defecto de
   `Pedidos/Index.razor` ni sobre la dirección con la que "N°" arranca al elegirlo — el cambio
   de §2.2 no debería romper nada existente, pero conviene que quien escriba los tests lo
   confirme corriendo la suite completa antes de dar por cerrado el punto.

## 7. Bitácora

- **2026-09-05.** Al analizar el ticket con el título original ("por numero de mesa"), se
  detectó una consecuencia no obvia: SCRUM-16 ya había decidido que un pedido sin mesa asociada
  ordena como "mesa 0" (antes que cualquier mesa real en ascendente), pero esa decisión solo se
  notaba si alguien elegía ordenar por Mesa a mano. Convertirla en el orden **por defecto**
  habría vuelto visible ese detalle en cada apertura de la pantalla, así que se consultó al
  usuario antes de seguir. La respuesta fue que el ticket estaba mal titulado: no es por mesa,
  es por número de pedido (N°) — el título en Jira ya se corrigió
  ("Ordenar por defecto la grilla por numero de numero de pedido de forma ascendente"). Este
  documento se escribió directamente contra el pedido corregido; no hay contenido de la lectura
  errónea inicial que limpiar, porque no se había llegado a escribir código ni spec todavía.

- **2026-09-05.** QA escribió los tests en rojo (§6); dev aplicó el diff de §3 hasta ponerlos
  en verde — 183 tests, 0 fallos, verificados de forma independiente. El usuario probó la
  grilla en el navegador y confirmó que queda todo en orden.
