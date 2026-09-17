---
name: atipico-workflow-in-practice
description: How the spec → plan → approval → code workflow is applied in practice
metadata:
  type: project
---

# Atipico Workflow in Practice

## Executive Summary

Atipico implements a strict, auditable workflow for every feature: **spec → plan → approval → code**. Each stage is documented, versioned, and tracked via git. The four specialized agents (atipico, db, qa, dev) each own distinct phases, with no overlap or regression.

---

## The Physical Workflow: Where Everything Lives

### Specs Directory: The Permanent Record

- **Location:** `specs/` — one `.md` file per feature
- **State Management:** YAML frontmatter declares `estado`, `ticket`, `actualizado`, `afecta`
- **Index:** `specs/README.md` lists all 27 specs with their state
- **Format:** Mandatory template in `specs/formato-spec.md`

**Current state (2026-09-16):** 4 propuesto · 0 aprobado · 14 implementado · 8 en-produccion · 1 descartado

### State Machine

```
propuesto → aprobado → implementado → en-produccion
                    ↓
                descartado
```

**Critical distinction:** `implementado` ≠ `en-produccion`. Code can be done while migrations wait to run against Neon.

---

## How Each Agent Works

### Agent 1: atipico (The Coordinator)
- **Owns:** Spec creation, plan drafting, agent coordination
- **Workflow:** Write spec → Draft plan → Get approval → Call qa → Call dev → Update spec state
- **Tools:** All tools; loads `graphify` for querying only

### Agent 2: db (Database Owner)
- **Owns:** `sql/` migrations, schema coherence, enum ↔ CHECK auditing
- **Rule:** Never connect to foreign persistent instances (Neon, user's localhost) without explicit request
- **Validation:** All done in disposable Docker containers (Testcontainers)
- **Deliverable:** Script + Runbook + Verification query (3 parts, 1 unit)

### Agent 3: qa (Quality Assurance)
- **Owns:** Unit tests (Moq), integration tests (real PostgreSQL), test stubs
- **Assertions from:** Spec acceptance criteria §6, never from guessing
- **Does NOT:** Implement logic, modify spec, run browser tests

### Agent 4: dev (Implementation)
- **Owns:** C# code (all layers), making red tests green
- **Rule:** Never modify tests or specs
- **Thresholds:** 10–15 lines/method, <10 cyclomatic complexity, <3 constructor deps
- **Tools:** Read, Glob, Grep, Bash, Edit, Write

---

## Real Example: reparacion-ck-cuenta-metodo (SCRUM-28)

1. **Spec (propuesto):** Problem: CHECK allows QR but enum doesn't (drift). Decision: Migration 015 adds YAPE/PLIN to both
2. **Plan (aprobado):** qa writes tests, db writes migration, dev implements enum
3. **Testing:** qa verifies enum ↔ CHECK with `ModeloEnumsCheckTest`
4. **Implementation:** dev adds 4 enum values, migration adds CHECK constraint
5. **Production:** User runs runbook, confirms, spec → en-produccion

---

## What This Prevents

- Typo features (no code before spec)
- Untested code (tests before code)
- Schema drift (migrations canonical, never ad-hoc)
- Silent database changes (Neon changes via migrations only)
- Scope creep (criteria define bounds)
- Forgotten runbooks (script + runbook + verification = 1 unit)
- Broken links (relative markdown, graphify indexes)
- Lost decisions (bitácora records everything)

---

## Key Practices in Code

1. **Migrations are commented** — explain why, reference spec/ticket
2. **Enums duplicate in SQL** — CHECK constraints match C# enums
3. **Tests use real database** — unit tests mock, integration tests use containers
4. **Timestamps server-stamped** — DateTimeOffset.UtcNow only
5. **DELETE always fails** — app as `app_restaurante`, no GRANT DELETE
6. **Frontmatter governs state** — YAML not prose; README mirrors it
7. **Navigation centralized** — Navegacion.cs, NavMenu/BarraInferior render from same model
8. **Breakpoint 641px** — mobile single breakpoint, 4 CSS files

---

## Where to Start

1. **Specs:** Read `specs/README.md` for the index (27 features, sorted by state)
2. **Format:** Read `specs/formato-spec.md` for mandatory structure
3. **Example spec:** Look at `specs/tipo-pedido.md` (implemented, simple, ~350 lines)
4. **Workflow:** Look at git log for how commits reference specs and tickets
5. **Migrations:** Read `sql/008_pedido_tipo.sql` for the pattern
6. **Tests:** Look at `Atipico.Domain.Tests` for unit test structure
