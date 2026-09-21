# Sonraki Session — Devir Notu

> **2026-09-21 tarihli anlık görüntü.** Kalıcı doğruluk kaynakları `PROGRESS.md` (nerede
> olduğumuz) ve `production_necessaries.MD` (neyin eksik olduğu). Bu dosya sadece hızlı
> başlangıç içindir; ikisiyle çeliştiğinde **onlar geçerlidir**.

## Tek cümlelik durum

Adım 1–13 bitti; `develop` güncel ve push'lanmış (`3df6bac`), çalışma alanı temiz.
Sıradaki iş **Adım 18 — testler**.

---

## Nerede kaldık

Son iki adım bu session'da tamamlandı:

**Adım 12 — NotificationService** (`IIM-1`): AI analizi bitince email / webhook / Jira
bildirimi, müşteri konfigüre edilebilir `Integration` tablosuyla. Ön koşul olarak EventBus
queue-per-event-type'tan **queue-per-service**'e taşındı (yoksa iki servis aynı event'te
competing consumer olurdu).

**Adım 13 — TelemetryIngestionService** (`IIM-14`): dış log kaynağından çekme → imza → sinyal
→ **deterministik skorla** otomatik incident. Uçtan uca doğrulandı; AI analizi kanıt sayesinde
0.82 confidence'a çıktı ve gerekçesinde tekrar sayısını, süreyi ve oran anomalisini doğrudan
alıntıladı.

