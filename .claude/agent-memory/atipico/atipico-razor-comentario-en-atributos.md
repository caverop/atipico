---
name: atipico-razor-comentario-en-atributos
description: Un comentario Razor entre los atributos de un componente compila sin warning y revienta en runtime; y escribir la secuencia de cierre dentro de un comentario lo corta antes de tiempo.
metadata:
  type: project
---

Dos trampas de Razor que no las agarra el compilador, encontradas al pasar parámetros nuevos a
`EntityTable` desde `Pedidos/Index.razor`:

**1. Comentario Razor DENTRO de la lista de atributos de un componente.** Esto:

```razor
<EntityTable TEntity="Pedido"
             WriteRoles="@(["Admin"])"
             @* explicacion del parametro que sigue *@
             DetalleRoles="@(["Admin", "Delivery"])" />
```

no es un comentario: Razor lo manda como **nombre de parámetro**. `dotnet build` pasa con 0
errores y 0 warnings, y explota recién en runtime con
`InvalidOperationException: Object of type '...' does not have a property matching the name '@* ... *@'`.
En bUnit aparece como fallo de TODOS los tests que rendericen esa página, lo que despista: parece
que rompiste la página entera, no que sobra un comentario. El comentario va **arriba de la
etiqueta**, en contexto de marcado.

**2. La secuencia de cierre de comentario adentro del comentario.** Escribir la secuencia `*` + `@`
dentro de un bloque `@* ... *@` (por ejemplo, para citar el problema anterior) **cierra el
comentario ahí mismo**, y el resto del texto pasa a compilarse como código: `error CS1525: El
término de expresión ')' no es válido`, apuntando a líneas que no tienen nada que ver. Escapar con
`@@` no ayuda. Si hay que hablar de comentarios Razor dentro de un comentario Razor, describilos
en palabras.

**Dónde:** `Atipico.Web/Components/Pages/Pedidos/Index.razor` tiene un comentario justo arriba del
`<EntityTable>` que documenta la trampa 1 para el próximo que pase.
