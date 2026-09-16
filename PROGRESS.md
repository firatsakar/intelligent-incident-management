# IIM — Roadmap & Progress

> **Roadmap pozisyonunun tek doğru kaynağı.** Claude Code her görevin başında bunu
> okur ve iş ilerledikçe günceller (bkz. `CLAUDE.md` task lifecycle). Konuşma
> Türkçe; yapısal etiketler (Adım/Parça) Jira ile birebir eşleşsin diye korunur.

## Durum lejantı
- `[x]` **Yapıldı** — bitti, build-doğrulandı (tarih + Jira key ekle)
- `[~]` **Devam ediyor** — üzerinde çalışılıyor (Jira key ekle)
- `[ ]` **Yapılacak** — planlı / sırada
- `[!]` **Yapılmadı** — atlandı veya ertelendi (tek satır gerekçe ekle)

## Son durum
- **Son tamamlanan:** Adım 10 — Root Cause Analysis (RCA)
- **Devam eden:** Adım 12 — NotificationService (`IIM-1`, 2026-09-16)
- **Sıradaki:** Adım 13 — TelemetryIngestionService

---

## Tamamlanan adımlar

- [x] **Adım 1** — Proje yapısı: BuildingBlocks (Contracts/EventBus/Observability/SharedKernel) + Services (AgentOrchestrator/IncidentService/NotificationService/TelemetryIngestionService)
- [x] **Adım 2** — BuildingBlocks içerikleri (SharedKernel: Entity/AggregateRoot+DomainEvents/DomainEvent/ValueObject; EventBus; Contracts: IncidentDetectedEvent; Observability)
- [x] **Adım 3** — RabbitMQ EventBus (EventBusOptions, RabbitMqConnection+Polly, RabbitMqEventBus)
- [x] **Adım 4** — Docker Compose (RabbitMQ, 4x PostgreSQL 5433-5436, Seq, pgAdmin; base/override/.env)
- [x] **Adım 5** — IncidentService Clean Architecture: Incident aggregate, CQRS, EF Core+Npgsql, migration + HTTP 201 + RabbitMQ publish doğrulandı
- [x] **Adım 6** — IncidentService CRUD tamamlama: List/pagination, UpdateStatus & AssignTeam, FluentValidation v12, ValidationBehavior, GlobalExceptionHandler (RFC 9457)
- [x] **Adım 7** — AgentOrchestrator + AI (çıplak Anthropic.SDK): IncidentAnalysis aggregate, jsonb mapping, event subscribe, geri-yazma döngüsü (IncidentAnalyzedEvent, ApplyAiAnalysis). Tam otonom uçtan uca döngü test edildi.
  - **Adım 8 / 9 / 11 bu adımın içinde tamamlandı** (IncidentDetectedEvent subscribe · AI priority/category · AI result write-back loop)
- [x] **Adım 7.5** — MAF refactor: Microsoft.Agents.AI 1.12.0 + Anthropic 1.12.0-preview, MafAiAnalyzer (AsAIAgent+RunAsync), DI tek satır geçiş, UsageDetails token counts. AnthropicAiAnalyzer yedek kaldı.
- [x] **Adım 10 — RCA (Parça 1 + A–H)** — Elasticsearch 9.4.3 + Kibana, Transactional Outbox (interceptor → atomik commit), OutboxDispatcher (at-least-once, dual-route RabbitMQ + ES), BM25 similarity search, MAF in-process tool-calling (search_similar_incidents) + kalibre edilmiş confidence. Uçtan uca doğrulandı.

---

## Sıradaki / kalan yol haritası (AI öne çekilmiş)

