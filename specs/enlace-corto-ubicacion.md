# Resolver enlaces cortos de Google Maps

Especificación funcional y técnica para que el campo **Ubicación** acepte un enlace
`maps.app.goo.gl` y saque de él las coordenadas. Continúa
[direccion-entrega.md](direccion-entrega.md), que dejó este caso explícitamente fuera de
alcance.

- **Estado:** **implementado**, compila y con pruebas en verde. Falta probarlo con un enlace
  real desde la aplicación corriendo (§10).
- **Alcance:** que pegar un enlace corto llene latitud y longitud, igual que hoy lo hace un
  enlace largo.
- **Fuera de alcance:** cualquier otro acortador, y resolver enlaces de forma diferida o en
  lote.
- **Sin migración:** no toca la base. No hay columnas nuevas (§3).

---

## 1. Qué pasa hoy

Un `maps.app.goo.gl` **no contiene coordenadas**: es un identificador opaco. El parser no
encuentra nada, avisa que no pudo leerlo, y guarda el texto tal cual para que el repartidor lo
abra a mano. Funciona, pero deja el pedido sin punto y sin mapa.

El rodeo que existe hoy es abrir el enlace en el navegador y pegar la URL larga de la barra de
direcciones. Esta especificación elimina ese paso.

## 2. Qué hay del otro lado

Comprobado con un enlace real. `https://maps.app.goo.gl/gYQEJD9DL87Sk9WZA` responde **302**
hacia:

```
https://www.google.com/maps/place/17%C2%B045'49.3%22S+63%C2%B011'59.7%22W
    /@-17.7637728,-63.2002991,18.94z
    /data=!4m4!3m3!8m2!3d-17.763701!4d-63.19992?entry=tts&g_ep=...&skid=...
```

Dos cosas que importan:

**La URL destino ya trae el punto**, así que alcanza con leer la cabecera `Location`. No hace
falta descargar ni interpretar la página.

**Trae dos pares de coordenadas distintos.** El del `@` es el encuadre de la cámara; el de
`!3d`/`!4d` es el marcador. En este ejemplo están a unos 40 metros. Eso ya se corrigió en el
parser —el pin gana sobre el encuadre— y aplica igual acá.

## 3. Decisión de fondo: se resuelve al pegar, y lo pegado no se toca

Al salir del campo, si el texto no tiene coordenadas y **sí** tiene una URL de un dominio que
resolvemos, se sigue el redirect del lado del servidor y se lee el punto de la URL destino.

**`ubicacion_compartida` sigue guardando lo que el cajero pegó, sin modificar.** Es la decisión
de §2 de `direccion-entrega.md`: se guarda el crudo y se deriva el dato. El enlace corto es lo
que mandó el comensal, y eso es la evidencia; la URL larga es un paso intermedio que no se
persiste.

De ahí que **no haga falta ninguna columna nueva ni migración**. Las coordenadas ya tienen
dónde ir.

## 4. Qué enlaces se resuelven

Solo estos anfitriones:

```
maps.app.goo.gl
goo.gl
maps.google.com
www.google.com
google.com
```

Es un requisito funcional antes que nada: seguir una URL que no sea de Google Maps no puede
producir coordenadas, así que intentarlo es trabajo perdido y una espera para el cajero.

También cierra una puerta que conviene tener cerrada. **La URL no la escribe el cajero: la
escribe el comensal**, y el cajero la reenvía desde su WhatsApp. O sea que el destino de la
petición que hace el servidor lo elige alguien de afuera. Con la lista, ese alguien solo puede
elegir entre cinco dominios de Google.

Por eso **se valida el anfitrión de cada salto**, no solo el del primero: un redirect puede
apuntar a cualquier parte, y validar únicamente la URL pegada dejaría la puerta abierta en el
segundo paso.

## 5. Cómo se resuelve

```
1. El parser corre sobre el texto pegado.
   ¿Encontró coordenadas?  → listo, sin ninguna petición HTTP.
2. ¿Hay una URL con anfitrión de la lista?
   No → comportamiento de hoy: aviso y texto guardado igual.
3. GET sin seguir redirects automáticamente.
   3.1 ¿Respuesta 3xx con Location?
       - anfitrión fuera de la lista → se abandona
       - el parser encuentra el punto en esa URL → listo
       - si no, se repite desde 3.1 con la nueva URL
   3.2 Máximo 3 saltos.
4. Sin punto después de todo eso → comportamiento de hoy.
```

