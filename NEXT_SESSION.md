# Sonraki Session — Devir Notu

> **2026-09-24 tarihli anlık görüntü.** Kalıcı doğruluk kaynakları `PROGRESS.md` (nerede
> olduğumuz) ve `production_necessaries.MD` (neyin eksik olduğu). Bu dosya sadece hızlı
> başlangıç içindir; ikisiyle çeliştiğinde **onlar geçerlidir**.

## Tek cümlelik durum

Adım 1–13, 15, 16, 18, 19, 19.5, 20, 20.5 ve 20.7 bitti; `develop` güncel ve push'lanmış
(`088eca3`), çalışma alanı temiz, **341 test yeşil**. Sıradaki iş **Adım 13.5 + 17 birlikte**
(OTLP log ingest + OpenTelemetry tracing).

---

## Nerede kaldık

**Adım 15 — YARP API Gateway** (`IIM-93`): tarayıcı artık tek origin'le (`:5100`) konuşuyor.
Rota tablosu `vite.config.ts`'ten `src/Gateway/Gateway.API/GatewayRoutes.cs`'e taşındı. CORS
eklenmedi — gereksiz kılındı. Gateway üretimde `dist/`'i de servis edebiliyor (`Spa:Root`).

**Adım 16 — JWT auth, roller, organizasyon bazlı sahiplik** (`IIM-94`, 13 parça):
- Beşinci servis **IdentityService** (`:5240`, `postgres_identity` `:5437`).
- Oturum iki **httpOnly + SameSite=Strict** cookie: 15 dk access JWT + 14 gün rotasyonlu
  refresh. Gateway access cookie'sini `Authorization` header'ına çeviriyor; beş servisin her biri
  token'ı kendi doğruluyor. Token'lar hiçbir zaman response gövdesinde değil.
- Organizasyon `IOrganizationContext` ile akıyor; **tek yazarı var**: HTTP'de claim middleware'i,
  mesajda bus, polling döngüsünde kaynağın satırı. `IntegrationEvent.OrganizationId` `required`.
- 10 entity + Elasticsearch araması + 3 SignalR hub organizasyona bağlı. Başka org'un satırı
  **404** (403 değil). Viewer okur, her yazmada 403.
- Fırat tarayıcıda doğru parolayla giriş yaptı — adımın insan gerektiren tek doğrulaması tamam.

---

## Sıradaki iş: Adım 13.5 + 17

İkisi aynı OpenTelemetry bağımlılığını paylaşıyor. `PROGRESS.md`'deki maddeler:
- **13.5** — OTLP log ingest: vendor başına connector yazmak yerine tek standart tel formatı.
  Her log satırı saklanmaz; imza başına sayım + örnek satırlar. Filtreleme kaynağa itilir.
- **17** — distributed tracing; MCP debug ve agentic akış görünürlüğünün önkoşulu.

**Yeni ingest yolu org'a bağlı olmak zorunda.** Bugünkü tek kök `TelemetrySource.OrganizationId`
ve polling döngüsü kapsamı oradan alıyor. OTLP push ile gelecek; push'un hangi organizasyona ait
olduğunu *istek* söylemeli (kaynak başına bir anahtar gibi), yoksa Adım 16'nın kapattığı sızıntı
yeni kapıdan geri girer.

---

## Ortamı ayağa kaldırma

```bash
docker compose up -d                 # onay ister
```

Servisler — **hepsi `--launch-profile http` ile**, yoksa ortam Production olur ve user secrets
(imza anahtarı dahil) **hiç yüklenmez**; servis açılışta `Jwt:SigningKey is not configured` diye
durur:

| Süreç | Port | Not |
|---|---|---|
| Gateway.API | **5100** | tarayıcının konuştuğu tek yer; `--no-launch-profile` ile de çalışır (secret okumaz) |
| IdentityService.API | 5240 | seed admin + imza anahtarı user secrets'ta |
| IncidentService.API | 5203 | |
| AgentOrchestrator.API | 5130 | **Anthropic API key gerekir** |
| NotificationService.API | 5210 | |
| TelemetryIngestionService.API | 5220 | |
| web (Vite) | 5173 | `.claude/launch.json` → `web`; `/api` ve `/hubs`'ı gateway'e yönlendirir |
| demo/MonitoredShop | 5300 | izlenen sistem taklidi |

Altyapı UI: RabbitMQ 15672 · Seq (bizim) 8081 · **seq-demo (izlenen sistem) 8082** ·
Mailpit 8025 · Kibana 5601 · pgAdmin 5050. Postgres 5433–5437.

**Bilinmesi gerekenler:**
- `docker-compose.override.yml` **gitignore'da** ve tüm port publish'leri orada.
- **`Jwt:SigningKey` beş projenin user secrets'ında aynı değerle duruyor.** Yeni makinede
  IdentityService'e set edip diğer dördüne kopyalamak gerekiyor.
- **`dotnet build` çalışan servisler varken başarısız olur** (DLL kilidi, MSB3027). Paylaşılan bir
  BuildingBlocks projesi değiştiyse önce **bütün** servisleri durdur, derle, sonra başlat.
  Birden çok `dotnet run`'ı aynı anda başlatmak da BuildingBlocks'u yarıştırır — önce derle, sonra
  `--no-build`.
