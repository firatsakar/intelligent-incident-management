# 🚨 Intelligent Incident Management

> An AI-assisted, event-driven incident management platform built with .NET 10 and a microservices architecture.

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Architecture](https://img.shields.io/badge/Architecture-Microservices-FF6B6B)]()
[![Messaging](https://img.shields.io/badge/Messaging-RabbitMQ-FF6600?logo=rabbitmq&logoColor=white)](https://www.rabbitmq.com/)
[![Database](https://img.shields.io/badge/Database-PostgreSQL-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Search](https://img.shields.io/badge/Search-Elasticsearch-005571?logo=elasticsearch&logoColor=white)](https://www.elastic.co/)
[![AI](https://img.shields.io/badge/AI-Anthropic_Claude-D4A27F?logo=anthropic&logoColor=white)](https://www.anthropic.com/)
[![Agent Framework](https://img.shields.io/badge/Agents-Microsoft_Agent_Framework-512BD4?logo=microsoft&logoColor=white)](https://github.com/microsoft/agent-framework)
[![Frontend](https://img.shields.io/badge/Frontend-React-61DAFB?logo=react&logoColor=black)](https://react.dev/)
[![License](https://img.shields.io/badge/License-Apache_2.0-blue)](LICENSE)

---

## 📖 Overview

**Intelligent Incident Management** is a self-hosted platform — services and a web console — that helps engineering teams detect, triage, and resolve operational incidents faster, with the help of AI.

When something goes wrong in production (a service degrades, an error rate spikes, a database connection pool drains), this platform captures the incident — noticed in the logs on its own, reported by an alerting tool through its API, or opened by a person — uses AI to assess its priority and probable root cause, and keeps everyone notified, all through a decoupled, event-driven architecture.

> **How it works, in short:** logs arrive — pulled from Seq or pushed over OTLP — and bursts of the same error become signals. A signal that keeps firing becomes an incident. An AI agent analyses each incident, searching the platform's own history of past incidents and the failing service's recent commits on its own initiative, and returns a suggested priority and category, evidence-based reasoning, remediation steps and a confidence score. The result lands on the incident, live in the console, and in the email, webhook or Jira notifications the organisation configured. Closing an incident records whether it was real, and the detector learns from that.

This project is built as a deep, hands-on exploration of **production-grade distributed systems design** with modern .NET.

---

## ✨ Key Features

- **AI-Powered Triage** — Incidents are automatically prioritized and categorized by Anthropic's Claude the moment they're created, with the result written back through a live, event-driven feedback loop.
- **Agentic Root Cause Analysis** — The AI agent decides *on its own* when to search past incidents, references genuinely matching cases in its reasoning, prefers remediation steps proven in *this* system, and lowers its confidence when no precedent exists. Powered by Microsoft Agent Framework tool-calling over an Elasticsearch corpus.
- **Reliable Delivery (Transactional Outbox)** — Database writes and external side effects (RabbitMQ publish, Elasticsearch indexing) are made atomic. No lost events, no phantom events — at-least-once delivery backed by idempotent consumers.
- **Event-Driven Architecture** — Services communicate asynchronously via RabbitMQ, fully decoupled from one another.
- **Per-Service Database Isolation** — Each microservice owns its data, following true microservice principles.
- **Portable Search Layer** — Similarity search runs on Elasticsearch, deliberately decoupled from the source-of-truth database, keeping the analysis engine independent of any specific storage backend (on-prem friendly).
- **Clean Architecture** — Every service follows a strict layered design (Domain → Application → Infrastructure → API).
- **CQRS** — Commands and queries are cleanly separated using MediatR.
- **Smart Notifications** — Email, webhook and Jira alerts go out when an analysis completes, filtered by priority and category per integration.
- **Incident History & Comments** — Every change on an incident is recorded with who made it — a person, an API key, the telemetry detector or the AI analysis — in the same transaction as the change, and the people working it comment in the same stream.
- **Telemetry-Driven Detection** — Incidents are raised automatically from bursts in the customer's logs, pulled from Seq or pushed over OTLP by any OpenTelemetry Collector or SDK.
- **Code-Aware Analysis (MCP)** — With a read-only GitHub token, the analysis reads the failing service's recent commits over GitHub's MCP server and names a suspected change, linked on the incident.
- **Incident API** — Alerting tools and scripts open incidents with an organisation API key, deduplicated by their own external id.
- **Organisations, Users & Roles** — Invitation-based users with Admin, Engineer and Viewer roles; every record is scoped to its organisation.
- **Secure by Default** — No default account or password: an installation generates its own secrets on first start, is claimed with a one-time setup code, and encrypts customer credentials at rest.

---

## 🖥️ The Console

The web console is served by the gateway, in English and Turkish, with a light and a dark theme. Everything updates live over WebSockets. What each page offers:

| Page | What it shows |
|---|---|
| **Dashboard** | Open incidents by priority; incidents opened (and closed) per day; the latest incidents; detection — the share the platform noticed on its own and how long that took; where incidents come from; time to resolution; detection accuracy from closing verdicts; how many incidents the AI analysed. Window of 7, 30 or 90 days. |
| **Incidents** | Every incident, filterable by status and priority, with its source, analysis state and detection latency. |
| **Incident detail** | Detection latency, what happened, how the detection gate scored it, the timeline, the full history of changes with comments, the AI analysis (category, priority, confidence, reasoning, suspected commits) and the notifications sent. Admins and Engineers change the status — closing asks whether it was a real problem — assign a team and comment. |
| **Signals** | A map of error signatures by service, sized and coloured by how often they fired, and the signals behind it with the gate's score for each. Select a tile to filter. |
| **Evidence** | The raw log records, signatures and signals of a time window, filterable by service. |
| **Signal funnel** | How many scored signals the gate held back and how many it acted on; log records → signatures → signals; how the gate ruled. |
| **Service health** | Per service: log volume, signals, promoted signals, incidents, top signature and last signal. Sortable. |
| **Delivery health** | Notification deliveries sent, failed and pending; median dispatch time per integration; the state and last failure of each integration. |
| **Settings › Profile** | Your identity, password, theme and language. |
| **Settings › Organization** | The organisation's name, the language AI analyses are written in, members and invitations. Admin only. |
| **Settings › Integrations** | Telemetry sources (Seq, OTLP) and incident API keys; the GitHub connection the analysis reads; notification channels (email, webhook, Jira). Admin only. |

---

## 🏛️ Architecture

The platform is composed of independent microservices coordinated through an event bus, following a **choreography pattern** — services react to events without knowing who published them.

```
       Web console                   Alerting tools                   Log shippers
            │                               │                               │
            ▼                               ▼                               ▼
┌───────────────────────────────────────────────────────────────────────────────────────┐
│       Gateway (YARP): serves the console, routes /api, /hubs (SignalR) and /otlp      │
└───────────────────────────────────────────────────────────────────────────────────────┘
        │                 │                 │                 │                 │
        ▼                 ▼                 ▼                 ▼                 ▼
┌───────────────┐ ┌───────────────┐ ┌───────────────┐ ┌───────────────┐ ┌───────────────┐
│    Identity   │ │    Incident   │ │  Notification │ │   Telemetry   │ │     Agent     │
│    Service    │ │    Service    │ │    Service    │ │   Ingestion   │ │  Orchestrator │
│               │ │               │ │               │ │    Service    │ │      (AI)     │
└───────┬───────┘ └───────┬───────┘ └───────┬───────┘ └───────┬───────┘ └───────┬───────┘
        ▲                 ▲                 ▲                 ▲                 ▲
        ▼                 ▼                 ▼                 ▼                 ▼
┌───────────────────────────────────────────────────────────────────────────────────────┐
│         RabbitMQ event bus: integration events, one durable queue per service         │
└───────────────────────────────────────────────────────────────────────────────────────┘

  Each service owns its PostgreSQL database. AgentOrchestrator also indexes analyses in
  Elasticsearch and reads the failing service's commits over GitHub's MCP server.
```

**The AI loop:** `IncidentService` publishes `IncidentDetectedEvent` → `AgentOrchestrator` consumes it and calls Claude. The agent forms a root-cause hypothesis and **calls its `search_similar_incidents` tool on its own** to check the Elasticsearch corpus of past analyses. It persists the analysis, then — through a **Transactional Outbox** — atomically records its intent to (1) publish `IncidentAnalyzedEvent` to RabbitMQ and (2) index the analysis into Elasticsearch. A background dispatcher delivers both reliably. `IncidentService` consumes the event and updates the incident. No manual trigger, no central orchestrator — just services reacting to events.

| Event | From → To | What it means |
|-------|-----------|---------------|
| `OrganizationCreatedEvent` | Identity → Telemetry | A new organisation gets its detection rule. |
| `SignalPromotedEvent` | Telemetry → Incident | A signal passed the detection gate: open an incident. |
| `IncidentDetectedEvent` | Incident → Agent | A new incident to analyse. |
| `IncidentAnalyzedEvent` | Agent → Incident, Notification | The analysis: applied to the incident and sent to the integrations. |
| `IncidentAnalysisFailedEvent` | Agent → Incident | The analysis failed; the incident shows it. |
| `IncidentResolvedEvent` | Incident → Telemetry | Closed with a verdict: the signature is released and the detector learns from it. |

### Services

| Service | Responsibility |
|---------|----------------|
| **Gateway** | The single entry point (YARP). Serves the web console and routes `/api`, `/hubs` and `/otlp` to the services; rate-limits the incident API per key. |
| **IdentityService** | Organisations, users, invitations and roles; sign-in with short-lived JWTs and refresh tokens; the first-run setup. |
| **IncidentService** | Core incident lifecycle — create, track, update status, assign teams, comment, close with a verdict — with the full history of changes. Opens incidents from promoted signals and from the incident API. Consumes AI results and applies them to the incident. |
| **AgentOrchestrator** | The AI brain — analyzes incidents and suggests priority, category, reasoning, remediation steps, and a confidence score using Anthropic Claude via the Microsoft Agent Framework. Performs **agentic root cause analysis** by searching past incidents (Elasticsearch tool-calling) and delivers results reliably via a Transactional Outbox. |
| **NotificationService** | Sends email, webhook and Jira notifications when an analysis completes, filtered by priority and category per integration, and keeps the delivery history. |
| **TelemetryIngestionService** | Pulls logs from Seq or receives them over OTLP/HTTP (`/otlp/v1/logs`), folds bursts into signatures, and promotes anomalous ones to incidents. Learns from the verdicts incidents are closed with. |

### Shared Building Blocks

| Block | Purpose |
|-------|---------|
| **SharedKernel** | Base domain primitives (`Entity`, `AggregateRoot`, `DomainEvent`, `ValueObject`), the organisation context, access keys and at-rest encryption of secrets. |
| **EventBus** | RabbitMQ abstraction for publishing and subscribing to integration events. |
| **Contracts** | Shared integration event definitions exchanged between services. |
| **Observability** | Structured logging (Serilog → Seq) and OpenTelemetry tracing (OTLP → Seq) for every service. |
| **Outbox** | The transactional outbox: domain events harvested into the same `SaveChanges`, a dispatcher with growing back-off, one worker per message across replicas. |
| **Application** | The validation pipeline, and masking and encryption of credentials in integration settings. |
| **Web** | JWT authentication and roles, organisation scoping, the SignalR hub base, forwarded headers, migrations on start and the generated-secrets reader. |

---

## 🔍 Detection — How Logs Become Incidents

Incidents are opened from logs by a deterministic gate, not by the AI. Every part of a signal's
score is recorded on it and shown on the **Signals** screen, so any promotion can be explained. The
AI comes in afterwards, to analyse the incident the gate opened.

**1. Only errors count.** Severity is read from the log's own structured level, never by searching
the text for "error". From Seq, the connector asks for `@Level in ['Error','Fatal']` and reads each
event's `Level`; over OTLP it is the standard `SeverityNumber` (17–20 Error, 21–24 Fatal), or
`SeverityText` when no number is set. Information and Warning records are kept as context for the
evidence screen but never raise anything, and a plain-text line without a level counts as
Information — so the platform expects structured logs (Serilog, OpenTelemetry and the like).

**2. Errors are grouped into signatures.** Two errors are the same error when they come from the
same service, throw the same exception type, and say the same thing once the volatile parts are
removed: the log's message template is used when it has one; otherwise ids, email addresses, long
hex strings and numbers are masked. The signature is a hash of those three. Repeats are folded — a
signature keeps its count and a few sample lines, not every copy.

**3. A burst raises a signal.** Each batch of logs is checked against the signatures it touched.
The rule an organisation starts with is **3 errors of one signature within 5 minutes**; a fatal
error does not wait for the count. A signature raises at most one signal per 5-minute window, so
the count measures the error rather than how often the platform looked. A rule scoped to one
service takes precedence over the catch-all.

**4. The signal is scored.** Confidence starts at 0.55 and moves with the evidence, clamped to 0–1:

| Component | Effect |
|-----------|--------|
| Burst over the threshold | 0.55 to start with |
| Size of the burst | +0.10 for each doubling past the threshold, up to +0.30 |
| Rate anomaly | +0.25 when the rate is unusual for this signature: a z-score of 2 or more against its previous 12 windows. An error never seen before counts as unusual. |
| History | −0.25 to +0.15, from how this signature's earlier incidents were closed — real or false alarm. Discounted while there are few verdicts: with *n* of them it carries *n*/(*n*+2) of its value. |
| Fatal | Scoring is skipped: confidence 1.0. |

**5. The score decides.**

| Confidence | Outcome |
|------------|---------|
| Below 0.60 | Recorded, nothing more. |
| 0.60 up to 0.90 | **Weak** — shown to people, wakes nobody. |
| 0.90 or more, with this signature's incident still open and the error seen within 24 hours | **Deduplicated** — counted into the open incident instead of opening another. |
| 0.90 or more otherwise | **Promoted** — an incident opens with a title, an evidence summary and a sample stack trace, starting at Critical (fatal), High (ten times the threshold) or Medium. The AI analysis then sets the real priority. |

A promotion leaves through the transactional outbox, in the same transaction as the signal.

**6. Verdicts teach the gate.** Closing an incident asks whether it was a real problem. The answer
releases the signature from its incident and is counted on it, and the history term uses those
counts the next time the error bursts. It is asymmetric on purpose: a false alarm costs more
(−0.25) than a confirmation earns (+0.15), because waking someone for nothing again is the worse
mistake.

**An example.** A new error starts firing six times every ten seconds. Detection runs as the logs
arrive, so the third occurrence already raises a signal: 0.55 + 0.25 (never seen before) =
**0.80, Weak**. Five minutes later the window holds about 180 occurrences: 0.55 + 0.30 (far past
the threshold) + 0.25 (unusual rate) = 1.10, clamped to **1.00 — promoted**, and an incident opens.
While it stays open, later bursts of the same error are counted into it.

---

## 🧠 Root Cause Analysis — How It Works

RCA turns the AI from a passive classifier into an evidence-driven investigator. Three technologies combine:

- **Elasticsearch (search layer).** Past analyses are indexed with an explicit mapping (`english` analyzer on `title`/`description`/`reasoning`, `keyword` on categorical fields). Similarity search uses a BM25 `multi_match` query with a `title^2` boost. The index is a **derived view** — losable and fully rebuildable from PostgreSQL via a backfill path — so it is never a source of truth.
- **Transactional Outbox (reliable delivery).** A database write and its external side effects can't share a transaction. The outbox records side-effect *intent* as a row in the *same* transaction as the analysis (via a `SaveChanges` interceptor that harvests domain events). A separate polling dispatcher then delivers to RabbitMQ and Elasticsearch with **at-least-once** semantics. Idempotent consumers (Elasticsearch upsert keyed by `IncidentId`, idempotent event re-application) make retries safe. This closed a pre-existing dual-write gap.
- **Agentic tool-calling (Microsoft Agent Framework).** The agent is given a `search_similar_incidents` tool and *decides for itself* whether and what to search — querying its own root-cause hypothesis (e.g. `"Npgsql connection pool exhausted"`), not the raw symptom. It reads the results, judges relevance itself (the relevance score is a hint, not a verdict), cites genuinely matching incidents in its reasoning, and **lowers its confidence when no precedent is found** rather than inflating it.

The result: remediation steps specific to *this* system's history, and a confidence score grounded in evidence rather than model self-assurance.

---

## 🛠️ Tech Stack

- **Runtime:** .NET 10 / ASP.NET Core
- **Frontend:** React 19, TypeScript, Vite, Tailwind CSS 4, TanStack Query, Base UI
- **Gateway & realtime:** YARP, SignalR
- **Messaging:** RabbitMQ
- **Database:** PostgreSQL (isolated per service)
- **Search:** Elasticsearch 9.4 + Kibana (BM25 similarity search / RCA corpus)
- **AI:** Anthropic Claude API via [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) — including agentic tool-calling; GitHub's MCP server through the Model Context Protocol C# SDK
- **Patterns:** Clean Architecture, CQRS, Domain-Driven Design, Event-Driven Architecture, Transactional Outbox
- **Libraries:** MediatR, FluentValidation, Entity Framework Core, Polly
- **Observability:** Seq (structured logs and distributed traces), OpenTelemetry
- **Testing:** xUnit, NSubstitute, Testcontainers (PostgreSQL, RabbitMQ)
- **Infrastructure:** Docker & Docker Compose

---

## 🚀 Getting Started

### Install with Docker

Everything — infrastructure, services, gateway and web console — comes up from one compose file.
All you need is [Docker](https://www.docker.com/) with Compose v2 (Docker Desktop on Windows and
macOS) and about 6 GB of memory for it.

**1. Get the code and start it.** Nothing needs to be filled in first.

```bash
git clone https://github.com/firatsakar/intelligent-incident-management.git
cd intelligent-incident-management
docker compose up --build
```

The first build takes several minutes. Add `-d` to run it in the background. On the first start a
short-lived `secrets` container generates every password and key this installation needs — the
session signing key, the key that encrypts stored credentials, the database, RabbitMQ, Seq and
pgAdmin passwords — into a Docker volume that everything else reads from. They are never printed
and never leave that volume.

**2. Turn on the AI analysis** (optional, but it is what the platform is for). Give it your
Anthropic API key, from [console.anthropic.com](https://console.anthropic.com) → *API keys*:

```bash
cp example.env .env
```

Uncomment `ANTHROPIC_API_KEY=` in `.env`, put the key after it, and run `docker compose up -d` again.
Without a key everything else works and each analysis is recorded as failed. `example.env` lists
every other optional setting — ports, public URL, email, log retention.

**3. Open the console** at http://localhost:8080. An empty installation asks for a one-time
setup code (see [First-run setup](#first-run-setup)); the identity service writes it to its log:

```bash
docker compose logs identity | grep "First-run setup"
```

Enter it with your organisation's name and your own name, email and password — you are its first
Admin. From there, connect a log source under *Settings › Integrations* and invite your team under
*Settings › Organization*.

| What | Where |
|------|-------|
| Console and API | `http://<server>:8080` — the only port open to the network |
| OTLP log ingest | `http://<server>:8080/otlp/v1/logs` |
| Seq (IIM's own logs and traces) | http://127.0.0.1:8081 — user `admin` |
| pgAdmin | http://127.0.0.1:5050 — user `admin@example.com` |
| Kibana | http://127.0.0.1:5601 |

The generated passwords of the admin interfaces are read from the `secrets` volume when you need
them (Seq asks for a new one at the first sign-in; pgAdmin's database password is
`postgres-password`):

```bash
docker compose run --rm secrets cat /secrets/raw/seq-admin-password
```

(In Git Bash on Windows, prefix it with `MSYS_NO_PATHCONV=1` so the path is not rewritten.)

The admin interfaces answer on the server itself only. From another machine, tunnel to them:
`ssh -L 8081:127.0.0.1:8081 -L 5050:127.0.0.1:5050 -L 5601:127.0.0.1:5601 you@server`.

**HTTPS.** Put your own TLS proxy in front of port 8080 — for example Caddy:

```text
iim.example.com {
    reverse_proxy localhost:8080
}
```

Then set `IIM_PUBLIC_URL=https://iim.example.com` in `.env` and tell IIM to believe the proxy about
the caller's address and scheme: `FORWARDED_KNOWN_PROXIES=172.16.0.0/12` for a proxy on the Docker
host (it reaches the published port from the Docker bridge). Over plain HTTP — trying it out on a
LAN address — everything works too, the session cookies are simply not marked Secure.

**Email.** Invitations and password resets are sent through the SMTP server in `SMTP_*` (your
company's, Google Workspace, Microsoft 365, SES, SendGrid…). Without one, each invitation link is
shown to the Admin to pass on. Incident notification emails are separate: each organisation enters
its own SMTP server in the console, under *Settings › Integrations › Notifications*.

**Updating.** `git pull`, then `docker compose up -d --build`. Each service applies its own database
migrations as it starts. `docker compose down` stops everything and keeps the data, which lives in
Docker volumes. Keep the `iim_secrets` volume: it holds this installation's keys, and without it
everybody is signed out and the stored integration credentials can no longer be decrypted.

### Development

For working on the code, the services run with `dotnet run` and the console with Vite, against
infrastructure in Docker. You need:

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 24](https://nodejs.org/)
- [Docker & Docker Compose](https://www.docker.com/)

The commands below are for bash; on Windows, use Git Bash.

**1. Start the infrastructure.** `docker-compose.dev.yml` runs RabbitMQ, a PostgreSQL per service,
Elasticsearch, Kibana, Seq, pgAdmin and Mailpit, published on `127.0.0.1` only. Its development
passwords match the connection strings in each service's `appsettings.json`, so no `.env` is
needed.

```bash
docker compose -f docker-compose.dev.yml up -d
```

| Tool | URL | Sign-in |
|------|-----|---------|
| RabbitMQ Management | http://localhost:15672 | `admin` / `Admin1234!` |
| Seq (the platform's logs and traces) | http://localhost:8081 | `admin` / `Admin1234!` |
| pgAdmin | http://localhost:5050 | `admin@example.com` / `Admin1234!` |
| Mailpit (every email the platform sends) | http://localhost:8025 | — |
| Elasticsearch | http://localhost:9200 | — |
| Kibana | http://localhost:5601 | — |
| Seq (demo: a customer's log system to connect) | http://localhost:8082 | — |

**2. Set the secrets, once.** They go into user secrets and are never committed. Every service
validates the same JWT signing key; three services also encrypt customer credentials in their
databases, each with its own key. The Anthropic key is optional: without it incidents are still
created and detected, but their AI analysis fails.

```bash
jwt_key=$(openssl rand -base64 48)
for service in IdentityService IncidentService NotificationService TelemetryIngestionService AgentOrchestrator; do
  dotnet user-secrets set "Jwt:SigningKey" "$jwt_key" --project src/Services/$service/$service.API
done
for service in NotificationService TelemetryIngestionService AgentOrchestrator; do
  dotnet user-secrets set "Secrets:EncryptionKey" "$(openssl rand -base64 32)" --project src/Services/$service/$service.API
done
dotnet user-secrets set "AiAnalyzer:ApiKey" "<your Anthropic API key>" --project src/Services/AgentOrchestrator/AgentOrchestrator.API
```

**3. Create the databases.** In development, migrations are applied by hand, so a new migration
is read before it runs:

```bash
dotnet tool install --global dotnet-ef
for service in IdentityService IncidentService NotificationService TelemetryIngestionService AgentOrchestrator; do
  dotnet ef database update --project src/Services/$service/$service.Infrastructure --startup-project src/Services/$service/$service.API
done
```

**4. Run the services, the gateway and the console.** Each in its own terminal:

```bash
dotnet run --project src/Services/IdentityService/IdentityService.API --launch-profile http
dotnet run --project src/Services/IncidentService/IncidentService.API --launch-profile http
dotnet run --project src/Services/NotificationService/NotificationService.API --launch-profile http
dotnet run --project src/Services/TelemetryIngestionService/TelemetryIngestionService.API --launch-profile http
dotnet run --project src/Services/AgentOrchestrator/AgentOrchestrator.API --launch-profile http
dotnet run --project src/Gateway/Gateway.API --launch-profile http
```

```bash
npm ci --prefix web
npm run dev --prefix web
```

The console is at http://localhost:5173. Vite sends `/api`, `/hubs` and `/otlp` to the gateway
(http://localhost:5100), which routes them to the services. The first time, the console opens the
first-run setup, described below.

**Tests.** `dotnet test` runs the unit tests and `tests/Integration.Tests`, which start a
throwaway Postgres and RabbitMQ in Docker through Testcontainers, so Docker must be running. They
check what only the real thing can: every service's migrations against its model, organisation
filters in SQL, unique indexes, and a subscription made before the broker is up. For the console,
`npm run build`, `npm run lint` and `npm run check:i18n` in `web/`. See
[CONTRIBUTING.md](CONTRIBUTING.md).

### First-run setup

There is no default account and no default password. On an empty database the identity service
writes a one-time setup code to its log, on a single warning line:

```text
[WRN] First-run setup: no organisation exists yet. Open the console — it asks for this one-time setup code: ABCD-EFGH-JKMN. ...
```

1. Open the console — http://localhost:8080 in a Docker install; in development, start the
   services, the gateway and `npm run dev --prefix web`. It goes straight to the setup screen.
2. Enter the code from the log, your organisation's name, and your own name, email and password
   (12 characters or more). You are signed in as the organisation's first Admin.
3. Invite your team from **Settings → Organization**, where the organisation's name can also be
   changed later.

The code only lives in memory: restarting the identity service issues a new one, and completing
the setup spends it — once any user exists, the setup screen never opens again. One installation
holds one organisation.

For an automated install, skip the screen by configuring the first Admin before the first start
(environment variables, or user secrets in development):

```bash
Identity__Seed__Email=admin@example.com
Identity__Seed__Password=<at least 12 characters>
Identity__Seed__OrganizationName="Example Operations"
Identity__Seed__DisplayName="Platform Admin"
```

### Incident API

IIM finds incidents itself, from telemetry. A system that already knows it has a problem — a
script, a CI pipeline, your own alerting — can report one too. An Admin makes a key under
**Settings → Integrations → Observability → Incident API**; it is shown once.

```bash
curl -X POST http://localhost:8080/api/incidents/intake   -H "Content-Type: application/json"   -H "X-IIM-Api-Key: iim_inc_…"   -d '{"title": "Checkout error rate above 5%", "description": "5xx rate above 5% for 10 minutes.", "priority": "High", "externalId": "checkout-error-rate"}'
```

| Field | |
|-------|-|
| `title`, `description` | Required. |
| `priority` | `Critical`, `High`, `Medium` (default) or `Low`. The analysis suggests one either way. |
| `externalId` | Your own name for the problem, such as an alert fingerprint. While an incident with it is open, sending it again returns that incident instead of opening another; once it is resolved, the next one opens a new incident. |
| `detectedAt` | When the problem started (ISO 8601), if not now. |

| Response | |
|----------|-|
| `201 {"id": "…", "created": true}` | A new incident, analysed like any other. |
| `200 {"id": "…", "created": false}` | An open incident already has this `externalId`. |
| `400` | The body is invalid; the errors name the fields. |
| `401` | The key is missing, unknown or deleted. |
| `429` | More than 60 requests in a minute with one key. |

A key belongs to the organisation and can open incidents, nothing else — it cannot read them.
Every incident it opens shows its name. To rotate one without a gap, make a new key, move the
sender to it, then delete the old one. Alertmanager, Grafana and similar tools send this JSON from
their own webhook templates.

---

## 🗺️ Roadmap

- [x] Core infrastructure (Event Bus, Docker, Shared Kernel)
- [x] IncidentService — full CRUD with validation & error handling
- [x] AgentOrchestrator — AI-powered priority & category suggestion (Anthropic Claude)
- [x] End-to-end bidirectional event flow (incident created → AI analyzed → incident updated, fully autonomous)
- [x] AgentOrchestrator refactor onto the Microsoft Agent Framework
- [x] Root cause analysis (RCA) — Elasticsearch similarity search + Transactional Outbox + agentic tool-calling with suggested remediation steps & evidence-based confidence
- [x] NotificationService — email/webhook/Jira alerts on incident lifecycle events
- [x] TelemetryIngestionService — anomaly-based incident detection, Seq pull and OTLP push ingest
- [x] Feedback loop — closing an incident records whether it was real or a false positive, and the detector scores that error's next burst accordingly
- [x] Incident history & comments — who changed what and when, recorded with the change; comments from Admins and Engineers
- [x] API Gateway (YARP) & JWT authentication, organisation-scoped data, roles
- [x] Invitation-based user management — Admins invite by email, change roles, deactivate accounts and issue password resets; organisation settings are Admin-only
- [x] Distributed tracing with OpenTelemetry — one trace from a pushed log line to the notification
- [x] First-run setup — an empty installation is claimed from the console with a one-time code from the server's log; no default credentials
- [x] Incident API — external systems open incidents with an organisation API key, deduplicated by their own external id
- [x] **MCP integration, GitHub first** — the analysis reads the failing service's recent commits over GitHub's MCP server (read-only, the organisation's own token) and names a suspected change, linked on the incident
- [ ] More MCP sources (Grafana, Kubernetes, PagerDuty) on the same client
- [x] Unit & integration tests (integration tests run real PostgreSQL and RabbitMQ in Docker)
- [x] React frontend & analytics dashboard (time to resolution, detection accuracy, AI analysis, trends)
- [x] One-command install — Dockerfiles and a Docker Compose file for the whole platform (CI/CD and Kubernetes are left to each installation)

---

## 📐 Design Principles

This project deliberately favors **clarity and correctness** over shortcuts:

- **Each service owns its data.** No shared databases, no hidden coupling.
- **Publishers don't know their subscribers.** Services emit events; whoever cares, listens.
- **The domain is protected.** Business rules live in the domain layer, shielded from infrastructure concerns.
- **Interfaces belong to their consumer.** Abstractions live in the Application layer; Infrastructure hides the implementation. This let the AI framework swap (bare SDK → Agent Framework) and the search backend swap (PostgreSQL → Elasticsearch) touch only Infrastructure + DI, never Domain or Application.
- **Cross-cutting concerns are centralized.** Validation and error handling are handled via pipelines and middleware, not scattered across handlers.
- **AI output is structured but schema-flexible.** AI-generated analysis is stored as `jsonb`, so richer output (reasoning, remediation steps, confidence scores, model metadata) can be added without a database migration.
- **Search is a derived view, not a source of truth.** Elasticsearch can be wiped and rebuilt from PostgreSQL at any time; its startup failure is non-fatal by design.
- **Reliability over convenience.** External side effects go through a Transactional Outbox rather than fire-and-forget publishing, trading a little latency for guaranteed, atomic delivery.
- **YAGNI, and no premature abstraction.** Shared code is extracted on the *second* real use, not on speculation. (This is also why MCP is used only across a real boundary — GitHub's own server — while the agent reads the platform's own data through in-process tools.)

---

## 📝 License

Licensed under the [Apache License 2.0](LICENSE).

Contributions are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md). Please report security issues privately, as described in [SECURITY.md](SECURITY.md).

---

*Built with a focus on learning production-grade distributed systems architecture.*