**`AllowAutoRedirect = false`** es lo que permite inspeccionar cada salto en vez de que
`HttpClient` los siga solo. Se usa `GET` con `ResponseHeadersRead`: solo interesan las
cabeceras, no hay motivo para descargar el cuerpo.

**Timeout de 5 segundos** para toda la operación. No es una precaución teórica: el cajero está
esperando con el comensal enfrente, y un servicio lento que cuelgue el campo es peor que no
resolver el enlace.

Se manda un **User-Agent** identificable. Un cliente sin User-Agent es lo primero que rechaza
cualquier servicio.

## 6. Interfaz

Sin campos nuevos. Cambia solo lo que pasa al salir del campo **Ubicación**:

- La resolución se envuelve en `EstadoOperaciones.SeguirAsync`, así la barra de progreso
  global la acompaña como a cualquier otra llamada.
- Si resuelve, las coordenadas aparecen en el campo único y el mapa se recentra. Igual que
  hoy al pegar un enlace largo.
- Si no resuelve, el aviso actual — con una diferencia: hoy dice que el repartidor puede abrir
  el enlace igual, y eso se mantiene, porque sigue siendo cierto.

## 7. Cuándo no resuelve, y qué pasa entonces

| Situación | Qué hace |
|---|---|
| Sin conexión, o timeout | aviso, texto guardado |
| Anfitrión fuera de la lista | ni lo intenta; aviso, texto guardado |
| Google devuelve una página de consentimiento | aviso, texto guardado |
| Más de 3 saltos | se abandona; aviso, texto guardado |

**En los cuatro casos el texto pegado sobrevive intacto.** Esta funcionalidad solo puede
mejorar el resultado de hoy; no puede empeorarlo. Es lo que la hace segura de agregar: si
Google cambia el formato mañana, se vuelve al comportamiento actual sin que nadie pierda
datos.

## 8. Dónde vive cada pieza

| Pieza | Dónde | Por qué |
|---|---|---|
| Decidir si una URL es resoluble | `Atipico.Application/Common/UbicacionCompartida.cs` | lógica pura, y `Atipico.Application.Tests` existe |
| Extraer el punto | ídem, ya está | sin cambios |
| Hacer la petición | `Atipico.Web/Services/ResolvedorEnlaceUbicacion.cs` | es un adaptador de I/O, como los clientes de API que ya viven ahí |

La división no es estética. `Atipico.Web` **no tiene proyecto de pruebas**, así que todo lo
que se pueda decidir sin tocar la red se decide en `Application`, donde sí se puede probar. En
Web queda solo la mecánica de HTTP.

No se expone un endpoint nuevo en la API: el único consumidor es la pantalla de pedido, y el
agente de WhatsApp (§7 de `direccion-entrega.md`) recibirá las coordenadas ya estructuradas,
sin enlaces que resolver.

## 9. Plan de implementación

| # | Capa | Archivo | Qué |
|---|---|---|---|
| 1 | Application | `Common/UbicacionCompartida.cs` | `TryObtenerEnlaceResoluble()` y la lista de anfitriones |
| 2 | Pruebas | `Atipico.Application.Tests/UbicacionCompartidaTest.cs` | qué URL se acepta, cuál no, y que un texto con coordenadas no pida resolver nada |
| 3 | Web | `Services/ResolvedorEnlaceUbicacion.cs` | el `HttpClient` con `AllowAutoRedirect = false` y el recorrido de saltos |
| 4 | Web | `Program.cs` | registrar el cliente con nombre y el servicio |
| 5 | Web | `Components/Pages/Pedidos/Edit.razor` | llamar al resolvedor cuando el parser no encontró nada |

## 10. Puesta en producción

Nada. Sin migración, sin variables de entorno, sin dependencias nuevas.

Lo único a verificar es que **el contenedor de la aplicación tenga salida a internet** hacia
Google. Ya la tiene para hablar con Cloudflare R2, así que no debería haber sorpresa — pero es
lo primero a mirar si en producción no resuelve y en desarrollo sí.
