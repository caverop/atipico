---
name: atipico-narrow-enum-over-migration
description: "Cuando el CHECK de la DB ya permite un superconjunto, se angosta el enum de C# y no se escribe migración."
metadata:
  type: feedback
---

Cuando piden restringir qué valores de un enum (como `MetodoPago`) son usables, y el
CHECK constraint de la DB viva ya permite un superconjunto de los valores que quedan,
angostar solamente el enum de C# — no escribir un `sql/00X_*.sql` para apretar también
la constraint.

**Why:** confirmado dos veces en la misma sesión (2026-08-18): primero al sacar
Yape/Plin de `MetodoPago` (la DB ya permitía `EFECTIVO,TARJETA,TRANSFERENCIA,QR`, se
quedaron `Efectivo,Tarjeta,Transferencia`), y otra vez al decir que los únicos métodos
debían ser QR y Efectivo (`Qr` mapea al `'QR'` ya existente vía
`UpperSnakeCaseEnumConverter`, tampoco hizo falta migración). En ambos casos que el
CHECK sea un superconjunto estricto de lo que usa C# se trata como holgura inofensiva,
no como algo a corregir — consistente con el hallazgo de drift en `ck_cuenta_metodo`
de [[atipico-improvement-plan]], donde el usuario eligió explícitamente "angostar C#"
sobre "arreglar la DB" una vez que supo qué valores estaban vivos.

**How to apply:** antes de proponer una migración por un pedido de angostar un enum,
chequear si el CHECK actual (vía `sql/schema_completo.sql` o un `pg_dump` en vivo) ya
cubre los valores que quedan. Si sí, tocar solo el enum de C# (y los comentarios/docs
que citen la lista vieja) — no preguntar si además hay que migrar. Proponer migración
solo cuando la constraint NO permite un valor que el usuario quiere conservar.
