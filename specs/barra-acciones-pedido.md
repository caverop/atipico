# Barra de acciones del pedido

**Estado:** implementado — falta la pasada por navegador (§8)
**Afecta:** `Atipico.Web` (`Pedidos/Edit.razor`, `app.css`), `CLAUDE.md`

---

## 1. Problema

En `Pedidos/Edit.razor` las acciones que mueven el pedido —*Continuar*, *En
Preparación*, *Servido*, *Anular*— viven arriba de todo, entre «Tipo de pedido» y
el `<hr>`. Debajo hay tres pantallas de formulario: platos, comprobantes QR,
mesas y entrega.

El recorrido real de un pedido `Abierto` es este:

```
[arriba]  Comensal · Tipo · Método de pago · [En Preparación]
[abajo]   Platos → agregar platos → adjuntar comprobantes QR
[abajo]   Mesas · Entrega
[arriba]  ...volver a subir para apretar En Preparación
```

El mesero adjunta el comprobante **abajo** y el botón que factura está tres
pantallas **arriba**. Es el mismo gesto en cada pedido del turno.

## 2. Decisión

Una **barra de acciones fija al pie del viewport**, siempre visible, con el bloque
de acciones que hoy está arriba. El bloque de arriba se va: no hay dos.

### 2.1 Por qué fija y no una copia al final

La primera idea fue duplicar el botón al final del formulario. Se descartó por dos
razones, y la segunda es la que decide:

1. **Una copia se desincroniza.** El bloque no es un botón: son cuatro acciones con
   condiciones distintas (§3). Copiar el marcado garantiza que las dos instancias
   se separen la primera vez que alguien toque una condición. Este repo ya tropezó
   con eso —`SeccionNav.RolesCsv` se deriva justamente porque dos listas paralelas
   se habían separado— y lo resuelve siempre igual: una definición, dos dibujos.
2. **Una copia al final solo sirve si estás al final.** Con el formulario a media
   altura no hay botón ni arriba ni abajo. La barra fija no tiene ese hueco.

El costo está en §6: hay que meterse con la capa de elementos fijos, que es
compartida.

### 2.2 Lo que se descartó

| Idea | Por qué no |
|---|---|
| Copia estática al final del formulario | §2.1 |
| Duplicar solo el botón, dejando el select de método de pago arriba | el botón queda deshabilitado sin ninguna pista de qué lo desbloquea, con el control fuera de pantalla (§4) |
| Mover el bloque abajo, sin barra | mismo hueco a media altura, y rompe el orden actual donde el método de pago se elige antes de cargar nada |

## 3. Qué entra en la barra

No es un botón: es un bloque que cambia con el estado.

| Estado | Contenido |
|---|---|
| Nuevo (`Id` null) | **Continuar** |
| `Abierto` | select *Método de pago* · **En Preparación** · Anular |
| `EnPreparacion` | **Servido** · Anular |
| `Servido` | Anular |
| `Cerrado` / `Anulado` | — nada: **la barra no se dibuja** |

Las condiciones son las que ya existen (`_pedidoActivo`, `_puedeMarcarServido`,
`_puedeAnular`), sin tocarlas.

## 4. El método de pago viaja con el botón

`En Preparación` está deshabilitado hasta elegir método de pago. Si el select se
queda arriba, la barra muestra un botón gris **sin ninguna pista** de qué lo
desbloquea y con el control fuera de pantalla — peor que no tener la barra.

Por eso el select entra en la barra. La acción queda autosuficiente: todo lo que
hace falta para ejecutarla está en el mismo lugar.

**Y eso rompió algo en la pantalla de al lado.** La zona de comprobantes QR aparece al
elegir Qr, y está arriba, en la columna de platos. Con el select arriba, aparecía debajo
del cursor; con el select en la barra, se elige abajo y la zona queda fuera de pantalla.
Se resolvió llevando el foco al campo de archivo al elegir Qr — ver
[`comprobantes-qr.md`](comprobantes-qr.md) §8.1.1. Vale como recordatorio de que mover un control cambia
todo lo que ese control disparaba.

## 5. Dónde va en el marcado

**Al final del `@if (_loaded)`, fuera del `<EditForm>`**, después de los
comprobantes.

Podría ir dentro del `<EditForm>` —`position: fixed` no depende de dónde se
declare— y así *Continuar* conservaría su `type="submit"`. Se prefiere afuera:
declararla al final hace que el orden del DOM coincida con el orden visual, que es
lo que leen el lector de pantalla y el tabulador. Una barra que se ve abajo pero se
lee entre «Tipo de pedido» y «Platos» es una trampa.

