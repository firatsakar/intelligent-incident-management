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
- **Son tamamlanan:** Adım 18 — Unit testler (`IIM-28`, 2026-09-21) — 203 test, 4 proje
- **Devam eden:** Adım 19 — React frontend (`IIM-35`)

### Öncelik sırası (2026-09-21'de kararlaştırıldı)

Yol haritası **adım numarası sırasına göre değil**, aşağıdaki sıraya göre yürüyecek:

1. ~~**Adım 18 — testler.**~~ **Yapıldı** (`IIM-28`). Regresyon koruması artık var.
   Integration testleri (Testcontainers) bilinçli olarak kapsam dışı bırakıldı → ayrı adım.
2. **Adım 19 — React frontend.** Sistem şu an yalnızca log/psql/curl üzerinden görünüyor;
   yapılan her şey gerçek ama görünmez. API yüzeyi buna hazır.
3. **Adım 15 + 16 birlikte** — gateway auth'tan ayrı yapılırsa auth iki kere yazılır; JWT
   doğrulama, rate limiting ve CORS'un doğal yeri gateway. 16 olmadan credential şifreleme de
   yarım kalır.
4. **Adım 13.5 + 17 birlikte** — ikisi de OpenTelemetry bağımlılığını paylaşıyor.
5. Sonra: 17.5 (MCP), 20 (AI paneli + analytics), 21 (CI/CD), 22 (Kubernetes).