Ayrıca kapandı: Outbox → `BuildingBlocks.Outbox` (+ temizlik job'ı), `Incident.DetectedAt`,
dört servis → Seq logging, `IncidentDto`'nun AI alanlarını gizlemesi.

---

## Sıradaki iş: Adım 18 — testler

Öncelik sırası `PROGRESS.md`'nin "Son durum" bölümünde gerekçeleriyle duruyor. Özet:
**18 → 19 → (15+16) → (13.5+17)**. Adım 14'ün önceliği düşürüldü.

Adım 18'e başlarken **önce bunları test et** — hepsi bilinçli olarak test edilmemiş,
gerekçeleri `production_necessaries.MD` §7'de:

| Yol | Neden riskli |
|---|---|
| Zayıf sinyal bandı (0.60–0.89) | Hiç gözlenmedi; düz baseline tüm burst'leri 1.00'a itti |
| `LogFingerprint` regex fallback'i | Demo'daki her olay Serilog şablonu taşıyor, bu kol hiç çalışmadı |
| `SignalScoring` bileşenleri | Saf fonksiyon, test etmesi ucuz, yanlışsa sessizce yanlış |
| `ErrorSignature.CanAbsorbInto` | Dedup eskime kuralı — yanlışsa ya incident yağmuru ya sessizlik |
| `RateBaseline` kenar durumları | Düz baseline, 5 örnekten az, açık pencere hariç tutma |
| `NotificationDelivery` yarışı | Eşzamanlı aynı-event teslimi elle tetiklenemedi |

Test projesi yok; `tests/` klasörü `.slnx`'te tanımlı ama boş.

---

## Ortamı ayağa kaldırma

```bash
docker compose up -d                 # onay ister
```

Servisler (her biri ayrı terminalde, `--launch-profile http`):

| Servis | Port | Not |
|---|---|---|
| IncidentService.API | 5203 | |
| AgentOrchestrator.API | 5130 | **Anthropic API key gerekir** |
| NotificationService.API | 5210 | |
| TelemetryIngestionService.API | 5220 | |
| demo/MonitoredShop | 5300 | izlenen sistem taklidi + `/echo` |

Altyapı UI: RabbitMQ 15672 · Seq (bizim) 8081 · **seq-demo (izlenen sistem) 8082** ·
Mailpit 8025 · Kibana 5601 · pgAdmin 5050. Postgres 5433–5436.

**Bilinmesi gerekenler:**
- `docker-compose.override.yml` **gitignore'da** ve tüm port publish'leri orada. Yeni makinede
  yoksa stack çalışmaz (bu bizzat yaşandı).
- Anthropic key yoksa AI analizi `x-api-key header is required` ile düşer:
  ```bash
  dotnet user-secrets set "AiAnalyzer:ApiKey" "sk-ant-..." --project src/Services/AgentOrchestrator/AgentOrchestrator.API
  ```
- Docker Desktop kapalıysa hiçbir servis kalkmaz; container'lar oturumlar arasında düşebiliyor.
- `dotnet build` demo uygulaması çalışırken **başarısız olur** (exe kilitli) — önce durdur.

### Demo akışını tetikleme

```bash
# tek FATAL -> eşik beklemeden terfi eder
curl -X POST http://localhost:5300/chaos/fatal
# tek imzalı hata fırtınası (fingerprint'in çöktürmesini gösterir)
curl -X POST "http://localhost:5300/chaos/error-storm?count=30"
# ikinci, farklı imza
curl -X POST "http://localhost:5300/chaos/timeout-storm?count=15"
```

Sonra: `GET localhost:5220/api/telemetry/evidence?service=checkout-service` ·
`GET localhost:5220/api/telemetry/signals` · `GET localhost:5203/api/incidents`

---

## İhlal edilmemesi gereken kararlar

Bunlar tartışılıp karara bağlandı; değiştirmeden önce gerekçeyi oku.

- **Terfi yolunda AI yok.** Incident açma kapısı hızlı, ucuz ve sabah 3'te savunulabilir olmalı.
  AI'a giden tek şey, incident açıklamasına gömülü kompakt kanıt özeti — ek çağrı yok.
- **Signal ≠ Incident.** `≥0.90` incident · `0.60–0.89` zayıf sinyal · `<0.60` sadece kayıt.
  Eşik altı **çöpe atılmaz**, emsal ağırlığı onu öğrenmek için kullanır.
- **Telemetri bir entegrasyon.** Kaynak müşteri tarafından konfigüre edilir ve *çekilir*.
  Platform kendini değil, müşterinin sistemini izler — `seq` (bizim loglarımız) ile `seq-demo`
  (izlenen sistem) **asla karışmaz**.
- **Vendor başına connector yazma.** Uzun kuyruk için Adım 13.5 = OTLP; müşterinin zaten
  kullandığı shipper (OTel Collector / Fluent Bit / Vector) adaptasyonu yapar.
- **`DetectedAt` ≠ `CreatedAt`.** Sorunun başladığı an ile kaydın açıldığı an ayrı; kanıt
  penceresi `DetectedAt` etrafında kurulur.
- **Queue-per-service**, queue-per-event-type değil (`SubscriptionClientName`).
- **Outbox sırası:** önce yan etki, sonra `ProcessedOn` damgası. Ters çevirmek mesaj kaybettirir.
- **ES türetilmiş görünüm**: arama best-effort, ES düşse de sistem ayakta kalır.

---

## Bilinen tuzaklar

- **Jira MCP** bir kez tamamen yanıt vermez oldu (okuma dahil, 300s timeout). Bir süre sonra
  kendiliğinden düzeldi. Takılırsa yorum geçmiş ama transition geçmemiş olabilir — durumu
  `getJiraIssue` ile teyit et.
- **Jira bildirim entegrasyonu `IIM` projesini hedefliyor**, yani test koşuları görev takibi
  yaptığımız projeye issue açıyor (`IIM-12/13/25/26/27` böyle oluştu). Fırat "şimdilik sorun
  değil" dedi.
- PowerShell 5.1'de `HttpMethod::Patch` yok; PATCH için `New-Object System.Net.Http.HttpMethod('PATCH')`.
- `curl`/`wget` `deny` listesinde — HTTP çağrıları için PowerShell kullan.

## İzinler

Jira write (create/edit/transition/comment) ve `dotnet ef` **ön onaylı**.
`migrations remove` ve `docker compose up/down` hâlâ sorar; `database drop`, force push,
`reset --hard`, `rm -rf` **yasak**.

## Dosya haritası

`PROGRESS.md` yol haritası + tech-debt · `production_necessaries.MD` üretim engelleri ·
`CLAUDE.md` çalışma anlaşması · `src/BuildingBlocks/` 6 paylaşılan proje ·
`src/Services/` 4 servis × 4 katman · `demo/MonitoredShop` izlenen sistem taklidi.