La consecuencia es que *Continuar* pasa a `type="button"` llamando a `HandleSave`.
Es equivalente: el formulario no tiene `DataAnnotationsValidator`, así que
`OnValidSubmit` nunca filtra nada. Y `OnValidSubmit` **se mantiene conectado**,
porque sin botón de submit el Enter en un campo de texto sigue enviando el
formulario y tiene que seguir haciendo lo mismo.

## 6. Capas — lo que hay que respetar

`app.css` y los componentes de layout ya tienen un orden de apilado, y esta barra
se mete en el medio:

| | z-index |
|---|---|
| Contenido de la página | — |
| **Barra de acciones** | **1020** |
| `BarraInferior` (pestañas, solo móvil) | 1030 |
| Hoja «Más» | 1035 / 1036 |
| `#blazor-error-ui` | 1040 |
| Tooltip de `Ayuda` | 1080 |
| `BarraProgreso` | 2000 |
| `Bloqueo` | 2100 |

**1020 y no más** por dos motivos concretos: `#blazor-error-ui` también está
anclado abajo y tiene que poder leerse por encima (fue lo que motivó subirlo a
1040 en su momento), y la hoja «Más» tiene que tapar la barra, no al revés.

Que `Bloqueo` la cubra durante una acción es lo correcto: es exactamente lo que ese
overlay existe para hacer.

### 6.1 En móvil se apoya sobre las pestañas

Debajo de 641px existe `BarraInferior`. La barra de acciones no la tapa: se sienta
encima.

```css
bottom: calc(var(--atipico-tabbar) + env(safe-area-inset-bottom, 0px));
```

El `env(safe-area-inset-bottom)` es el mismo que ya usa la hoja «Más»: sin él, en
un iPhone con notch la barra queda debajo del indicador de inicio.

Arriba de 641px no hay pestañas y va a `bottom: 0`.

### 6.1.1 El reparto en móvil: todo el ancho, o mitades

Debajo del punto de corte la barra reparte así:

| Contenido | Reparto |
|---|---|
| Select de método de pago | **100 %**, fila propia |
| Un solo botón (*Continuar*, *Anular*) | **100 %** |
| Dos botones | **mitad y mitad** |

Arriba del punto de corte **no cambia nada**: los controles conservan su ancho
natural y quedan alineados a la derecha.

**Mitades exactas, no proporcionales a la etiqueta.** La regla es `flex: 1 1 0` y no
`flex: 1 1 auto`. Con `auto` el reparto sigue el largo del texto: medido a 320 px,
*En Preparación* se llevaba el 59 % y *Anular* el 38 %. El botón que queda chico es
siempre el secundario, y en un teléfono eso es justo donde el pulgar tiene menos
precisión — además de que dos botones de distinto tamaño se leen como si uno fuera
menos pulsable que el otro.

`min-width: 0` acompaña a la regla y hace falta: por defecto un elemento flexible no
se achica por debajo de su ancho de contenido, y sin eso *En Preparación* no bajaría
de 142 px.

**El caso límite está medido.** A 320 px —el teléfono más angosto en uso— cada mitad
queda en 140 px y *En Preparación* entra **en una sola línea, sin truncar**. Importa
porque si el texto se partiera en dos líneas la barra crecería y el espaciador de
§6.2 quedaría corto, tapando el final del formulario.

### 6.2 El último contenido no puede quedar tapado

Una barra fija tapa el final de la página. `.content` ya reserva
`--atipico-tabbar`, pero eso cubre las pestañas, no esto.

Se reserva con un espaciador al final del formulario, **no** con `padding-bottom`
en `.content`: `.content` es de todas las pantallas y solo esta tiene barra. El
espaciador se dibuja bajo la misma condición que la barra, así que en un pedido
`Cerrado` no queda un hueco blanco sin motivo.

#### El espaciador tiene dos alturas porque la barra tiene dos

En móvil el select de método de pago toma el ancho completo y empuja los botones a
una segunda fila. Medido: con select la barra mide **122 px**, sin él **66 px**. Un
solo valor de espaciador no sirve para las dos:

| Estado | barra | espaciador | despeje al final del scroll |
|---|---|---|---|
| `Abierto` (con select) | 122 px | `8.5rem` = 136 px | 46 px |
| `EnPreparacion`, `Servido`, nuevo | 66 px | `5rem` = 80 px | 46 px |

El despeje queda idéntico en los dos, que es exactamente el punto. Con un único
espaciador de 8.5 rem, los tres estados sin select arrastraban **56 px de más** de
espacio muerto al final del formulario.

El espaciador sigue la misma condición que el select, y esa condición se declara
**una sola vez** (`BarraConMetodo`) para que no puedan discrepar. Si discreparan por
defecto de menos, el último bloque del formulario quedaría tapado — que es justo lo
que el espaciador existe para evitar.

