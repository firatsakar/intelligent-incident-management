# Intelligent Incident Management (IIM) — Working Agreement

> Conversation with Fırat is in **Turkish**. All code, comments, commit messages,
> and log output are in **English**. This split is mandatory.

---

## 0. Task lifecycle — MANDATORY, before any code

For **every** task you are given, do this before writing or changing any code:

1. **Create a Jira issue** in project `IIM` (replace with the real project key).
   - Fill in: summary, description, issue type, and acceptance criteria.
   - Report the issue key back to me (e.g. `IIM-123`) as soon as it exists.
2. **Transition the Jira issue to `In Progress`.**
3. **Update `PROGRESS.md`**: mark the matching Adım/Parça as `[~] Devam ediyor`
   and write the Jira key next to it.
4. Only then begin implementation.
5. When the work is complete:
   - Add a Jira comment summarizing what changed and list the affected file paths.
   - Transition the Jira issue to `In Review` (or the correct next state).
   - **Update `PROGRESS.md`**: flip the item to `[x] Yapıldı`, add the date and
     Jira key. If the item was deferred or skipped, mark it `[!] Yapılmadı` with a
     one-line reason instead.
6. If a task is large, split it into subtasks — one Jira subtask per "Parça", and
   one `PROGRESS.md` line per Parça. Keep Jira and `PROGRESS.md` in sync as you go.

Jira write actions (create / transition / comment) require my confirmation — they
are in the `ask` list. Never assume a status transition; do it explicitly.
`PROGRESS.md` is the single source of truth for roadmap position — read it at the
start of a task and keep it accurate. Never mark something `[x] Yapıldı` unless it
builds (and tests pass where they exist).

---

## 1. How we work

- **Approval-based progression**: propose a plan → wait for my confirmation → then
  implement. Start in Plan Mode for anything non-trivial.
- Work is structured as **Adım** (step) + **Parça** (chunk).
- **Build-verify between parçalar**: each chunk must build (and tests pass where
  they exist) before moving to the next.
- **Discovery-first for unknown APIs**: when an external/library API is unclear,
  add debug logging and inspect at runtime instead of writing blind mappings.
- **Fallback preservation**: during risky refactors, keep the old implementation
  until the new one is explicitly confirmed stable. Do not delete it early.
- **Communication style**: file-path-first, minimal commentary. Every code change
  must clearly state its target file path.
- Flag only genuinely important issues, and briefly. **Speed is the priority** —
  I will ask if I want more depth. Avoid long architectural/technical/practical
  essays unless I request them.

---

## Git & branch strategy

Solo development — no pull requests. Merge directly.

- `master` — stable / release only. Merged from `develop` at release points.
  Never commit directly to it.
- `develop` — active integration branch. All work branches from here and merges
  back here.
- **One branch per Adım**, named with the Jira key:
  `feature/IIM-<key>-<short-desc>` (e.g. `feature/IIM-42-notification-service`).
- **One commit per Parça**, so history shows what each chunk did. Commit message
  starts with the relevant Jira key: `IIM-42: add Integration entity + migration`.
- Merge the Adım branch back into `develop` with a **merge commit**
  (`git merge --no-ff`) to preserve Parça history — **never squash**.
- After merge: transition the Jira issue and update `PROGRESS.md`.
- **No confirmation needed** for creating branches, committing, pushing, merging.
- **Always ask me** before any delete or undo (branch delete, revert, reset,
  restore/discard, clean, remote-branch delete). The catastrophic ones are
  hard-blocked in `.claude/settings.json` (force push, `reset --hard`, force
  branch delete, `rm -rf`, volume/DB drops). Never rewrite published history on
  `master` or `develop`.

---

## 2. Project context

- **Repo**: github.com/firatsakar/intelligent-incident-management — work on `develop`.
- Production-grade, AI-assisted incident management platform.
- **.NET 10**, Clean Architecture, distributed microservices.
- **BuildingBlocks**: Contracts / EventBus / Observability / SharedKernel.
- **Services**: AgentOrchestrator, IncidentService, NotificationService,
  TelemetryIngestionService.