- [~] **Adım 12** — NotificationService (`IIM-1`) — AI analizi bitince email/webhook/Jira bildirimi, müşteri konfigüre edilebilir `Integration` tablosuyla. Kanallar: email + webhook + Jira; lokal SMTP = Mailpit; config REST CRUD ile yönetilir.
  - [x] **Parça 1** (`IIM-2`, 2026-09-16) — EventBus: queue-per-service + DLQ. Kuyruk adı `SubscriptionClientName`, event başına routing-key binding; tek consumer channel (SemaphoreSlim); hata halinde `requeue:false` + servis başına DLQ (eskiden sonsuz requeue). Broker topolojisi + iki yönlü mesaj akışı runtime doğrulandı.
  - [x] **Parça 2** (`IIM-3`, 2026-09-16) — `IncidentAnalyzedEvent`'e `IncidentTitle` + `Confidence` eklendi (OutboxDispatcher map'liyor); integration event Id'si artık outbox satırının Id'si → retry'da stabil, idempotency anahtarı olarak kullanılabilir
  - [x] **Parça 3** (`IIM-4`, 2026-09-16) — NotificationService.{Domain,Application,Infrastructure,API} oluşturuldu (IncidentService düzeninin aynısı), `.slnx`'e eklendi, `NotificationDb` (5434) bağlandı, API http 5210 / https 7110. Artık `Services.NotificationService.csproj` çözümden çıkarıldı (dosyalar diskte; IncidentService/AgentOrchestrator'da da aynısı yapılmış — `**/*.cs` glob'u alt projeleri derliyordu)
  - [ ] **Parça 4** (`IIM-5`) — `Integration` + `NotificationDelivery` aggregate'leri + EF migration
  - [ ] **Parça 5** (`IIM-6`) — Kanal soyutlaması + Email kanalı (MailKit + Mailpit)
  - [ ] **Parça 6** (`IIM-7`) — Webhook kanalı
  - [ ] **Parça 7** (`IIM-8`) — Jira kanalı
  - [ ] **Parça 8** (`IIM-9`) — Fan-out handler + Integration CRUD API
  - [ ] **Parça 9** (`IIM-10`) — Uçtan uca doğrulama + kapanış
- [ ] **Adım 13** — TelemetryIngestionService (anomali → otomatik incident tetikleme; MCP motivasyonunun doğduğu yer). — **SIRADA**
- [ ] **Adım 14** — Comment & Timeline (audit trail; event sourcing/Marten yeniden değerlendirilebilir)
- [ ] **Adım 15** — YARP API Gateway (tek giriş noktası)
- [ ] **Adım 16** — JWT Authentication + rol sistemi (Admin/Engineer/Viewer); MCP per-customer secret/auth önkoşulu
- [ ] **Adım 17** — OpenTelemetry distributed tracing; MCP debug + agentic akış görünürlüğü önkoşulu
- [ ] **Adım 17.5** — MCP entegrasyonu (Grafana/Kubernetes/GitHub/PagerDuty dış tool'ları). Kural: kendi verine in-process, başkasının verisine MCP. Önkoşul: 13 + 16 + 17. Erken opsiyon: Adım 12 sonrası salt-okunur GitHub MCP spike (ürüne girmez)
- [ ] **Adım 18** — Unit & Integration testler (xUnit)
- [ ] **Adım 19** — React frontend (Vite + TS; liste + detay)
- [ ] **Adım 20** — AI önerileri paneli + analytics dashboard (MTTR, trendler, model performansı)
- [ ] **Adım 21** — CI/CD (GitHub Actions)
- [ ] **Adım 22** — Kubernetes / production config

---

## Temizlik / tech-debt

- [x] `MafAiAnalyzer` debug `LogWarning("RAW AI RESPONSE")` kaldırıldı
- [x] `GET /api/analyses/similar` silindi (saf test amaçlıydı)
- [ ] `POST /api/analyses/reindex` — **bilinçli bırakıldı** (operasyonel: mapping değişimi / ES rebuild). Ürünleşmede gözden geçir.
- [ ] Outbox → BuildingBlocks'a çıkarma — **Adım 13'e ertelendi** (Adım 12 kararı: NotificationService terminal consumer, integration event publish etmiyor → gerçek ikinci kullanım yok, YAGNI. İkinci kullanım TelemetryIngestionService ile doğacak; processed-satır temizlik job'ı da o aşamada)
- [ ] Production secret migration (API key, DB/RabbitMQ/ES credentials) — User Secrets/.env'den Key Vault/Secrets Manager'a; Adım 16 ile
- [ ] ES production sertleştirme (xpack.security, TLS, auth; multi-node/replica) — Adım 16 / ölçek ile
- [ ] `AnthropicAiAnalyzer` drift (fallback, tool-calling'siz, gövdede kullanılmıyor) — düşük öncelik
- [ ] Seq'e log gönderimi yok — container Adım 4'ten beri ayakta ama hiçbir serviste Serilog/Seq sink'i yok, loglar sadece console. (2026-09-16, Adım 12 Parça 1 sırasında fark edildi)
- [ ] UML diyagramları (class / sequence / component) — çekirdek bitince
- [ ] pgvector / hybrid search (BM25 eş anlamlı kaçırınca) — ertelendi (YAGNI)

---

## Notlar
- Roadmap **AI öne çekilerek** yeniden sıralandı; yürütme sırası adım numaralarıyla birebir aynı değil.
- MCP kararı: Adım 10 Parça H'de in-process AIFunction seçildi; MCP tool *tüketmek* için (Adım 17.5), sunmak için değil.
