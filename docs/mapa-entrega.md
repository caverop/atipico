# Mapa de la ubicación de entrega

Especificación funcional y técnica para **ver** el punto de entrega dentro de la aplicación,
sin salir a otra app. Continúa [direccion-entrega.md](direccion-entrega.md), que dejó las
coordenadas guardadas y un enlace externo como única forma de mirarlas.

- **Estado:** **implementado**, compila y con pruebas en verde. Falta verlo con un pedido
  real (§8).
- **Alcance:** mostrar un mapa con el pin del pedido, en el detalle del pedido.
- **Fuera de alcance:** elegir o corregir el punto sobre el mapa, ver varios pedidos en un
  mismo mapa, y cualquier estilo propio del marcador (§6).
- **Sin migración:** no toca la base. Las coordenadas ya están.

---

## 1. Contexto: qué falta hoy

`direccion-entrega.md` dejó el punto guardado y un botón **Ver en el mapa** que abre Google
Maps en otra pestaña. Eso resuelve *navegar hasta ahí* —que es lo que necesita el
repartidor— pero no resuelve *confirmar de un vistazo que el punto tiene sentido*, que es lo
que necesita el mesero mientras registra el pedido.

Para eso hay que sacar al mesero de la pantalla, mirar, y volver. Un mapa embebido de 260 px
lo evita.

---

## 2. Decisión de fondo: un `<iframe>`, no una librería de mapas

Se evaluaron dos caminos:

| | `<iframe>` de OpenStreetMap | Leaflet |
|---|---|---|
| Dependencias | ninguna | librería vendorizada + módulo JS + interop |
| API key | no | no |
| Ciclo de vida | ninguno | `IAsyncDisposable` o el circuito se llena |
| Elegir el punto | **no** | sí |
| Costo | ~15 líneas | ~1 día |

**Se elige el `<iframe>`, y la razón es que nadie necesita elegir un punto.** Hoy el mesero
pega el enlace de WhatsApp y las coordenadas se llenan solas; cuando llegue el agente (§7 de
`direccion-entrega.md`), la Cloud API las entrega numéricas. Los dos caminos de captura ya
están resueltos sin mapa.

Un selector serviría para **corregir** un punto que llegó mal. Pasa, pero todavía no se sabe
con qué frecuencia — el mismo criterio con el que quedó fuera la resolución de sobrepagos en
los comprobantes: primero ver si ocurre.

### 2.1 Una aclaración sobre el CSP

Una versión anterior de `direccion-entrega.md` afirmaba que los tiles externos chocaban con
el CSP del proyecto. **Era falso**: no hay ninguna cabecera `Content-Security-Policy` ni en
Web ni en API. Esa afirmación ya se corrigió.

Lo que sí es real es que el mapa se le pide a un tercero en cada carga (§5). Y que la
convención del proyecto para librerías de front es **vendorizarlas**, no traerlas de un CDN
— Bootstrap ya vive en `wwwroot/lib/bootstrap`. Si algún día entra Leaflet, va ahí.

---

## 3. La URL del embed

```
https://www.openstreetmap.org/export/embed.html
    ?bbox=<minLng>,<minLat>,<maxLng>,<maxLat>
    &layer=mapnik
    &marker=<lat>,<lng>
```

Es una página propia de OpenStreetMap, no un servicio de tiles crudo. Trae su atribución
incluida, así que no hay nada que agregar por ese lado.

### 3.1 Los dos parámetros usan orden distinto

**`bbox` va longitud primero. `marker` va latitud primero.** En la misma URL.

Es la clase de error que no falla ruidosamente: muestra un mapa perfectamente dibujado de un
lugar equivocado. Por eso la URL se arma en **un solo lugar** y con prueba (§4).

### 3.2 El bbox es el zoom

No hay parámetro de zoom: el encuadre sale del tamaño de la caja.

- **Latitud:** 1° ≈ 111 km en todo el planeta.
- **Longitud:** 1° ≈ 111 km × cos(latitud).

En Santa Cruz (−17.8°), cos ≈ 0.95: un grado de longitud son unos 106 km contra 111 de
latitud. **Un 4% de diferencia**, así que una caja cuadrada en grados se ve prácticamente
cuadrada y **no hace falta corregir por coseno**. Cerca de los polos importaría; Bolivia no
lo está.

Con eso, un margen de `0.0015°` da unos **330 metros de lado**: se ve la cuadra y las de al
lado. `0.002` da 440 m y ya empieza a perderse el detalle de las casas.