- **Current step**: Adım 12 — NotificationService (email / webhook / Jira
  notifications on AI analysis completion, driven by a customer-configurable
  Integration config table).
- **Roadmap & live status live in `PROGRESS.md`** — that file, not this one, is the
  authoritative record of what is done / in progress / pending. Consult it first.

### Tech stack
- RabbitMQ, PostgreSQL (per-service), Seq, pgAdmin, Docker Compose (base / override / .env).
- Elasticsearch 9.4.3 + Kibana 9.4.3; `Elastic.Clients.Elasticsearch` 9.4.2 (pinned).
- MAF: `Microsoft.Agents.AI` 1.12.0 + `Microsoft.Agents.AI.Anthropic` 1.12.0-preview.
- MediatR v14, FluentValidation v12, Polly, EF Core + Npgsql.
- Anthropic Claude (`claude-sonnet-4-6`), `Temperature=0` for analysis.

---

## 3. Architectural rules (do not violate)

### Messaging & events
- **Queue-per-service, not queue-per-event-type**: one durable queue per service
  (via `SubscriptionClientName`), direct exchange with routing-key bindings for
  fan-out. Queue-per-event-type creates competing consumers.
- Distinguish three event kinds: `DomainEvent` (intra-service) vs
  `IntegrationEvent`/RabbitMQ (inter-service, in BuildingBlocks.Contracts) vs
  service-specific domain events.
- Two service domains must **never** reference each other directly.

### Outbox & dispatch
- Outbox atomicity comes from the **same DB transaction**: an interceptor harvests
  domain events into `OutboxMessage` rows within the same `SaveChanges`.
- At-least-once delivery + idempotent consumers (ES `_id=IncidentId` upsert,
  idempotent `ApplyAiAnalysis`).
- **Dispatch ordering is critical**: apply the side effect first, *then* stamp
  `ProcessedOn`. Reversing risks silent message loss on a crash between operations.

### MCP
- MCP is for cross-boundary tool sharing (different owner/client/language/process).
- Use in-process `AIFunction` for your own data in your own process.
- **Never** use MCP to call your own methods over the network.

### Elasticsearch
- ES is a **derived view**: search is best-effort. The indexer throws on failure;
  the searcher returns an empty list + WARNING. The app stays alive if ES is down.
- Use `Elastic.Clients.Elasticsearch`, not legacy NEST. Pin to 9.4.x (loose semver).
- `english` analyzer for full-text fields (stemming); `keyword` for categorical.

### AI tool design
- Tool signatures carry **intent, not data shape** — prefer a single query string
  over structured fields, so the AI searches its own hypothesis.
- Hidden parameters (e.g. `excludeIncidentId`) belong in the closure, not exposed
  to the model.
- BM25 scores are signals, not verdicts: prompt the AI to judge relevance itself.
- Per-call agent construction is correct when closure vars differ per call.
- JSON parse robustness: extract embedded JSON via
  `IndexOf('{')...LastIndexOf('}')`, since multi-turn tool-calling prefixes prose.

### General discipline
- **YAGNI**: extract shared BuildingBlocks only when a genuine second consumer
  exists. No premature abstraction.
- `IHostedService` in Infrastructure class libraries: reference
  `Microsoft.Extensions.Hosting.Abstractions`, not the full host package.

---

## 4. Hard limits (also enforced in .claude/settings.json)

- Never read or write `.env`, `.env.*`, or `appsettings.*.json` (secrets live there).
- Never run destructive commands: `rm -rf`, `git reset --hard`, force push,
  `dotnet ef database drop`, `docker compose down -v`.
- EF migrations and `docker compose up/down` require my confirmation.
- Do not run a feature as "done" without tests where the layer has test coverage.