- Postgres `POSTGRES_USER`'ı yalnızca veri dizini boşken oluşturur: `.env`'de bir DB kullanıcısını
  yeniden adlandırmak **volume'u da silmeyi** gerektirir, yoksa `28P01` yanlış parola gibi görünür.
- Docker konteynerleri oturumlar arasında (makine uykusu vb.) duruyor; API 500 verirse önce
  `docker ps`.
- Anthropic key yoksa AI analizi `x-api-key header is required` ile düşer:
  ```bash
  dotnet user-secrets set "AiAnalyzer:ApiKey" "sk-ant-..." --project src/Services/AgentOrchestrator/AgentOrchestrator.API
  ```

### Test için kanarya organizasyon

`11111111-1111-1111-1111-111111111111` her depoda **bilerek** duruyor: 152 demo olay + 1 probe
olayı, 2 imza (biri Acme'nin parmak iziyle aynı), 1 webhook entegrasyonu (hedefi `:5399`, kimse
dinlemiyor), 1 tespit kuralı, ES'te 1 belge. Acme'ye görünmüyor; iki-org izolasyonunun kanıtı.
Silmek Fırat'ın onayını ister.

**Yan etkisiz test için olayları kanarya org adına aç.** Acme adına açılan her olay analiz edilip
Acme'nin **filtresiz Jira entegrasyonuna** gider ve `IIM` projesinde gerçek bir issue açar.
Parolasız test token'ı, imza anahtarıyla `org`/`role`/`sub` claim'leri taşıyan bir JWT basarak
üretilebilir (scratchpad'deki probe'lar böyle çalıştı).

---

## İhlal edilmemesi gereken kararlar

Bunlar tartışılıp karara bağlandı; değiştirmeden önce gerekçeyi oku.

- **Terfi yolunda AI yok.** Incident açma kapısı hızlı, ucuz ve sabah 3'te savunulabilir olmalı.
- **Signal ≠ Incident.** `≥0.90` incident · `0.60–0.89` zayıf sinyal · `<0.60` sadece kayıt.
- **Telemetri bir entegrasyon.** Kaynak müşteri tarafından konfigüre edilir; platform kendini
  değil müşterinin sistemini izler — `seq` ile `seq-demo` **asla karışmaz**.
- **Vendor başına connector yazma.** Uzun kuyruk için Adım 13.5 = OTLP.
- **`DetectedAt` ≠ `CreatedAt`.**
- **Queue-per-service**, queue-per-event-type değil (`SubscriptionClientName`).
- **Outbox sırası:** önce yan etki, sonra `ProcessedOn` damgası.
- **ES türetilmiş görünüm**: arama best-effort, ES düşse de sistem ayakta kalır.
- **Organizasyon sınırı** (Adım 16):
  - Organizasyon kapsamını **handler set etmez**; kapsamı açan set eder (middleware, bus,
    poller).
  - Query filter'lar **tek tek** yazılır, reflection'la süpürülmez.
  - DbContext organizasyonu **sorgu anında** okur, kurulumda yakalamaz.
  - Sınırı geçen okuma yalnızca adlandırılmış metotla yapılır (`…ForPollingAsync`,
    `…ForReindexAsync`).
  - **Unique index'ler org'a göredir** — tablo geneli bir unique, ikinci organizasyonu kırar
    (üçü böyle yakalandı).
  - Hub yayını asla `Clients.All` değildir.
- **Token asla response gövdesinde değil**; oturum yalnızca httpOnly cookie.

---

## Bilinen tuzaklar

- **Birim testleri veritabanı kısıtlarını görmez** — repository'ler mock'lu. Adım 16'nın üç
  index hatası yalnızca canlı koşuda çıktı. Şema değişikliğinden sonra canlı doğrula; Adım 23
  (Testcontainers) bunu kalıcı kapatacak.
- Windows PowerShell 5.1'in `CookieContainer`'ı, path'i istek path'inin öneki olmayan bir
  Set-Cookie'yi atıyor (refresh cookie'si `/api/auth/refresh`'e kapsanmış) ve `Invoke-WebRequest`
  `-Headers`'taki `Cookie`'yi sessizce yok sayıyor. Auth'u test ederken `HttpClient` +
  `UseCookies = false` kullan.
- **Jira MCP** bir kez tamamen yanıt vermez oldu; takılırsa durumu `getJiraIssue` ile teyit et.
- `curl`/`wget` `deny` listesinde — HTTP çağrıları için PowerShell kullan.
- Bir parolayı tarayıcı formuna Claude giremez; giriş yapılmış ekranları görmek Fırat'ın bir kez
  giriş yapmasını gerektirir.

## İzinler

Jira write (create/edit/transition/comment) ve `dotnet ef` **ön onaylı**.
`migrations remove` ve `docker compose up/down` hâlâ sorar; `database drop`, force push,
`reset --hard`, `rm -rf` **yasak**. `.env` ve `appsettings.*.json` okunmaz/yazılmaz.

## Dosya haritası

`PROGRESS.md` yol haritası + tech-debt · `production_necessaries.MD` üretim engelleri ·
`CLAUDE.md` çalışma anlaşması · `src/Gateway/` YARP · `src/BuildingBlocks/` 7 paylaşılan proje
(`Web` = auth, org hub, cookie adları) · `src/Services/` 5 servis × 4 katman · `web/` konsol ·
`demo/MonitoredShop` izlenen sistem taklidi.