Arriba del punto de corte no hay dos filas: la barra mide 60 px en todos los estados
y `5rem` alcanza siempre.

**El despeje no lo da solo el espaciador.** `.content` ya aporta
`calc(var(--atipico-tabbar) + 1.5rem)` = 82 px de `padding-bottom` en móvil, que
existía desde antes para que la barra de pestañas no tapara la última fila de las
listas. El espaciador se suma a eso; dimensionarlo ignorándolo lleva a reservar de
más.

#### Por qué no `position: sticky`

Sería lo elegante: en flujo normal reserva su propio alto, el espaciador desaparece
y el número mágico con él. **Se probó y no salió.** Con `sticky` + `bottom` el
último bloque se seguía solapando con la barra al final del scroll —34 px, medidos—
por cómo interactúa con el `padding-bottom` que `.content` ya tiene en móvil.

No se descarta para siempre, se descarta hoy: `fixed` + espaciador está medido y
funciona en los cuatro estados y en todos los anchos, y no vale cambiar eso por una
variante que todavía no anda.

## 7. Reglas

| # | Regla |
|---|---|
| BA-1 | Las acciones existen en **un solo lugar** del marcado. No hay copia arriba. |
| BA-2 | Sin acciones disponibles (`Cerrado`, `Anulado`) no se dibuja la barra ni el espaciador. |
| BA-3 | Todo lo necesario para ejecutar la acción está dentro de la barra (§4). |
| BA-4 | La barra nunca tapa a `BarraInferior`, a la hoja «Más» ni a `#blazor-error-ui`. |
| BA-5 | Los botones siguen deshabilitados durante la acción (`_procesando`), además del overlay. |

## 8. Criterios de aceptación

> BA-CA-3 a BA-CA-7 se verificaron en un **banco de pruebas**: una página con el
> `app.css` real y réplicas de la cromo fija usando los valores reales de sus
> componentes (`1030` de `BarraInferior`, `1036` de la hoja «Más», `1040` de
> `#blazor-error-ui`, `2100` de `Bloqueo`). El apilado se midió con
> `elementFromPoint` —quién gana en un punto dentro de la barra— y no a ojo sobre
> una captura. Medido a 1200×820 y a 375×812.

- [x] ✅ BA-CA-1 — La barra es `position: fixed`, así que el botón está a la vista en cualquier posición del scroll. **Falta verlo en el formulario real.**
- [ ] BA-CA-2 — El select está en la barra ✅, pero que *habilite* el botón es enlace de Blazor: **sin ejercitar**.
- [x] ✅ BA-CA-3 — En `Cerrado` no se dibujan ni barra ni espaciador, y la página se acorta exactamente el alto del espaciador (136 px en móvil): no queda hueco.
- [x] ✅ BA-CA-4 — En móvil la barra termina exactamente donde empiezan las pestañas (`bottom` 754 = `top` 754), y `elementFromPoint` sobre las pestañas sigue devolviéndolas: pulsables.
- [x] ✅ BA-CA-5 — Con la hoja «Más» abierta, el punto medio de la barra devuelve la hoja.
- [x] ✅ BA-CA-6 — Con `Bloqueo` visible, ese mismo punto devuelve el overlay.
- [x] ✅ BA-CA-7 — Al final del scroll el último bloque queda completo: escritorio, termina en 716 y la barra empieza en 760; móvil, 586 contra 632.
- [ ] BA-CA-8 — Un doble clic no ejecuta dos veces. Cubierto por código (`EjecutarAsync` corta con `_procesando`), **sin ejercitar**.
- [ ] BA-CA-9 — En un pedido nuevo, *Continuar* guarda igual que antes. **Sin ejercitar**: es el cambio de `submit` a `button` y hay que verlo guardar de verdad.
- [x] ✅ BA-CA-10 — En móvil el select ocupa el 100 % y un botón solo también; dos botones quedan al 49/49. Medido a 320 y 375 px, en los cuatro estados.
- [x] ✅ BA-CA-11 — A 320 px *En Preparación* entra en media pantalla en una línea: sin truncar y sin partirse en dos.
- [x] ✅ BA-CA-12 — Arriba del punto de corte el reparto no cambia: anchos naturales, alineados a la derecha.

Los tres que quedan necesitan la aplicación corriendo con una sesión iniciada, y
dos de ellos **escriben en la base**. Son tuyos.

## 9. Limitación conocida

En iOS, el teclado en pantalla desplaza los elementos `position: fixed`. Al
escribir el comensal la barra puede saltar. Se acepta: esconderla al enfocar un
campo requiere interop de JS por evento de foco, y el problema dura lo que dura el
tipeo.

