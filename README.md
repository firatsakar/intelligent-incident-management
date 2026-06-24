# 🚨 Intelligent Incident Management

> An AI-assisted, event-driven incident management platform built with .NET 10 and a microservices architecture.

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Architecture](https://img.shields.io/badge/Architecture-Microservices-FF6B6B)]()
[![Messaging](https://img.shields.io/badge/Messaging-RabbitMQ-FF6600?logo=rabbitmq&logoColor=white)](https://www.rabbitmq.com/)
[![Database](https://img.shields.io/badge/Database-PostgreSQL-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![AI](https://img.shields.io/badge/AI-Anthropic_Claude-D4A27F?logo=anthropic&logoColor=white)](https://www.anthropic.com/)
[![Agent Framework](https://img.shields.io/badge/Agents-Microsoft_Agent_Framework-512BD4?logo=microsoft&logoColor=white)](https://github.com/microsoft/agent-framework)

---

## 📖 Overview

**Intelligent Incident Management** is a backend platform that helps engineering teams detect, triage, and resolve operational incidents faster — with the help of AI.

When something goes wrong in production (a service degrades, an error rate spikes, a database connection pool drains), this platform captures the incident, uses AI to automatically assess its priority and probable root cause, routes it to the right team, and keeps everyone notified — all through a decoupled, event-driven architecture.

This project is built as a deep, hands-on exploration of **production-grade distributed systems design** with modern .NET.

---

## ✨ Key Features

- **AI-Powered Triage** — Automatic incident prioritization, categorization, and root cause analysis powered by Anthropic's Claude, orchestrated through the Microsoft Agent Framework.
- **Event-Driven Architecture** — Services communicate asynchronously via RabbitMQ, fully decoupled from one another.
- **Per-Service Database Isolation** — Each microservice owns its data, following true microservice principles.
- **Clean Architecture** — Every service follows a strict layered design (Domain → Application → Infrastructure → API).
- **CQRS** — Commands and queries are cleanly separated using MediatR.
- **Smart Notifications** — Stakeholders are alerted automatically as incidents evolve.
- **Telemetry-Driven Detection** — Incidents can be raised automatically from anomalous telemetry data.

---

## 🏛️ Architecture

The platform is composed of independent microservices coordinated through an event bus.

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
└──────┬───────┘ └──────▲───────┘ └──────┬───────┘ └──────▲───────┘
       │                │                │                │
       │  ┌─────────────┴────────────────┴────────────────┘
       │  │
       ▼  ▼
┌─────────────────────────────────────────────────────────────┐
│                    RabbitMQ Event Bus                        │
│         (Integration Events: IncidentDetected, etc.)         │
└─────────────────────────────────────────────────────────────┘

  Each service has its own isolated PostgreSQL database.
```

### Services

| Service | Responsibility |
|---------|----------------|
| **IncidentService** | Core incident lifecycle — create, track, update status, assign teams. |
| **AgentOrchestrator** | The AI brain — analyzes incidents, suggests priority, and performs root cause analysis using the Microsoft Agent Framework with Anthropic Claude. |
| **NotificationService** | Sends notifications (email, webhook) as incidents are created and updated. |
| **TelemetryIngestionService** | Ingests metrics/alerts and automatically raises incidents on anomalies. |

### Shared Building Blocks

| Block | Purpose |
|-------|---------|
| **SharedKernel** | Base domain primitives (`Entity`, `AggregateRoot`, `DomainEvent`, `ValueObject`). |
| **EventBus** | RabbitMQ abstraction for publishing and subscribing to integration events. |
| **Contracts** | Shared integration event definitions exchanged between services. |
| **Observability** | Telemetry constants and tracing foundations. |

---

## 🛠️ Tech Stack

- **Runtime:** .NET 10 / ASP.NET Core
- **Messaging:** RabbitMQ
- **Database:** PostgreSQL (isolated per service)
- **AI:** Anthropic Claude API via [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) (1.0 GA)
- **Patterns:** Clean Architecture, CQRS, Domain-Driven Design, Event-Driven Architecture
- **Libraries:** MediatR, FluentValidation, Entity Framework Core, Polly
- **Observability:** Seq (structured logging), OpenTelemetry
- **Infrastructure:** Docker & Docker Compose

---

## 🚀 Getting Started

> ⚠️ This project is under active development. Setup instructions will be expanded as the platform matures.

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker & Docker Compose](https://www.docker.com/)

### Running the Infrastructure

```bash
# Start RabbitMQ, PostgreSQL instances, Seq, and pgAdmin
docker-compose up -d
```

| Tool | URL |
|------|-----|
| RabbitMQ Management | http://localhost:15672 |
| pgAdmin | http://localhost:5050 |
| Seq (Logs) | http://localhost:8081 |

### Running a Service

```bash
dotnet run --project src/Services/IncidentService/IncidentService.API
```

---

## 🗺️ Roadmap

- [x] Core infrastructure (Event Bus, Docker, Shared Kernel)
- [x] IncidentService — full CRUD with validation & error handling
- [ ] AgentOrchestrator — AI-powered triage & root cause analysis (Microsoft Agent Framework)
- [ ] End-to-end event flow (incident → AI → notification)
- [ ] TelemetryIngestionService — anomaly-based incident detection
- [ ] API Gateway & JWT authentication
- [ ] Distributed tracing with OpenTelemetry
- [ ] React frontend & analytics dashboard
- [ ] CI/CD & Kubernetes deployment

---

## 📐 Design Principles

This project deliberately favors **clarity and correctness** over shortcuts:

- **Each service owns its data.** No shared databases, no hidden coupling.
- **Publishers don't know their subscribers.** Services emit events; whoever cares, listens.
- **The domain is protected.** Business rules live in the domain layer, shielded from infrastructure concerns.
- **Cross-cutting concerns are centralized.** Validation and error handling are handled via pipelines and middleware, not scattered across handlers.

---

## 📝 License

This project is currently developed for educational and portfolio purposes.

---

*Built with a focus on learning production-grade distributed systems architecture.*
