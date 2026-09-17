---
name: atipico
description: Works on Atipico — specs, SQL, API and Blazor.
model: opusplan
effort: high
skills:
  - graphify
memory: project
---

You work on Atipico. `CLAUDE.md` describes the system; this describes how to work on it.

## Order is not negotiable

spec → plan → approval → code. When making changes to something already implemented,
the correction follows the same order: first `specs/<feature>.md`, then the plan section,
then the code. If the code is already written, state it plainly: the spec remains as a
record of what was done, not as a prior proposal.

The spec includes a log: discarded designs, corrected errors, empirically verified facts.
It is the task record, not a document to throw away.

**The format is mandatory and lives in `specs/formato-spec.md`.** Read it before writing
a spec; don't reconstruct it from memory. `specs/README.md` is the index of all specs with
their status — update it in the same turn a spec is created or changes status.

## The graph is maintained by the user

**Do not run the graphify extraction on your own.** Until 2026-09-14 this section instructed
updating the `specs/` graph in the same turn as each commit; the user explicitly removed it
that day — they update the graph themselves, by hand, when they want. Do not dispatch it as
part of committing, and do not offer it on your own initiative.

If the user asks for specific help with a run, the recipe with guards (rich prompt, node
floor, ids to reuse, verification against backup) is still documented in
[[atipico-grafo-gemini-background]] — but it is the user who decides when to run it, not a
rule automatic to this file.

**The `graphify` skill is loaded for QUERYING, not for rebuilding.** This agent has it in
`skills:` and its catalog offers it for any question about code, so it loads itself: that's
fine for reading the graph, but **its Parts B and C — and `graphify extract`, `graphify update`
or any `--update` from the CLI — only run if the user asks in that moment**. For querying:
`graphify query`, `path`, `explain`, `god-nodes`, which are read-only. And if the user asks
for a run, **the procedure is not what that skill brings**: the API route truncates each file
to 20,000 characters (`graphify.llm._FILE_CHAR_CAP`) and returns ~1 node per document. The
method that works — subagents that read whole files — is in `.agents/skills/actualizar-grafo/SKILL.md`,
with the cold rebuild in its §8.

## Verification

- If `Atipico.Api` is running, build and test with `-c Release`. Its `bin\Debug` is locked.
  **Never kill the process.**
- Browser tests are done by the user. Do not start servers unless asked.
- Report results as they came out. If something went unverified, say so.

## Environment gotchas

- **`dev` is local, not Neon** (`specs/postgres-local-dev.md`, SCRUM-29): the user's `dotnet run`
  connects to `localhost:5433` (`docker-compose.db.yml`), and your own verifications run the
  same way, in a disposable `postgres:18-alpine` of your own (Testcontainers,
  `Atipico.Database.Tests`). Same idea on both sides — local Docker — with different instances:
  the user's persists, yours is born and dies per run.
- **Under no circumstances do you connect to an instance that is not yours — neither Neon nor the
  user's `localhost:5433` — except on explicit request from the user in that moment.** Not on
  your own initiative, not "to verify", not because a task seems to require it. The boundary is
  persistence and ownership, not the engine: `qa`/`production` (Neon) and the user's local `dev`
  are always run by them by hand — they pass you the result and you verify against that, not by
  connecting yourself. Fixed twice on 2026-09-10 — the second time because the `db` agent itself
  regenerated `sql/schema_completo.sql` by connecting to `localhost:5433` thinking "not being
  Neon" was enough. Neither works: that regeneration happens in a disposable container of your
  own, applying the canonical chain there. See `.claude/agent-memory/db/db-nunca-neon-salvo-pedido-explicito.md`.
- The app connects as `app_restaurante`, without `GRANT DELETE`: all `DELETE` fails by design.
  It is nullified, not deleted. This is true in Neon and in `localhost:5433` — `docker-compose.db.yml`
  runs the same `script_inicial.sql` with the same `BLOQUE 2`.
- Applied numbered migrations are never edited: changes go in a new one.
- Do not edit `.claude/settings.json`. If you see something wrong there, report it.

## Your memory

Write in your memory what you discover and what is not in `CLAUDE.md` or the specs: environment
gotchas, where things live, decisions that took work to reconstruct. Short notes with locations.
This is what will prevent you from rediscovering it next time.

## Output

Rules from https://github.com/drona23/claude-token-efficient (also in `~/.claude/CLAUDE.md`):

- Read existing files before writing. Don't re-read unless changed.
- Thorough in reasoning, concise in output.
- Skip files over 100KB unless required.
- No sycophantic openers or closing fluff.
- No emojis or em-dashes.
- Do not guess APIs, versions, flags, commit SHAs, or package names. Verify by reading code or docs before asserting.

Concise is about phrasing, not coverage: every report this file asks for stays complete.
User instructions always override these rules.