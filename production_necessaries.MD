# Production Gereksinimleri

> Adım 1–13 boyunca biriken, **ürün kullanımına geçmeden önce** ele alınması gereken her şey.
> `PROGRESS.md` nerede olduğumuzu söyler; bu dosya neyin eksik olduğunu söyler.
> Son güncelleme: 2026-09-21 (Adım 19 sonu).

## Nasıl okunmalı

| İşaret | Anlamı |
|---|---|
| 🔴 **Engel** | Bu haliyle üretime çıkılmaz. Veri sızıntısı, veri kaybı ya da hizmet durması riski. |
| 🟡 **Gerekli** | Üretimde kısa sürede canınızı yakar; ilk sprintlerde kapanmalı. |
| ⚪ **İyi olur** | Ölçek ya da olgunluk meselesi; bilinçli olarak ertelenebilir. |

---

## 1. Sırlar ve kimlik bilgileri

### 🔴 Bağlantı dizeleri ve parolalar `appsettings.json` içinde ve **git'te izleniyor**

Dört servisin `appsettings.json` dosyasında Postgres parolaları ve RabbitMQ kimlik bilgileri
düz metin duruyor ve bu dosyalar repoda. Geliştirme için kabul edilebilir, üretim için değil.

- Etkilenen: `IncidentService.API`, `AgentOrchestrator.API`, `NotificationService.API`,
  `TelemetryIngestionService.API`
- Yapılacak: bağlantı dizelerini ortam değişkeni / secret store'a taşı, repodaki değerleri
  yer tutucuya indir. Mevcut parolalar repo geçmişinde kaldığı için **rotasyon gerekir**.

### 🔴 Müşteri credential'ları veritabanında düz metin

`Integration.config` ve `TelemetrySource.config` (jsonb) müşterinin SMTP parolasını, Jira API
token'ını ve log sistemi anahtarını olduğu gibi saklıyor.

- Okuma yanıtlarında maskeleme **var** (`IntegrationDto`, `TelemetrySourceDto`), ama bu yalnızca
  API'yi korur; veritabanını, yedeği ve logu korumaz.
