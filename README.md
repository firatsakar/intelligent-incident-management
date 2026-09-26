# 🚨 Intelligent Incident Management

> An AI-assisted, event-driven incident management platform built with .NET 10 and a microservices architecture.

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Architecture](https://img.shields.io/badge/Architecture-Microservices-FF6B6B)]()
[![Messaging](https://img.shields.io/badge/Messaging-RabbitMQ-FF6600?logo=rabbitmq&logoColor=white)](https://www.rabbitmq.com/)
[![Database](https://img.shields.io/badge/Database-PostgreSQL-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Search](https://img.shields.io/badge/Search-Elasticsearch-005571?logo=elasticsearch&logoColor=white)](https://www.elastic.co/)
[![AI](https://img.shields.io/badge/AI-Anthropic_Claude-D4A27F?logo=anthropic&logoColor=white)](https://www.anthropic.com/)
[![Agent Framework](https://img.shields.io/badge/Agents-Microsoft_Agent_Framework-512BD4?logo=microsoft&logoColor=white)](https://github.com/microsoft/agent-framework)

---

## 📖 Overview

**Intelligent Incident Management** is a backend platform that helps engineering teams detect, triage, and resolve operational incidents faster — with the help of AI.

When something goes wrong in production (a service degrades, an error rate spikes, a database connection pool drains), this platform captures the incident, uses AI to automatically assess its priority and probable root cause, routes it to the right team, and keeps everyone notified — all through a decoupled, event-driven architecture.

> **Status:** The AI feedback loop is live *and* now performs **root cause analysis**. An incident created via the API is automatically picked up by the AI agent, which forms a hypothesis, **searches the history of past incidents on its own initiative** (agentic tool-calling), and returns a calibrated analysis — suggested priority, category, evidence-based reasoning, concrete remediation steps, and a confidence score grounded in whether this failure has been seen before. All results are delivered reliably through a **Transactional Outbox**, with zero manual intervention. See the [Roadmap](#️-roadmap) below.

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
- **Smart Notifications** — Stakeholders are alerted automatically as incidents evolve. *(planned)*
- **Telemetry-Driven Detection** — Incidents are raised automatically from bursts in the customer's logs, pulled from Seq or pushed over OTLP by any OpenTelemetry Collector or SDK.

---

## 🏛️ Architecture

The platform is composed of independent microservices coordinated through an event bus, following a **choreography pattern** — services react to events without knowing who published them.

```
┌─────────────────────────────────────────────────────────────┐
│                       API Gateway (YARP)                     │
└─────────────────────────────────────────────────────────────┘
          │              │               │              │
          ▼              ▼               ▼              ▼
┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│   Incident   │ │ Notification │ │  Telemetry   │ │    Agent     │
│   Service    │ │   Service    │ │  Ingestion   │ │ Orchestrator │
│              │ │              │ │   Service    │ │    (AI) 🤖   │
└──────┬───▲───┘ └──────▲───────┘ └──────┬───────┘ └──────▲───┬───┘
       │   │            │                │                │   │
       │   └────────────┼────────────────┼────────────────┘   │
       │                │                │                    │
       │  ┌─────────────┴────────────────┴────────────────────┘
       │  │                                            ┌───────────────┐
       ▼  ▼                                            │ Elasticsearch │
┌─────────────────────────────────────────────────┐   │  (RCA search) │
│                RabbitMQ Event Bus                │   └───────▲───────┘
│  IncidentDetectedEvent ──▶ IncidentAnalyzedEvent │           │
│      (live, bidirectional AI feedback loop ✅)    │   ┌───────┴───────┐
└─────────────────────────────────────────────────┘   │ Outbox        │
                                                       │ Dispatcher    │
  Each service has its own isolated PostgreSQL DB.     │ (at-least-once)│
  AgentOrchestrator delivers side effects via the      └───────────────┘
  Transactional Outbox → RabbitMQ + Elasticsearch.
```

**The AI loop, live today:** `IncidentService` publishes `IncidentDetectedEvent` → `AgentOrchestrator` consumes it and calls Claude. The agent forms a root-cause hypothesis and **calls its `search_similar_incidents` tool on its own** to check the Elasticsearch corpus of past analyses. It persists the analysis, then — through a **Transactional Outbox** — atomically records its intent to (1) publish `IncidentAnalyzedEvent` to RabbitMQ and (2) index the analysis into Elasticsearch. A background dispatcher delivers both reliably. `IncidentService` consumes the event and updates the incident. No manual trigger, no central orchestrator — just services reacting to events.

### Services

| Service | Responsibility |
|---------|----------------|
| **IncidentService** | Core incident lifecycle — create, track, update status, assign teams. Consumes AI results and applies them to the incident. |
| **AgentOrchestrator** | The AI brain — analyzes incidents and suggests priority, category, reasoning, remediation steps, and a confidence score using Anthropic Claude via the Microsoft Agent Framework. Performs **agentic root cause analysis** by searching past incidents (Elasticsearch tool-calling) and delivers results reliably via a Transactional Outbox. |
| **NotificationService** | Sends notifications (email, webhook) as incidents are created and updated. *(planned)* |
| **TelemetryIngestionService** | Pulls logs from Seq or receives them over OTLP/HTTP (`/otlp/v1/logs`), folds bursts into signatures, and promotes anomalous ones to incidents. |

### Shared Building Blocks

| Block | Purpose |
|-------|---------|
| **SharedKernel** | Base domain primitives (`Entity`, `AggregateRoot`, `DomainEvent`, `ValueObject`). |
| **EventBus** | RabbitMQ abstraction for publishing and subscribing to integration events. |
| **Contracts** | Shared integration event definitions exchanged between services. |
| **Observability** | Structured logging (Serilog → Seq) and OpenTelemetry tracing (OTLP → Seq) for every service. |

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
- **Messaging:** RabbitMQ
- **Database:** PostgreSQL (isolated per service)
- **Search:** Elasticsearch 9.4 + Kibana (BM25 similarity search / RCA corpus)
- **AI:** Anthropic Claude API via [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) — including agentic tool-calling
- **Patterns:** Clean Architecture, CQRS, Domain-Driven Design, Event-Driven Architecture, Transactional Outbox
- **Libraries:** MediatR, FluentValidation, Entity Framework Core, Polly
- **Observability:** Seq (structured logs and distributed traces), OpenTelemetry
- **Infrastructure:** Docker & Docker Compose

---

## 🚀 Getting Started

> ⚠️ This project is under active development. Setup instructions will be expanded as the platform matures.

### Install with Docker

Everything — infrastructure, services, gateway and console — from one compose file. You need
[Docker](https://www.docker.com/) with Compose v2 and about 6 GB of memory for it.

**1. Fill in the settings — before the first start.** There are no default passwords; compose
refuses to start while a required value is empty.

```bash
git clone https://github.com/firatsakar/intelligent-incident-management.git
cd intelligent-incident-management
cp deploy/example.env deploy/.env
```

Open `deploy/.env` and fill in every value under *Required*, one secret per line:

```bash
openssl rand -hex 32
```

Add `ANTHROPIC_API_KEY` for the AI analysis (without it everything else works and each analysis
is marked failed). Every setting is explained in the file.

**2. Start it.** The first build takes several minutes.

```bash
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build
```

**3. Open the console** at http://localhost:8080 and complete the [first-run setup](#first-run-setup).
The setup code is in the identity service's log:

```bash
docker compose -f deploy/docker-compose.yml --env-file deploy/.env logs identity | grep "First-run setup"
```

| What | Where |
|------|-------|
| Console and API | `http://<server>:8080` — the only port open to the network |
| OTLP log ingest | `http://<server>:8080/otlp/v1/logs` |
| Seq (IIM's own logs and traces) | http://127.0.0.1:8081 — user `admin`; asks for a new password at first sign-in |
| pgAdmin | http://127.0.0.1:5050 |
| Kibana | http://127.0.0.1:5601 |

The admin interfaces answer on the server itself only. From another machine, tunnel to them:
`ssh -L 8081:127.0.0.1:8081 -L 5050:127.0.0.1:5050 -L 5601:127.0.0.1:5601 you@server`.

**HTTPS.** Put your own TLS proxy in front of port 8080 — for example Caddy:

```text
iim.example.com {
    reverse_proxy localhost:8080
}
```

Then set `IIM_PUBLIC_URL=https://iim.example.com` and tell IIM to believe the proxy about the
caller's address and scheme: `FORWARDED_KNOWN_PROXIES=172.16.0.0/12` for a proxy on the Docker
host (it reaches the published port from the Docker bridge). Over plain HTTP — trying it out on a
LAN address — everything works too, the session cookies are simply not marked Secure.

**Email.** Invitations and password resets are sent through the SMTP server in `SMTP_*` (your
company's, Google Workspace, Microsoft 365, SES, SendGrid…). Without one, each invitation link is
shown to the Admin to pass on. Incident notification emails are separate: each organisation enters
its own SMTP server in the console, under Settings → Integrations → Notifications.

**Updating.** `git pull`, then the same `up -d --build`. Each service applies its own database
migrations as it starts. `down` stops everything and keeps the data, which lives in Docker volumes.

### Development

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker & Docker Compose](https://www.docker.com/)

`dotnet test` runs the unit tests and `tests/Integration.Tests`, which start a throwaway Postgres
and RabbitMQ in Docker through Testcontainers — so Docker must be running. They check what only
the real thing can: every service's migrations against its model, organisation filters in SQL,
unique indexes, and a subscription made before the broker is up.

### Running the Infrastructure

```bash
# Start RabbitMQ, PostgreSQL instances, Elasticsearch, Kibana, Seq, and pgAdmin
docker-compose up -d
```

| Tool | URL |
|------|-----|
| RabbitMQ Management | http://localhost:15672 |
| pgAdmin | http://localhost:5050 |
| Seq (Logs) | http://localhost:8081 |
| Elasticsearch | http://localhost:9200 |
| Kibana | http://localhost:5601 |

### Running a Service

```bash
dotnet run --project src/Services/IncidentService/IncidentService.API
```

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
sender to it, then delete the old one. Alertmanager, Grafana and similar tools can send this JSON
from their own webhook templates; built-in adapters for their formats are not there yet.

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
- [ ] Comment & timeline (audit trail)
- [x] API Gateway (YARP) & JWT authentication, organisation-scoped data, roles
- [x] Invitation-based user management — Admins invite by email, change roles, deactivate accounts and issue password resets; organisation settings are Admin-only
- [x] Distributed tracing with OpenTelemetry — one trace from a pushed log line to the notification
- [x] First-run setup — an empty installation is claimed from the console with a one-time code from the server's log; no default credentials
- [x] Incident API — external systems open incidents with an organisation API key, deduplicated by their own external id
- [x] **MCP integration, GitHub first** — the analysis reads the failing service's recent commits over GitHub's MCP server (read-only, the organisation's own token) and names a suspected change, linked on the incident
- [ ] More MCP sources (Grafana, Kubernetes, PagerDuty) on the same client
- [ ] Unit & integration tests
- [ ] React frontend & analytics dashboard (MTTR, trends, model performance)
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
- **YAGNI, and no premature abstraction.** Shared code is extracted on the *second* real use, not on speculation. (This is why MCP — and a shared search building block — are scheduled for when a genuine second consumer appears, not now.)

---

## 📝 License

This project is currently developed for educational and portfolio purposes.

---

*Built with a focus on learning production-grade distributed systems architecture.*
