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
- **Telemetry-Driven Detection** — Incidents can be raised automatically from anomalous telemetry data. *(planned)*

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
| **TelemetryIngestionService** | Ingests metrics/alerts and automatically raises incidents on anomalies. *(planned)* |

### Shared Building Blocks

| Block | Purpose |
|-------|---------|
| **SharedKernel** | Base domain primitives (`Entity`, `AggregateRoot`, `DomainEvent`, `ValueObject`). |
| **EventBus** | RabbitMQ abstraction for publishing and subscribing to integration events. |
| **Contracts** | Shared integration event definitions exchanged between services. |
| **Observability** | Telemetry constants and tracing foundations. |

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
- **Observability:** Seq (structured logging), OpenTelemetry *(planned)*
- **Infrastructure:** Docker & Docker Compose

---

## 🚀 Getting Started

> ⚠️ This project is under active development. Setup instructions will be expanded as the platform matures.

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker & Docker Compose](https://www.docker.com/)

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

---

## 🗺️ Roadmap

- [x] Core infrastructure (Event Bus, Docker, Shared Kernel)
- [x] IncidentService — full CRUD with validation & error handling
- [x] AgentOrchestrator — AI-powered priority & category suggestion (Anthropic Claude)
- [x] End-to-end bidirectional event flow (incident created → AI analyzed → incident updated, fully autonomous)
- [x] AgentOrchestrator refactor onto the Microsoft Agent Framework
- [x] Root cause analysis (RCA) — Elasticsearch similarity search + Transactional Outbox + agentic tool-calling with suggested remediation steps & evidence-based confidence
- [ ] NotificationService — email/webhook alerts on incident lifecycle events
- [ ] TelemetryIngestionService — anomaly-based incident detection
- [ ] Comment & timeline (audit trail)
- [ ] API Gateway (YARP) & JWT authentication
- [ ] Distributed tracing with OpenTelemetry
- [ ] **MCP integration** — let the agent consume external systems (Grafana, Kubernetes, GitHub, PagerDuty) as tools, for cross-system root cause analysis
- [ ] Unit & integration tests
- [ ] React frontend & analytics dashboard (MTTR, trends, model performance)
- [ ] CI/CD & Kubernetes deployment

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