**Önceliği düşürüldü:** Adım 14 (Comment & Timeline) — evidence API'si ve delivery geçmişi
audit hikâyesinin çoğunu zaten veriyor.

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
- [x] **Adım 12** (`IIM-1`, 2026-09-16) — NotificationService: AI analizi bitince email/webhook/Jira bildirimi, müşteri konfigüre edilebilir `Integration` tablosuyla. Kanallar: email + webhook + Jira; lokal SMTP = Mailpit; config REST CRUD ile yönetilir. Uçtan uca doğrulandı.
  - [x] **Parça 1** (`IIM-2`, 2026-09-16) — EventBus: queue-per-service + DLQ. Kuyruk adı `SubscriptionClientName`, event başına routing-key binding; tek consumer channel (SemaphoreSlim); hata halinde `requeue:false` + servis başına DLQ (eskiden sonsuz requeue). Broker topolojisi + iki yönlü mesaj akışı runtime doğrulandı.
  - [x] **Parça 2** (`IIM-3`, 2026-09-16) — `IncidentAnalyzedEvent`'e `IncidentTitle` + `Confidence` eklendi (OutboxDispatcher map'liyor); integration event Id'si artık outbox satırının Id'si → retry'da stabil, idempotency anahtarı olarak kullanılabilir
  - [x] **Parça 3** (`IIM-4`, 2026-09-16) — NotificationService.{Domain,Application,Infrastructure,API} oluşturuldu (IncidentService düzeninin aynısı), `.slnx`'e eklendi, `NotificationDb` (5434) bağlandı, API http 5210 / https 7110. Artık `Services.NotificationService.csproj` çözümden çıkarıldı (dosyalar diskte; IncidentService/AgentOrchestrator'da da aynısı yapılmış — `**/*.cs` glob'u alt projeleri derliyordu)
  - [x] **Parça 4** (`IIM-5`, 2026-09-16) — `Integration` (channel + IsEnabled + jsonb config + MinPriority/CategoryFilter + `Matches()`) ve `NotificationDelivery` (unique `(IntegrationId, IncidentId)` = idempotency anahtarı) eklendi, `InitialCreate` migration uygulandı. `Skipped` statüsü düşürüldü (idempotency anahtarını işgal ediyordu); `IncidentPriority` yerel kopya (cross-domain referans yasak)
  - [x] **Parça 5** (`IIM-6`, 2026-09-16) — `INotificationChannel` (keyed DI, `NotificationChannelType` anahtarı) + `EmailNotificationChannel` (MailKit 4.18.0, ayarlar `Integration.Config`'ten), Mailpit compose'a eklendi (SMTP 1025 / UI 8025). Mailpit'e gerçek mail düştü. **Kapsam notu:** `POST /api/integrations/{id}/test` ve MediatR wiring Parça 8'den öne çekildi — her kanalı tek tek doğrulayabilmek için
  - [x] **Parça 6** (`IIM-7`, 2026-09-16) — `WebhookNotificationChannel` (named HttpClient + Polly retry; sadece 5xx/408/429/transport retry'lanır, 4xx final). Ayarlar: `Url`, `TimeoutSeconds`, `Header:<ad>` önekli serbest header'lar. Dev-only `POST /api/dev/webhook-echo` doğrulama hedefi. Başarı ve hata yolu ayrı ayrı doğrulandı
  - [x] **Parça 7** (`IIM-8`, 2026-09-16) — `JiraNotificationChannel` (v3 `POST /rest/api/3/issue`, Basic auth, ADF description). Gerçek Jira site'ına karşı doğrulandı: `IIM-12` (test ucu) ve `IIM-13` (uçtan uca koşu) oluştu, ADF paragrafları doğru render oldu
  - [x] **Parça 8a** (`IIM-11`, 2026-09-16) — `ValidationBehavior` → yeni `BuildingBlocks.Application`; `GlobalExceptionHandler` → yeni `BuildingBlocks.Web`; SharedKernel'e `NotFoundException` tabanı (handler eskiden `IncidentNotFoundException`'a bağlıydı, paylaşılamıyordu). NotificationService gerçek ikinci tüketici = YAGNI tetikleyicisi. IncidentService davranışı birebir aynı (400+errors / 404 doğrulandı)
  - [x] **Parça 8** (`IIM-9`, 2026-09-16) — `IncidentAnalyzedEvent` aboneliği + `DispatchNotificationsCommand` fan-out (filtre → idempotency → keyed kanal → `NotificationDelivery`), kanal başına hata izolasyonu, Integration CRUD (+ kanal başına zorunlu ayar validasyonu) ve `GET /api/notifications/incident/{id}` audit ucu. Credential görünümlü config değerleri okumada maskeleniyor. Tek event iki servise birden ulaştı, filtre ve idempotency doğrulandı
  - [x] **Parça 9** (`IIM-10`, 2026-09-16) — Uçtan uca doğrulandı: incident → gerçek AI analizi (Medium→Critical, kategori Application, confidence %82) → Outbox → RabbitMQ → hem IncidentService write-back hem NotificationService fan-out; 3 kanalın üçü de `Sent` (Mailpit maili, webhook echo, Jira `IIM-13`). 3 servis kuyruğu + 3 DLQ, DLQ'lar boş
- [x] **Adım 13** (`IIM-14`, 2026-09-21) — TelemetryIngestionService: dış log kaynağından çekme → imza → sinyal → **deterministik skorla** otomatik incident. Uçtan uca doğrulandı; AI analizi kanıt sayesinde 0.82 confidence'a çıktı ve gerekçesinde tekrar sayısını, süreyi ve oran anomalisini doğrudan alıntıladı. Telemetri bir *entegrasyon*: kaynak müşteri tarafından konfigüre edilir (Adım 12'deki `Integration` deseni), ilk connector Seq. Skorlamada AI yok; AI'a giden tek şey incident açıklamasına gömülen kompakt kanıt özeti.
  - **Signal ≠ Incident:** `≥0.90` incident · `0.60–0.89` zayıf sinyal (incident yok) · `<0.60` sadece kayıt (baseline + emsal beslenir)
  - **Dedup eskimesi:** açık incident var **ve** `now − LastSeenAt ≤ DedupWindow` (24s) → sayaç artar; TTL dolmuş ya da incident kapanmışsa **yeni** incident, öncekine bağlı
  - [x] **Parça 1** (`IIM-15`, 2026-09-17) — İskelet + 6 tablo + `InitialCreate` uygulandı. `LogRecord`'da `Timestamp` (kaynak saati) ≠ `IngestedAt`, clock-skew flag'i; `SourceCursor` ayrı tabloda ve geri sarmıyor; `ErrorSignature.CanAbsorbInto()` eskime kuralını tek yerde tutuyor; zaman sütunlarında BRIN index
  - [x] **Parça 2** (`IIM-16`, 2026-09-17) — `TelemetrySource` CRUD (jsonb config + kind başına zorunlu ayar validasyonu + credential maskeleme + `POST {id}/test`), `ITelemetrySourceConnector` keyed DI, Seq connector: `clef=true` ile NDJSON CLEF parse, `afterId` cursor'ı, boş batch'te pozisyon korunur. `ApiKey` opsiyonel (kimliksiz Seq'e de bağlanır); eksikse test ucu net hata veriyor
  - [x] **Parça 3a** (`IIM-24`, 2026-09-21) — `demo/MonitoredShop` (checkout servisi taklidi: sürekli trafik + `error-storm` / `timeout-storm` / `fatal` tetikleyicileri) + `seq-demo` (8082, `SEQ_FIRSTRUN_NOAUTHENTICATION` ile anonim okuma). Tespit verisi buradan gelir. **Kapsam düzeltmesi:** platform kendini değil, müşterinin sistemini izler. Canlı Seq'e karşı doğrulama Parça 2'de **iki gerçek bug** ortaya çıkardı: CLEF `@i` benzersiz id değil *event tipi hash'i* (bir fırtına tek satıra çökerdi), ve `afterId` ileri değil **geriye** sayfalıyor (tail cursor `fromDateUtc` olmalı)
  - [x] **Parça 3b** (`IIM-17`, 2026-09-21) — `BuildingBlocks.Observability.UsePlatformLogging()` ile 4 servis Serilog → `seq` (8081). `Service` damgası `TelemetryConstants.ServiceNames`'ten. Adım 12'de bulunan "Seq'e kimse yazmıyor" boşluğu kapandı; *tespit kaynağı değil*, sadece bizim gözlemlenebilirliğimiz. Ayrım korunuyor: servisler `seq`'e yazar, dedektör `seq-demo`'yu okur
  - [x] **Parça 4** (`IIM-18`, 2026-09-21) — Poller (hosted service sadece *ne zaman*a karar verir, iş `PollTelemetrySourceCommand`'da) + normalizasyon + fingerprint + `ErrorSignature` upsert. **Fingerprint mesaj şablonunu kullanıyor** — şablon yapısı gereği zaten normalize (değişkenler adlandırılmış yer tutucu), regex sadece şablonsuz kaynaklar için fallback. Doğrulandı: 110 kayıt → **2 imza** (80 + 30), 110 farklı event id (kopya yok), `Timestamp` ≠ `IngestedAt`
  - [x] **Parça 5** (`IIM-19`, 2026-09-21) — `DetectionRule` değerlendirmesi → `Signal` (kind=`LogBurst`) + imza başına z-score baseline (12 tam pencere; açık pencere hariç, düz baseline sınırlı skor, 5 örnekten az ise skor yok). Pencere başına tek sinyal (poll başına değil). Varsayılan kural boş tabloya bir kez seed'leniyor. Doğrulandı: 2 hata (eşik 3) → sinyal yok; 8 → tek sinyal; ikinci imza bağımsız sinyal
  - [x] **Parça 6** (`IIM-20`, 2026-09-21) — Outbox → `BuildingBlocks.Outbox`. İki dikiş yeri paylaşılabilir kıldı: `IOutboxStore` (satırlar hangi DB'de) ve `IOutboxMessageHandler` (mesaj ne anlama geliyor — eskiden dispatcher içindeki `switch`). Dispatch sırası korundu (önce yan etki, sonra `ProcessedOn`). `OutboxCleanupService` eklendi. Migration gerekmedi (EF: model değişmemiş); Adım 12 zinciri aynen çalışıyor
  - [x] **Parça 7** (`IIM-21`, 2026-09-21) — Deterministik skorlama (bileşen dökümü signal'de saklanıyor, AI yok) + TTL'li dedup + terfi + kanıt özeti → outbox → `SignalPromotedEvent`. IncidentId **telemetri tarafında** üretiliyor: at-least-once teslimde ikinci kopya, ikinci incident değil duplicate key olur. Test sırasında bug yakalandı: FATAL kısayolu eşik kontrolünden sonra geliyordu, tek crash eleniyordu — düzeltildi. Doğrulandı: FATAL tek seferde terfi, ikincisi açık incident'a yazıldı, üçüncüsü dedup dalından geçti, farklı imza bağımsız terfi etti
  - [x] **Parça 8** (`IIM-22`, 2026-09-21) — `SignalPromotedEvent` tüketimi → `Source=Telemetry` incident + `Incident.DetectedAt` (manuel oluşturmada da opsiyonel olarak var). Redelivery no-op (id promoter tarafından seçiliyor). `IncidentDto`'daki üç elle map tek `FromDomain`'e indi ve AI alanları artık dışarı veriliyor. Doğrulandı: `detectedAt 07:09:46` vs `createdAt 07:11:51`
  - [x] **Parça 9** (`IIM-23`, 2026-09-21) — `GET /api/telemetry/evidence` (insan + Adım 19 frontend'i için; AI tool'u **değil**) ve `GET /api/telemetry/signals` (varsayılan: zayıf band). Beş servisle tam uçtan uca koşu doğrulandı
- [x] **Adım 18** (`IIM-28`, 2026-09-21) — Unit testler (xUnit + NSubstitute + düz `Assert`). Yığın kararı 2026-09-21: FluentAssertions v8 ticari lisans istiyor, Moq'un SponsorLink geçmişi var — ikisi de alınmadı. **Integration testleri (Testcontainers) bu adımın dışında bırakıldı**; sonucu olarak `NotificationDelivery` yarış durumu ve EF mapping/jsonb/migration doğrulaması test edilmemiş kalıyor
  - [x] **Parça 1** (`IIM-29`, 2026-09-21) — Test iskeleti: `tests/` altında 4 proje (3 servis + BuildingBlocks), `.slnx` kaydı, xUnit 2.9.3 + NSubstitute 5.3.0. `dotnet test` yeşil (8 test). Her projede boş iskelet yerine gerçek bir assertion var ve bunlar bilinçli olarak sonraki parçaların tekrar etmeyeceği şeyler: tespit sabitleri + severity sırası, keyed DI'ın çözdüğü kanal enum'u, iki servisteki priority kopyalarının isim uyumu, outbox satırının pending varsayılanları
  - [x] **Parça 2** (`IIM-30`, 2026-09-21) — `SignalScoring` + `RateBaseline` (64 test). **Zayıf band (0.60–0.89) ilk kez doğrudan kapsandı**: eşiğin iki katı, korroborasyonsuz burst tam 0.65 → zayıf; aynı burst rate anomalisiyle tam 0.90 → terfi. Bandın tabanı sabit 0.60, tavanı kuralın — yani `PromoteThreshold` düşürülünce band kayar değil *daralır*. Ayrıca: fatal kısayolu diğer tüm terimleri atlıyor, doubling cap'i, sıfır eşik bölme koruması, z-score 2.0 dahil, clamp iki uçta, breakdown bileşenleri toplamı = kaydedilen total. `RateBaseline`: <5 örnek `null` (0 değil), düz baseline sınırlı 4.0, gürültülü baseline içindeki sayım anomali değil, açık pencere dışlanıyor, **boş zaman damgası listesi sıfırlanmış bucket döndürüyor** (boş liste dönseydi minimum örnek sayısının altına düşüp anomali bonusunu sessizce bastırırdı)
  - [x] **Parça 3** (`IIM-31`, 2026-09-21) — Telemetri domain (131 test). **`LogFingerprint` regex fallback'i ilk kez kapsandı**: maskeleme sırası kritik (GUID > uzun hex > sayı — sayı önce koşsaydı GUID `#-#-#-#-#` olurdu ve her müşterinin hatası ayrı imza görünürdü). **`ErrorSignature.CanAbsorbInto`** beş dal + pencerenin *son olaydan* ölçüldüğü (terfiden değil). **`LogRecord` clock skew** ilk kez tetiklendi. Ayrıca `DetectionRule`, `EvidenceSummary`. Not: fingerprint alan ayırıcısı `U+001F` — düz metinde görünmüyor, bir reformat sessizce silebilir; iki test tam olarak bunu yakalamak için var
  - [x] **Parça 4** (`IIM-32`, 2026-09-21) — `DetectSignalsCommandHandler` karar ağacının 19 dalı (150 test). **Bulgu: zayıf band, geçmişi olmayan imza için erişilemez.** Baseline 12 tam pencereden kuruluyor; geçmişi olmayan imza 12 *sıfır* bucket'a düşüyor → düz baseline → sınırlı 4.0 → anomali eşiğini geçiyor. Yani ilk burst'te anomali bonusu her zaman veriliyor: aynı 6 hatalık burst normal baseline'a karşı **0.65** (zayıf), boş baseline'a karşı **0.90** (terfi). Tasarım gereği doğru — bir haftadır sessiz imzanın ilk burst'ü gerçekten dikkate değer — ama eşik ayarlamadan önce bilinmesi gerekiyor. İki yarı da artık testle sabit
  - [x] **Parça 5** (`IIM-33`, 2026-09-21) — `Integration.Matches` filtre matrisi + `DispatchNotificationsCommandHandler` (27 test). Ağırlık hata yollarında: kanal patlarsa `Failed` yazılıp diğerleri gönderiliyor, kanal *çözülemezse* de aynı yere düşüyor, idempotency `(integration, incident)` bazında — kısmi hatadan sonra redelivery satırı olmayan entegrasyona ulaşıyor. `SaveChanges` hatasının yutulması bilinçli: bildirimler zaten gitti, rethrow DLQ'ya düşürüp redelivery'de hepsini tekrar gönderirdi — test tam olarak bunun "düzeltilmesini" engellemek için var
  - [x] **Parça 6** (`IIM-34`, 2026-09-21) — `Incident` aggregate (verilen id, `DetectedAt` ≠ `CreatedAt`, `ApplyAiAnalysis` idempotent ve statü/takımı bozmuyor), `CreateIncidentFromSignalCommandHandler` redelivery no-op + kaydetme *sonra* duyurma sırası, `OutboxDispatcher` (26 test). **Süitin gerçekten yakaladığı doğrulandı:** `OutboxDispatcher`'ın sırası bilerek ters çevrildi → üç test düştü → geri alındı

---

## Sıradaki / kalan yol haritası (AI öne çekilmiş)

- [ ] **Adım 13.5** — OTLP log ingest (müşteri log entegrasyonunun **genel çözümü**). Vendor başına connector yazmak yerine tek bir standart tel formatı kabul edilir; uzun kuyruğu müşterinin zaten kullandığı shipper (OTel Collector / Fluent Bit / Vector) çözer. Adım 17 ile aynı bağımlılık → birlikte ele alınabilir. Karar notu: her log satırı saklanmaz, imza başına sayım + örnek satırlar saklanır; filtreleme kaynağa (Collector) itilir
- [ ] **Adım 13.6** — Generic alert webhook ingest (Datadog monitor, Grafana alert, CloudWatch alarm). En düşük hacim, en yüksek sinyal; ham log çekmek istemeyen müşteriler için. Provider başına küçük bir payload mapper yeter
- [ ] **Adım 14** — Comment & Timeline (audit trail; event sourcing/Marten yeniden değerlendirilebilir)
- [ ] **Adım 15** — YARP API Gateway (tek giriş noktası)
- [ ] **Adım 16** — JWT Authentication + rol sistemi (Admin/Engineer/Viewer); MCP per-customer secret/auth önkoşulu
- [ ] **Adım 17** — OpenTelemetry distributed tracing; MCP debug + agentic akış görünürlüğü önkoşulu
- [ ] **Adım 17.5** — MCP entegrasyonu (Grafana/Kubernetes/GitHub/PagerDuty dış tool'ları). Kural: kendi verine in-process, başkasının verisine MCP. Önkoşul: 13 + 16 + 17. Erken opsiyon: Adım 12 sonrası salt-okunur GitHub MCP spike (ürüne girmez)
- [~] **Adım 19** (`IIM-35`) — React frontend (Vite + TS + Tailwind/shadcn + SignalR). Kararlar 2026-09-21: **tam yüzey, 6 ekran** · UI **Tailwind + shadcn/ui** · canlılık **SignalR (gerçek push, payload taşır)** · backend eklemesi `Confidence` + `SignalDto` zenginleştirmesi. **CORS alınmadı** → SPA yalnızca Vite proxy'sinin (`ws: true`) ardında çalışır; üretim cevabı Adım 15 gateway. State: sunucu state'i TanStack Query, filtreler URL'de, geçici UI state'i yerel — **global store yok**
  - [x] **Parça 1** (`IIM-36`, 2026-09-21) — Backend veri boşlukları (215 test). **`Confidence`** artık event → command → aggregate → `IncidentDto.AiConfidence` zincirinde akıyor; migration tek nullable kolon. `ApplyAiAnalysis` parametreyi **varsayılansız** alıyor — varsayılan, ileride bir çağrı yerinin alanı sessizce düşürmesine izin verirdi ki alan zaten tam olarak böyle kaybolmuştu. İmza değişikliği **dört çağrı yerini kırdı, dördü de testte, üretim kodunda sıfır** (Adım 18 ağının tam olarak yapmak için kurulduğu şey). **`SignalDto`** imzanın servis/exception/mesajını taşıyor (satır başına değil, batch başına tek okuma); imzası kaybolmuş sinyal etiketsiz ama listede kalıyor. **Pencere filtresi**: `from`/`to` verilince span **tüm statülerle** dönüyor — önce statüye filtrelemek, terfi edenle etmeyen arasındaki kontrastı gizlerdi ki span'a bakmanın tek sebebi odur
  - [x] **Parça 2** (`IIM-37`, 2026-09-21) — SignalR hub'ları: `/hubs/incidents` · `/hubs/notifications` · `/hubs/signals` (225 test). `IRealtimeNotifier` Application'da, `IHubContext` gerçeklemesi API'de — Application katmanı SignalR'ı bilmiyor. **Mesajlar HTTP ile aynı DTO'yu taşıyor**; tek istisna `incidentCreated` (yalnızca id — yeni satırın filtrelenmiş listenin 1. sayfasında durup durmadığı sunucunun sorusu). İki kural: **yayın kayıttan sonra** (yayınlanan, saklanan olmalı) ve **yayın hatası loglanır, fırlatılmaz** (yazma zaten commit'lendi; fırlatmak başarılı komutu 500'e çevirir ya da yan etkileri çoktan olmuş mesajı DLQ'ya düşürür). Dispatcher `SaveChanges` patlarsa kaydı yayınlamıyor. **Runtime doğrulandı:** üç hub da http profilinde WebSockets ile negotiate ediyor (`UseHttpsRedirection` engellemiyor, "Failed to determine the https port" uyarısı zararsız); gerçek bir PATCH singleton notifier'ı çözdü ve hatasız yayınladı
  - [x] **Parça 3** (`IIM-38`, 2026-09-21) — Frontend iskelet: `web/` (kökte, `.slnx`'te değil), Vite + React 19 + TS, Tailwind v4 + shadcn/ui (15 bileşen kaynak olarak kopyalandı), TanStack Query, React Router, `@microsoft/signalr`. **Vite proxy** yük taşıyan kısım: CORS yok, tarayıcı yalnızca dev server ile konuşuyor; hub rotalarında `ws: true` (olmadan WebSocket upgrade proxy'lenmez ve SignalR sessizce long polling'e düşer — çalışır ve hatayı gizler). Tipler elle yazıldı (codegen build'i çalışan servislere bağlardı). **Doğrulandı:** üç servis ayakken beş rota + hub negotiate proxy üzerinden 200. İlk probe iki servis kapalıyken 502 döndü — yanlış port da aynı görünürdü, o yüzden ikinci probe yapıldı
  - [~] **Parça 4** (`IIM-39`) — Incident listesi + detay (zaman çizelgesi, tespit gecikmesi, AI paneli)
  - [ ] **Parça 5** (`IIM-40`) — Telemetri yüzeyi: **ısı haritası** (servis × imza, log ölçekli renk, güven bandı işareti) + sinyal kuyruğu + kanıt görüntüleyici + "neden uyandırıldık" paneli
  - [ ] **Parça 6** (`IIM-41`) — Ayarlar: entegrasyonlar + telemetri kaynakları (maskeli credential ezme tuzağı dahil)
  - [ ] **Parça 7** (`IIM-42`) — Realtime client (cache yazma, reconnect invalidation) + uçtan uca doğrulama
- [ ] **Adım 20** — AI önerileri paneli + analytics dashboard (MTTR, trendler, model performansı)
- [ ] **Adım 21** — CI/CD (GitHub Actions)
- [ ] **Adım 22** — Kubernetes / production config
- [ ] **Adım 23** — Integration testleri (Testcontainers + gerçek Postgres). Adım 18'de bilinçli olarak kapsam dışı bırakıldı. Kapatacağı boşluklar: `NotificationDelivery` eşzamanlı teslim yarışı (gerçek unique index gerekiyor), EF mapping / jsonb / migration doğrulaması, servisler arası uçtan uca akış. Docker Desktop gerektirir

---

## Temizlik / tech-debt

- [x] `MafAiAnalyzer` debug `LogWarning("RAW AI RESPONSE")` kaldırıldı
- [x] `GET /api/analyses/similar` silindi (saf test amaçlıydı)
- [ ] `POST /api/analyses/reindex` — **bilinçli bırakıldı** (operasyonel: mapping değişimi / ES rebuild). Ürünleşmede gözden geçir.
- [x] Outbox → BuildingBlocks'a çıkarma — **yapıldı** (Adım 13 Parça 6, `IIM-20`, 2026-09-21); processed-satır temizlik job'ı da dahil. Önceki not: **Adım 13'e ertelenmişti** (Adım 12 kararı: NotificationService terminal consumer, integration event publish etmiyor → gerçek ikinci kullanım yok, YAGNI. İkinci kullanım TelemetryIngestionService ile doğacak; processed-satır temizlik job'ı da o aşamada)
- [x] `IncidentDto` AI alanlarını dışarı vermiyor — **kapandı** (Adım 13 Parça 8, `IIM-22`, 2026-09-21); üç elle map tek factory'ye indirildi. Önceki not: `IncidentDto` AI alanlarını dışarı vermiyordu — `AiSuggestedCategory` / `AiReasoning` / `IsAiAnalyzed` DB'de dolu ama `GET /api/incidents/{id}` yanıtında yok. Adım 19 frontend'i ve Adım 20 AI paneli için gerekli. (2026-09-16, Adım 12 Parça 9 sırasında fark edildi)
- [x] **`production_necessaries.MD` oluşturuldu** (2026-09-21) — Adım 1–13'ten biriken tüm üretim gereksinimleri, 🔴 engel / 🟡 gerekli / ⚪ iyi olur diye sınıflanmış. Aşağıdaki maddelerin çoğu artık orada da detaylı duruyor
- [x] Ölü şablon projeleri temizlendi (2026-09-21) — kök `Program.cs`/`appsettings.json`/`.csproj`/`Properties` + 4 servisin `Services.*.csproj` artıkları (çözümde değildi, derlenmiyordu). `DevController` (webhook echo) ürün servisinden çıkarılıp `demo/MonitoredShop`'a taşındı
- [ ] `README.md`'ye "API key nereden alınır" notu — production'da Anthropic API key'in hangi hesaptan/konsoldan alınacağı, hangi ortam değişkeni/secret store'a konacağı (dev'de `dotnet user-secrets`, `AiAnalyzer:ApiKey`). Fırat'ın isteği, 2026-09-16
- [ ] `Integration.config` (jsonb) içindeki müşteri credential'ları (SMTP parolası, Jira API token'ı) düz metin — at-rest şifreleme gerekiyor; Adım 16 secret migration ile
- [ ] Production secret migration (API key, DB/RabbitMQ/ES credentials) — User Secrets/.env'den Key Vault/Secrets Manager'a; Adım 16 ile
- [ ] ES production sertleştirme (xpack.security, TLS, auth; multi-node/replica) — Adım 16 / ölçek ile
- [ ] `AnthropicAiAnalyzer` drift (fallback, tool-calling'siz, gövdede kullanılmıyor) — düşük öncelik
- [ ] `Microsoft.OpenApi` 2.0.0 yüksek önem dereceli güvenlik açığı (GHSA-v5pm-xwqc-g5wc) — `Microsoft.AspNetCore.OpenApi` 10.0.7 transitif olarak çekiyor; IncidentService.API + NotificationService.API etkileniyor. Yamalı sürüme çıkılmalı
- [x] Seq'e log gönderimi yok — **kapandı** (Adım 13 Parça 3b, `IIM-17`, 2026-09-21)
- [x] Otomatik test yok — **kapandı** (Adım 18, `IIM-28`, 2026-09-21); 203 unit test. Integration testleri Adım 23'e ayrıldı
- [ ] UML diyagramları (class / sequence / component) — çekirdek bitince
- [ ] pgvector / hybrid search (BM25 eş anlamlı kaçırınca) — ertelendi (YAGNI)

---

## Notlar
- Roadmap **AI öne çekilerek** yeniden sıralandı; yürütme sırası adım numaralarıyla birebir aynı değil.
- MCP kararı: Adım 10 Parça H'de in-process AIFunction seçildi; MCP tool *tüketmek* için (Adım 17.5), sunmak için değil.