### 3.3 La cultura del servidor rompe la URL

Interpolar un `decimal` en un string usa la cultura actual. En una cultura con coma decimal,
`-17.783241` sale `-17,783241` — y el `bbox` separa **sus propios valores con coma**. La URL
se convierte en basura sintáctica.

Es el mismo riesgo contra el que ya está blindado `EnlaceMapa`. La defensa es la misma:
`FormattableString.Invariant`, y una prueba que lo fije.

---

## 4. Dónde va, y dónde no

**Va** en la columna de Entrega de `Pedidos/Edit.razor`, sobre el campo de coordenadas.

**Siempre se dibuja.** Cuando el pedido no tiene punto propio, el mapa se centra en el local
(§5.4 de `direccion-entrega.md`): una caja gris no le dice nada a nadie, y ver el local ubica
al mesero para mover el pin desde ahí. Que el punto sea el de referencia y no una dirección
real lo dice el aviso, no la ausencia de mapa.

**No va en `Pedidos/Index.razor`.** Un iframe por fila serían veinte peticiones a OSM al
abrir la pantalla y veinte mapas compitiendo por el ancho de banda del celular del mesero.

**Se conservan los dos, el mapa y el enlace.** No compiten: el embebido sirve para confirmar
de un vistazo que el punto tiene sentido; el enlace abre la app de mapas del teléfono, que es
la que sabe navegar hasta ahí.

### 4.1 El marcado

```razor
<iframe class="w-100 border rounded" height="260"
        loading="lazy" referrerpolicy="no-referrer"
        title="Ubicación de entrega"
        src="@UbicacionCompartida.EmbedMapa(lat, lng)"></iframe>
```

- `referrerpolicy="no-referrer"` — sin esto OSM recibe la URL de la aplicación, que incluye
  `/pedidos/123`.
- `loading="lazy"` — no pide el mapa si quedó fuera de la pantalla.
- `title` — un iframe sin título es una caja muda para un lector de pantalla.
- Altura fija: los iframes no se autodimensionan.

### 4.2 Por qué esto no necesita nada más

Sin JS interop, sin `IAsyncDisposable`, sin listeners que se filtren en el circuito. Esa
ausencia **es** el beneficio de la opción, y es lo que la vuelve reversible: si mañana entra
Leaflet, esto se borra sin dejar rastro.

Un detalle de Blazor que conviene entender: el diff solo toca el atributo `src` si la cadena
cambió, así que mientras las coordenadas no cambien el iframe no se recarga ni parpadea. Eso
depende de que la URL salga de una función determinista — otra razón para que viva en un solo
lugar.

---

## 5. Privacidad y uso del servicio

**Cada vez que alguien abre un pedido, OpenStreetMap recibe la coordenada del domicilio del
cliente.** Es inherente a mostrar un mapa de un tercero. No lo vuelve inaceptable, pero suma
a la pregunta de retención que sigue abierta (§6 de `direccion-entrega.md`).

Del lado del uso: el volumen de un restaurante no roza los límites del servicio. Si alguna
vez lo hiciera, la salida es un proveedor de tiles propio, y ahí ya se está en Leaflet.

**Degrada bien.** Si OSM está caído, el iframe queda en blanco pero el enlace y las
coordenadas en texto siguen funcionando.

---

## 6. Lo que no se puede hacer con esto

- Elegir o arrastrar el punto.
- Ver más de un marcador — no hay forma de mostrar todos los deliveries del día en un mapa.
- Estilo propio del pin, popups, capas.

Cualquiera de esas tres es la señal para pasar a Leaflet. Ninguna es un requerimiento hoy.

---

## 7. Plan de implementación

| # | Capa | Archivo | Qué |
|---|---|---|---|
| 1 | Application | `Common/UbicacionCompartida.cs` | `EmbedMapa()` y la constante del margen |
| 2 | Pruebas | `Atipico.Application.Tests/UbicacionCompartidaTest.cs` | orden de los parámetros, cultura invariante, tamaño de la caja |
| 3 | Web | `Components/Pages/Pedidos/Edit.razor` | el `<iframe>` bajo las coordenadas |

La prueba del paso 2 es la que importa: el error de §3.1 —los dos órdenes distintos— produce
un mapa correcto de un lugar equivocado, que a ojo no se distingue de uno bien.

---

## 8. Puesta en producción

Nada. No hay migración, no hay variable de entorno, no hay dependencia nueva que instalar.
Se verifica abriendo un pedido delivery con coordenadas y confirmando que el pin cae donde
debe.