## 10. Plan

| Fase | Qué | Estado |
|---|---|---|
| ~~1~~ | Bloque de acciones movido al final, `Continuar` a `type="button"`, `HayAcciones` | ✅ |
| ~~2~~ | `app.css`: `.pedido-barra-acciones`, interior, espaciador y el `@media` del punto de corte | ✅ |
| ~~3~~ | Listado de capas actualizado en `CLAUDE.md` | ✅ |
| ~~4a~~ | Banco de pruebas: apilado y maquetado (BA-CA-3 a BA-CA-7) | ✅ |
| ~~5~~ | Repaso de móvil: espaciador de dos alturas (§6.2) | ✅ |
| ~~6~~ | Reparto en móvil: mitades exactas en vez de proporcionales a la etiqueta (§6.1.1) | ✅ |
| **4b** | Formulario real: BA-CA-2, BA-CA-8, BA-CA-9 | **pendiente — requiere sesión y escribe en la base** |

#### No hizo falta el `RenderFragment`

El plan lo daba por hecho, porque venía del análisis de la copia: con dos
instancias, una definición compartida era la única forma de que no se separaran.
Al pasar a la barra fija hay **una sola instancia**, y un `RenderFragment` para
invocarlo una vez es ceremonia. El bloque quedó escrito una vez, donde se usa.

#### Dos detalles que aparecieron al escribirlo

**El alto del espaciador no es uno solo.** En el teléfono la barra son dos filas
—el select toma el ancho completo y los botones se reparten la de abajo—, así que
el espaciador crece en el mismo `@media`. Un solo valor dejaría contenido tapado
en móvil o un hueco de más en escritorio.

**El select perdió su `<label>` al mudarse.** Arriba tenía uno; en la barra no hay
lugar para una etiqueta y el `-- Método de pago --` del `<option>` vacío hace de
rótulo visual. Lleva `aria-label` para que el lector de pantalla no se quede sin
nada — el `<option>` no cumple ese papel.

## 11. Bitácora

**El requerimiento entró como «una copia del botón al final».** El análisis previo
mostró que el bloque cambia con el estado, que la copia se desincroniza y que una
copia al final no ayuda a media altura. Se eligió la barra fija sabiendo que cuesta
más: toca la capa de elementos fijos, que es compartida.

**Dos cosas que aparecieron mirando el formulario, fuera de alcance:**

- El botón que dice **«En Preparación» llama a `HandleCerrarPedido`**. El nombre
  miente y es una trampa para quien toque esto después.
- Desde **`Servido` la única acción es «Anular»**: no hay botón para cerrar el
  pedido en esta pantalla.

### El banco de pruebas, y por qué no fue la aplicación

Lo que había que verificar era CSS: apilado, punto de corte, alto reservado. Nada
de eso necesita Blazor, ni la API, ni una sesión — y levantar la aplicación sí
habría necesitado credenciales, y clickear las acciones habría facturado pedidos
de verdad en la base de desarrollo.

Un banco con el `app.css` real y réplicas de la cromo fija —con **los valores
reales** leídos de cada componente, ningún número inventado— responde las mismas
preguntas sin tocar nada. El apilado se midió con `elementFromPoint` en vez de
mirar capturas: sobre una captura escalada, "la hoja tapa la barra" es una
impresión; `elementFromPoint` dice qué elemento gana en ese píxel.

Una anotación de método: la primera captura en móvil contradecía la medición
—mostraba el último bloque tapado— y la medición tenía razón. La captura se había
tomado antes de que asentara un scroll hecho por script. Repetido con el scroll del
navegador, las dos coincidieron. Vale como recordatorio de que una captura
inmediatamente después de mover el scroll no es evidencia.

### Dos trampas del banco de pruebas

Ninguna era del código, las dos costaron mediciones equivocadas y conviene no
volver a caer:

**El JavaScript corre antes de que el scroll se aplique.** Un `scroll` seguido de
un `getBoundingClientRect()` en la misma tanda devuelve las posiciones de *antes*
de scrollear, y el resultado parece un solape que no existe. Hay que separar el
scroll de la medición en dos llamadas, y confirmar con `scrollY === scrollHeight -
innerHeight` que de verdad se llegó al fondo. `requestAnimationFrame` no sirve de
sincronización: con el panel oculto no dispara y la medición se cuelga.

**El banco pisaba una regla del original.** Escribir `.content { padding: 1rem }`
sobrescribe con la taquigrafía el `padding-bottom` que `app.css` le da en móvil, y
ese padding es parte del despeje. Con la regla pisada, el banco reportaba 20 px de
solape donde el original tiene 46 px de aire. Una réplica que redefine algo del
original ya no está midiendo el original.
