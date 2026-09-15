# Fuentes

Material de terceros que respalda decisiones. **No es documentación del proyecto** —eso vive en
`specs/`— y por eso está fuera: `specs/` es el corpus del grafo de graphify, y meter
documentación de terceros ahí le agrega nodos que no son decisiones de Atipico.

Es un vault de Obsidian aparte (este directorio), separado del de `specs/`.

## Cómo está organizado

```
fuentes/
  README.md                 ← este archivo: el mapa de temas
  <tema>/
    INDICE.md               ← la tabla que se lee para decidir
    NN-slug.md              ← una fuente por archivo
```

- **Un directorio por tema**, no por consulta. El tema es el que se acumula.
- **`INDICE.md` es el artefacto de decisión**: qué fuentes hay, qué aporta cada una, y si el
  tema ya merece un notebook.
- **Se versiona.** Es el rastro de *de dónde salió cada decisión*; una URL cambia o muere, el
  rastro no.

## Formato de una fuente

```markdown
# Título de la fuente

- **URL:** …
- **Capturada:** AAAA-MM-DD
- **Tipo:** documentación oficial | issue | artículo
- **Para qué la usé:** la decisión concreta, no "porque era interesante"

## Extracto

> Citas textuales de lo que importa.

## Datos que me llevé

- Los números y hechos concretos.

## Qué NO dice / límites

- Los huecos, para que nadie le atribuya a la fuente más de lo que dice.
```

**URL + extracto, no texto completo.** El texto entero de un tercero en un repo versionado es
una decisión de licencia si el repo es público. Documentación técnica se cita generosamente;
artículos y contenido con paywall, con la cita mínima que sostiene la afirmación.

## Cuándo un tema merece un notebook de NotebookLM

**≥8 fuentes sobre el mismo tema, y el tema se repite.** El free tier tope es 50 fuentes por
notebook, así que el notebook se abre cuando el tema ya demostró que vuelve — no en la primera
consulta. Mientras tanto, esta carpeta es suficiente: se lee sin herramienta y ya tiene el
material que habría que subir.

## Temas

| Tema | Fuentes | Última captura | ¿Notebook? |
|---|---|---|---|
| [claude-code-modelos](claude-code-modelos/INDICE.md) | 3 | 2026-09-15 | todavía no |