- Yapılacak: at-rest şifreleme (envelope encryption; anahtar Key Vault / KMS'te), ya da
  credential'ları hiç saklamayıp bir secret referansı tutmak.

### 🟡 Anthropic API key

- **Nereden alınır:** https://console.anthropic.com → Settings → API Keys.
- **Config anahtarı:** `AiAnalyzer:ApiKey`
- **Geliştirmede:**
  ```bash
  dotnet user-secrets set "AiAnalyzer:ApiKey" "sk-ant-..." --project src/Services/AgentOrchestrator/AgentOrchestrator.API
  ```
- **Üretimde:** secret store'dan ortam değişkeni olarak (`AiAnalyzer__ApiKey`). Asla repoya girmez.
- Kullanım maliyeti incident başına bir çağrıdır; kota ve bütçe alarmı kurulmalı.

---

## 2. Güvenlik

### 🔴 Hiçbir API'de kimlik doğrulama yok

Dört servisin tüm uçları kimliksiz erişime açık. Bu, incident verisini okumak, entegrasyon
oluşturmak ve silmek dahil her şeyi kapsar.

- **Adım 16** (JWT + rol sistemi) bu maddeyi kapatır ve yol haritasındaki en yüksek öncelikli
  güvenlik işidir.
- Ara önlem: servisleri dış ağa açmamak, yalnızca küme içinden erişilebilir tutmak.

### 🔴 Frontend'deki giriş ekranı **kozmetiktir — sıfır güvenlik sağlar**

Adım 19.5 Parça 5 (`IIM-51`, 2026-09-22) `/login` ekranını, oturum kapatmayı ve rota
korumasını ekledi. **Hiçbiri bir güvenlik sınırı değildir.** Oturum tarayıcıda uyduruluyor:
parola sorulmuyor, gönderilmiyor, doğrulanmıyor. Koruma yalnızca React router seviyesinde;
tarayıcıyı atlayıp servisleri doğrudan çağıran herkes her satıra ulaşır — yukarıdaki madde
aynen geçerli.

Bu bilinçli bir sıra kararıydı: ekranlar Adım 16'nın önüne alındı ki auth geldiğinde
değişmesi gereken tek şey `web/src/features/auth/session.ts` olsun. O dosyanın başındaki
yorum aynı uyarıyı taşıyor ve giriş kartının kendisi kullanıcıya da açıkça söylüyor
("No password, because nothing would check it").

- **Kapatan:** Adım 15 (gateway) + Adım 16 (JWT + organizasyon bazlı sorgular).
- **Yanlış anlaşılma riski:** bir ekran görüp "auth var" sanmak. Bu maddenin var olma sebebi.

### 🔴 `seq-demo` kimlik doğrulaması kapalı — üretime asla gitmemeli

`docker-compose.yml`'deki `seq-demo` servisi `SEQ_FIRSTRUN_NOAUTHENTICATION=true` ile çalışıyor.
Bu **kasıtlı** ve yalnızca demo içindir (izlenen müşteri sistemini taklit eder).
Üretim compose/manifest'inde bu servis hiç bulunmamalı.

### 🟡 Elasticsearch sertleştirilmemiş

`xpack.security.enabled=false`, tek düğüm, TLS yok, replika yok.
- Yapılacak: security aç, TLS, kimlik doğrulama, en az bir replika.
- Not: ES **türetilmiş görünüm**; düşerse arama bozulur ama sistem ayakta kalır (tasarım kararı).

### 🟡 `Microsoft.OpenApi` 2.0.0 güvenlik açığı

GHSA-v5pm-xwqc-g5wc (yüksek önem). `Microsoft.AspNetCore.OpenApi` 10.0.7 transitif olarak
çekiyor; **dört API projesinin dördü de** etkileniyor. Yamalı sürüme çıkılmalı.

### 🟡 CORS ve rate limiting yok

- **CORS hâlâ yok ve Adım 19'da bilinçli olarak eklenmedi.** Frontend Vite dev proxy'sinin
  ardında çalışıyor: tarayıcı yalnızca dev server ile konuşuyor, proxy sunucu tarafında
  çağırıyor, origin hiç değişmiyor. **Sonucu:** `dist/` başka bir origin'den servis edilirse
  uygulama çalışmaz. Üretim cevabı Adım 15 (YARP gateway) — gateway gelene kadar SPA yalnızca
  Vite dev/preview sunucusunun ardında ayakta.
- Rate limiting: kimliksiz uçlar kapandıktan sonra bile, `POST /api/incidents` ve test uçları
  kötüye kullanıma açık.

### ⚪ Dev-only uçlar

- `POST /api/dev/webhook-echo` (NotificationService) — `IsDevelopment()` dışında 404 döner.
  Korumalı, ama üretim imajında bulunmaması daha temiz.
- `POST /api/dev/seed-demo-incidents` (IncidentService, Adım 20 Parça 3) — geriye dönük tarihli
  demo incident'ları basar. `IsDevelopment()` dışında 404, idempotent, satırlar açıklamada
  `[demo-seed]` işareti taşır. **Gerçek veritabanına yazar**; üretim imajında bulunmamalı ve
  silme karşılığı bilinçli olarak yok.
- `POST /api/analyses/reindex` (AgentOrchestrator) — **bilinçli bırakıldı**, operasyonel
  (mapping değişimi / ES yeniden inşası). Yetkilendirme geldiğinde admin rolüne bağlanmalı.

---

## 3. Veri yönetimi ve saklama

### 🔴 `log_records` sınırsız büyüyor

Telemetri her poll'da Error/Fatal kayıtları yazıyor ve **hiçbir temizlik yok**. Gerçek bir
müşteri hacminde tablo kısa sürede yönetilemez hale gelir ve pencere sorguları yavaşlar.

- Yapılacak: retention job (öneri: 7 gün) — `OutboxCleanupService` deseninin aynısı.
- Ölçek cevabı: zaman-bazlı partition ya da ES'e taşımak. Karar Adım 13.5 notunda:
  *her satır saklanmaz, imza başına sayım + örnek satırlar saklanır.*

### 🟡 Yedekleme ve geri yükleme prosedürü yok

Dört ayrı Postgres veritabanı var (incident / notification / telemetry / agent). Yedek planı,
saklama süresi ve **geri yükleme tatbikatı** tanımlı değil.

### ⚪ ES index lifecycle

`incident-analyses` index'i için ILM politikası yok.

---

## 3.5 Ürün döngüsündeki kopukluk

### 🔴 Çözülen incident telemetriye geri bildirilmiyor

`ErrorSignature.DetachIncident` **kod tabanında hiç çağrılmıyor** (Adım 19 sırasında bulundu).
Bir incident çözüldüğünde ya da kapatıldığında imza `CurrentIncidentId`'yi bırakmıyor ve
`ConfirmedRealCount` / `FalsePositiveCount` hiç artmıyor.

İki sonucu var:

- **`SignalScoring`'deki precedent bonusu (+0.15) ve false-positive cezası (−0.25) pratikte
  ölü terimler.** "Sistem kullanıldıkça öğrenir" hikâyesi bağlı ama besleyen yok.
- **Dedup yalnızca 24 saatlik TTL ile açılıyor, çözümle değil.** Kapatılmış bir incident bir
  gün boyunca yeni burst'leri kendine emmeye devam ediyor.

Adım 18 testleri geçiyor çünkü metot doğru — sadece kimse çağırmıyor. Kapatmak için
IncidentService'ten çıkan bir `IncidentResolvedEvent` ve TelemetryIngestionService tarafında
bir tüketici gerekiyor; ayrı bir adım olarak ele alınmalı.

---

## 4. Güvenilirlik ve çalışma zamanı

### 🔴 Health check ucu yok

Hiçbir servis `/health` ya da `/ready` sunmuyor. Kubernetes liveness/readiness probe'ları
(Adım 22) bunsuz kurulamaz; load balancer sağlıksız bir örneğe trafik göndermeye devam eder.

- Yapılacak: `AddHealthChecks()` + Postgres, RabbitMQ, ES bağımlılık kontrolleri.

### 🔴 Ölçeklendiğinde çift çalışma

`TelemetryPollingService` ve `OutboxDispatcher` tek örnek varsayımıyla yazıldı. İki replika
çalışırsa ikisi de aynı kaynağı poll eder ve aynı outbox satırlarını dağıtır.

- Kopya **veri** oluşmaz (`(TelemetrySourceId, SourceEventId)` unique index ve idempotent
  tüketiciler korur), ama kopya **yayın** ve gereksiz yük oluşur.
- Yapılacak: `SELECT ... FOR UPDATE SKIP LOCKED` ile satır kiralama, ya da dağıtık kilit.

### 🟡 Dead-letter kuyruklarını kimse izlemiyor

Servis başına DLQ var (`incident-service.dlq` vb.) ve zehirli mesajlar oraya düşüyor — ama
kimse bakmıyor. Sessizce biriken bir DLQ, sessizce kaybolan mesaj demektir.

- Yapılacak: DLQ derinliği üzerine alarm; tercihen platformun kendi bildirim kanallarını kullan.

### 🟡 Başarısız bildirimler için retry yok

`NotificationDelivery` satırı `Failed` olarak kaydediliyor ve orada kalıyor. Geçici bir SMTP
arızası bildirimi kalıcı olarak kaybettirir.

- Yapılacak: başarısız delivery'ler için sınırlı yeniden deneme job'ı.

### ⚪ Graceful shutdown

Hosted service'ler `stoppingToken`'a saygı duyuyor, ama kapanışta devam eden bir poll ya da
dispatch turunun tamamlanması garanti değil.

---

## 5. Gözlemlenebilirlik

### 🟡 Distributed tracing yok

Dört servis ve bir mesaj kuyruğu var; bir incident'ın hangi adımda takıldığını anlamak şu an
log'ları elle eşleştirmekle mümkün. **Adım 17** (OpenTelemetry) bunu kapatır.

### 🟡 Metrik toplanmıyor

`TelemetryConstants.MetricNames` tanımlı ama **hiçbir yerde kullanılmıyor** — `incidents.created`,
`notifications.sent`, `ai.request_duration_seconds` hepsi ölü tanım.

- Yapılacak: Adım 17 ile birlikte gerçekten yayınla; MTTR ve model performansı (Adım 20)
  bunlara dayanacak.

### 🟡 Platformun kendisi izlenmiyor

Servisler Seq'e yazıyor (Adım 13 Parça 3b) ama kimse o logları izlemiyor. Platform kendi
hatalarında sessiz kalır.

- İlginç seçenek: IIM'i kendi Seq'ine bağlamak. Mimari buna hazır — ama demo hikâyesini
  bulandırmamak için bilinçli olarak yapılmadı.

---

## 6. Dağıtım

### 🔴 Servisler için Dockerfile yok

Compose yalnızca altyapıyı (Postgres, RabbitMQ, ES, Seq, Mailpit) ayağa kaldırıyor. Dört .NET
servisi geliştirme makinesinde `dotnet run` ile çalışıyor. **Dağıtılabilir bir artefakt yok.**

### 🔴 Migration stratejisi tanımsız

Şu an migration'lar elle uygulanıyor (`dotnet ef database update`). Üretimde bir karar gerekir:
açılışta otomatik migrate (basit, ama çoklu replikada yarış), init container / ayrı job
(önerilen), ya da elle kontrollü.

### 🟡 `docker-compose.override.yml` gitignore'da ve repoda yok

Tüm port publish'leri bu dosyada. Yeni bir makinede stack **çalışmaz** — Adım 13'te bu bizzat
yaşandı, dosya kayıptı ve yeniden kurmak gerekti.

- Yapılacak: `docker-compose.override.example.yml` repoya eklensin, README'de anlatılsın.

### ⚪ CI/CD ve Kubernetes

Adım 21 ve Adım 22. Build + test + imaj yayınlama hattı yok.

---

## 7. Test

### 🟡 Unit testler var, integration testi yok

**Adım 18** (`IIM-28`, 2026-09-21) dört unit test projesi ekledi — **203 test**, xUnit +
NSubstitute + düz `Assert`. Önceki "🔴 hiç otomatik test yok" maddesi kapandı.

Kapsanan: `SignalScoring`, `RateBaseline`, `LogFingerprint`, `ErrorSignature`, `LogRecord`,
`DetectionRule`, `EvidenceSummary`, `DetectSignalsCommandHandler` (karar ağacının her dalı),
`Integration.Matches`, `DispatchNotificationsCommandHandler`, `Incident`,
`CreateIncidentFromSignalCommandHandler`, `OutboxDispatcher`.

Süitin gerçekten yakaladığı doğrulandı: `OutboxDispatcher`'ın sırası bilerek ters çevrildi, üç
test düştü, sonra geri alındı.

#### Kapatılan bilinen boşluklar

| Yol | Durum |
|---|---|
| Zayıf sinyal bandı (0.60–0.89) | ✅ Kapsandı — aşağıdaki nota bakın |
| Regex normalizasyon fallback'i | ✅ Kapsandı (maskeleme sırası dahil: GUID > uzun hex > sayı) |
| Clock skew işaretlemesi | ✅ Kapsandı |
| `SignalScoring` bileşenleri | ✅ Kapsandı (clamp iki uçta, breakdown toplamı = total) |
| `ErrorSignature.CanAbsorbInto` | ✅ Kapsandı (beş dal + pencerenin son olaydan ölçülmesi) |
| `RateBaseline` kenar durumları | ✅ Kapsandı |

**Zayıf band hakkında bilinmesi gereken:** unit testler bandın *mantığının* doğru olduğunu
kanıtlıyor, ama band **geçmişi olmayan bir imza için erişilemez**. Baseline 12 tam pencereden
kuruluyor; geçmişsiz imza 12 *sıfır* bucket'a düşüyor → düz baseline → sınırlı 4.0 → anomali
eşiği geçiliyor. Aynı 6 hatalık burst normal baseline'a karşı 0.65 (zayıf), boş baseline'a karşı
0.90 (terfi). Tasarım gereği doğru, ama eşik ayarlamadan önce bilinmeli. İki yarı da testle sabit.

#### Hâlâ açık

| Yol | Neden |
|---|---|
| `NotificationDelivery` yarış durumu | Gerçek Postgres unique index'i gerekiyor → integration testi |
| EF mapping / jsonb / migration doğrulaması | Aynı sebep |
| Servisler arası uçtan uca akış | Aynı sebep (RabbitMQ + 4 DB) |
| **Frontend'in tamamı** | Adım 19'a test alınmadı — 6 ekran + SignalR zaten genişti |

**Frontend testi yok** (Adım 19, 2026-09-21). `web/` altında tek bir test yok: ısı haritası
aggregate'i (`heatmap.ts` — saf fonksiyon, test etmesi ucuz), maskeleme form mantığı
(`buildConfig` — yanlışsa credential yok eder), realtime cache yazma haritası. Ekranlar
tarayıcıda elle doğrulandı ve üç kusur böyle bulundu, ama regresyon koruması sıfır.
Vitest + Testing Library ile kapatılmalı.

Integration testleri (Testcontainers) Adım 18 kapsamı dışında bırakıldı — bilinçli karar,
2026-09-21. Ayrı bir adım olarak ele alınmalı.

---

## 8. Ölçek notları (bilinçli ertelenenler)

- `LogRecord`, `AggregateRoot`'tan türüyor → her satır kullanılmayan `CreatedAt`/`UpdatedAt`/
  domain-event listesi taşıyor. Demo hacminde zararsız.
- Oran baseline'ı zaman damgalarını belleğe çekip orada bucket'lıyor. Birkaç pencerelik veri
  için sorunsuz; gerçek hacimde bucket'lama SQL'e itilmeli.
- BM25 eş anlamlıları kaçırır; pgvector / hybrid search ertelendi (YAGNI).
- `AnthropicAiAnalyzer` sapma halinde (fallback, tool-calling'siz, gövdede kullanılmıyor).

---

## 9. Ürünleştirme öncesi operasyonel notlar

**Telemetri kaynağı bağlama:** `POST /api/telemetry-sources` — `kind: Seq`, `config.Url` zorunlu,
`ApiKey` kimlik doğrulaması açık kaynaklarda gerekli. `POST {id}/test` ayarları kaydetmeden
doğrular. Şu an tek connector Seq; Elasticsearch/Loki/Datadog için Adım 13.5'teki OTLP kararına
bakılmalı — vendor başına connector yazmak yerine tek standart format kabul edilecek.

**Bildirim entegrasyonu:** `POST /api/integrations` — email (`Host`, `Port`, `From`, `To`),
webhook (`Url`, opsiyonel `Header:*`), Jira (`BaseUrl`, `ProjectKey`, `Email`, `ApiToken`).
`POST {id}/test` aynı şekilde doğrular.

**Tespit eşiğini ayarlama:** `detection_rules` tablosu — `WindowSeconds`, `Threshold`,
`DedupWindowHours`, `PromoteThreshold`. Varsayılan boş tabloya bir kez seed'lenir ve bir daha
yazılmaz, yani ayarlanmış eşik yeniden başlatmada korunur. **Şu an REST ucu yok** — doğrudan SQL
gerekiyor; ürünleşmede CRUD eklenmeli.

**Hiçbir şey tespit edilmiyorsa bakılacak yerler:** kaynak `IsEnabled` mi · `source_cursors`
tablosunda `LastError` · `detection_rules` boş mu · imza `IsMuted` mi · sinyal var ama terfi yok
ise `signals.score_breakdown` kararı açıklar.
