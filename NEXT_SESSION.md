# Sonraki Session — Devir Notu

> **2026-09-26 tarihli anlık görüntü.** Kalıcı doğruluk kaynakları `PROGRESS.md` (nerede
> olduğumuz) ve `production_necessaries.MD` (neyin eksik olduğu). Bu dosya sadece hızlı
> başlangıç içindir; ikisiyle çeliştiğinde **onlar geçerlidir**.

## Tek cümlelik durum

Adım 1–13, **13.5**, 15, 16, **16.5**, **17**, **17.5**, 18, 19, 19.5, 20, 20.5, 20.7, **24**,
**25**, **26** ve **27** bitti; `develop` güncel ve push'lanmış, **558 test yeşil**, prob **93/93**.
Ürün **açık kaynak**: 21 (CI/CD) ve 22 (Kubernetes) çıktı, yerine tek komutla kurulum geldi.
**`master` geride:** son sürüm noktası PR #20; Adım 26 ve 27 yalnız `develop`'ta — Fırat GitHub'da
`deploy/`'u göremedi çünkü varsayılan dal `master`. `develop` → `master` (PR ile) Fırat'ın kararı.
Kalanlar (23, 20.6, 13.6, Adım 20 kalıntısı, 14, outbox retry tavanı 🔴, event bus aboneliğinin
sessizce ölmesi 🟡) Fırat'la konuşulacak — sırayı sen önerme, sor. Kapsamı belirsiz bir adımda 24 ve 17.5'teki gibi yap:
önce ne işe yaradığını anlat, kararları sor, sonra plan.

---

## Nerede kaldık

**Adım 27 — servis üzerinden olay açma** (`IIM-155`). Konsolda olay formu **yok** (Fırat). Admin
Ayarlar → Entegrasyonlar → Gözlemlenebilirlik → **Olay API'si**'nden adlandırılmış anahtar üretir
(`iim_inc_…`, bir kez gösterilir, hash saklanır). `POST /api/incidents/intake` + `X-IIM-Api-Key`:
201 yeni / 200 aynı `externalId` ile açık olay / 401 / 429 (anahtar başına 60/dk). Olay `Source =
Alert`, `ReportedBy` = anahtarın adı; AI analizi ve bildirimler her olaydaki gibi. Açık olaylarda
`(OrganizationId, ExternalId)` kısmi tekil index yarışı tutuyor. Prob her koşuda `isolation-probe`
anahtarını üretip siliyor; sabit `externalId` sayesinde kanaryada tek bir açık olay kalıyor.
**Dikkat:** `web/src/lib/i18n/*.ts` düzenlemesi Vite HMR'ını bozabiliyor ("useLanguage must be used
inside LanguageProvider") — dev sunucuyu yeniden başlat.

**Adım 26 — tek komutla kurulum** (`IIM-150`). `cp deploy/example.env deploy/.env` → zorunlu
sırları doldur (boşken compose başlamaz) → `docker compose -f deploy/docker-compose.yml --env-file
deploy/.env up -d --build` → `http://localhost:8080` kurulum ekranı. Tek Postgres (5 veritabanı,
5 sahip, `deploy/postgres/init.sh`), dışarıya yalnız gateway; Seq/pgAdmin/Kibana `127.0.0.1`'de.
Servisler `Database:MigrateOnStartup=true` ile kendi migration'larını uyguluyor (geliştirmede kapalı).
Cookie `Secure` = `Request.IsHttps`; gateway dış proxy'nin `X-Forwarded-*`'ına yalnız
`ForwardedHeaders:KnownProxies`'teki adres/ağlardan inanıyor. İmajlar `iim-*:local` adıyla yerelde
duruyor (Fırat kalsın dedi). **Kurulum `.env`'ine yazamazsın** (CLAUDE.md) — kabul için sırları
script içinde üretip ortam değişkeni olarak ver; `docker compose up/down` ve volume silme Fırat'a sorulur.

**Adım 25 — ilk kurulum ekranı ve organizasyon adı** (`IIM-145`). Varsayılan hesap/parola **yok**.
Boş veritabanında (ve `Identity:Seed:*` boşken) IdentityService log'a tek bir Warning satırıyla
**tek kullanımlık kurulum kodu** yazar (`XXXX-XXXX-XXXX`, bellekte, yeniden başlatmada yenilenir).
Konsol girişte `GET /api/auth/setup` sorar, gerekiyorsa `/setup`'a yönlenir: kod + organizasyon adı +
ilk Admin → oturum açılır. Yanlış kod / kurulmuş sistem / ikinci deneme hepsi aynı 404; tamamlama
ucu gateway'de rate-limit'li. **Kurulum başına tek organizasyon**; ad Ayarlar → **Organizasyon**'dan
değişir (eski Üyeler sekmesi; `/settings/members` oraya yönlenir). README'de "First-run setup".

**Adım 17.5 — AI analizi GitHub'a bakıyor (MCP)** (`IIM-137`). Ayarlar → **Entegrasyonlar**
artık tek sayfa, üç grup: Gözlemlenebilirlik (telemetri) · **Analiz (GitHub)** · Bildirimler —
Fırat ayrı sekmeleri istemedi, yeni bir bağlantı bu gruplardan birine girer. Admin salt-okunur
bir token ve servis → repo[@branch] eşlemesi girer (`*` = geri kalan). Telemetriden açılan olayın
servisi eşliyse ajan `list_recent_changes` / `inspect_change` alır (repo ve pencere closure'da),
açıklayan commit'i gerekçesinde anar ve olayda **Şüpheli değişiklikler** olarak linklenir.
GitHub'ın barındırdığı MCP sunucusu, `/mcp/x/repos/readonly`, `ModelContextProtocol.Core` 2.2.0.
**Kanaryada:** Fırat Admin olarak üye (`firat@canary.test`, parolası yalnız onda); GitHub bağlantısı
onun PAT'iyle (süreli — bitince Test kırmızı olur), `checkout-service → …@demo/regression-payment-timeout`
ve `*` → master. `demo/regression-payment-timeout` branch'i kabul koşusunun verisi, silme.

**Adım 24 — çözülen olay telemetriye geri bildiriliyor** (`IIM-130`). Açık bir olay Çözüldü ya da
Kapandı yapılırken konsol bir kez **gerçek sorun / yanlış alarm** diye soruyor (API'de `verdict`,
kararsız kapatma 400). Kapatma IncidentService'in **yeni outbox'ından** `IncidentResolvedEvent`
olarak çıkıyor; telemetri, olayı açan imzayı serbest bırakıp kararı sayacına yazıyor. Puanlamada iki
bayrağın yerini tek bir **geçmiş** terimi aldı: `(0.15·p − 0.25·(1−p)) × n/(n+2)`, her zaman
[−0.25, +0.15]. Canlıda aynı patlama iki imzada farklı sonuçlandı: yanlış alarm denen imza 0.8167 ile
eşiğin altında kaldı (zayıf sinyal), gerçek denen imza 0.95 ile yeni bir olay açtı. Kanaryada `verdict-probe` adlı bir test servisi ve onun iki
imzası/olayları kaldı — zararsız.

**Adım 16.5 — davetle kullanıcı yönetimi** (`IIM-122`): açık kayıt yok, bilerek. Admin
Ayarlar → **Üyeler**'den davet eder (e-posta Mailpit'e gider, `http://localhost:8025`; bağlantı
Admin'e bir kez de gösterilir), rol değiştirir, hesabı kapatır/açar, sıfırlama bağlantısı üretir.
Herkese açık `/invite/:token` ve `/reset/:token` ekranları oturumu açar. Kişi kendi parolasını
Profil'den değiştirir. **Organizasyona ait her ayar yalnız Admin'e** (üyeler, entegrasyonlar,
telemetri kaynakları; GET dahil) — Adım 20.6'daki AI dili de öyle olacak. Engineer olay işler,
Viewer okur. Konsolda roller TR'de Yönetici / Mühendis / İzleyici.
**Açık kalan tek şey:** kabul ve sıfırlama formunu gerçek bir parolayla tarayıcıda Fırat'ın bir kez
denemesi (API seviyesinde doğrulandı; ben parola alanı doldurmuyorum).

**Adım 17 — OpenTelemetry tracing** (`IIM-109`): altı süreç de span'lerini log'larla aynı Seq'e
(`:8081`) yazıyor. HTTP, SQL, RabbitMQ (istemcinin kendi span'leri), outbox (trace satırda
taşınıyor), telemetri poll'u ve agent (`invoke_agent` / `chat` / `execute_tool`) tek zincir.
Metrikler **yok**, bilerek — Adım 22.

**Adım 13.5 — OTLP log alımı** (`IIM-115`): müşteri logları artık **gönderilebiliyor**:
`POST {gateway}/otlp/v1/logs`, protobuf ya da JSON, gzip, anahtar `X-IIM-Ingest-Key`'de.
Poll ve push aynı hattan geçiyor (`IngestLogBatchCommand`). Her batch'te imza başına en fazla 10
satır saklanıyor, gerisi en yeni satırın `Occurrences`'ına sayılıyor; tespit satır saymak yerine
topluyor.

**Kabul koşusu** OTLP push'undan bildirime kadar tüm zinciri Seq'te **tek trace** olarak gösterdi.

**Yolda kapanan bir Adım 16 regresyonu:** outbox dispatcher'ı organizasyon kapsamını set
etmiyordu → Adım 16'dan beri hiçbir AI analizi ES'e indekslenmemişti ve her biri
`IncidentAnalyzedEvent`'i 5 sn'de bir sonsuza dek yeniden yayınlıyordu.

---

## Test kimlikleri — Fırat'ı beklemeden tarayıcıda test etmek için (`IIM-121`)

Kanarya organizasyonunda (`11111111-1111-1111-1111-111111111111`, IdentityService'te
"Canary (test)") üç test kullanıcısı var. **Parolayla girilemez** — hash bilerek geçersiz, login
401 döner. (Kanaryada bir de 16.5 koşularından kalan iki **kapalı** "Invited Viewer" var;
dokunma, zararsız.)

| Kullanıcı | Id | Rol |
|---|---|---|
| `claude.admin@canary.test` | `c1a0de00-0000-4000-8000-00000000ad01` | Admin |
| `claude.engineer@canary.test` | `c1a0de00-0000-4000-8000-00000000e603` | Engineer |
| `claude.viewer@canary.test` | `c1a0de00-0000-4000-8000-00000000ee02` | Viewer |

**Oturum açma:** imza anahtarıyla (`dotnet user-secrets list --project src/Services/IdentityService/IdentityService.API` →
`Jwt:SigningKey`; ekrana yazdırmadan, script içinde okuyarak) HS256 bir JWT bas: `sub` = kullanıcı
id'si, `org` = kanarya, `role`, `name`, `iss`/`aud` = `iim`. `/api/auth/me` kullanıcıyı
veritabanında aradığı için `sub` **gerçek bir kullanıcı id'si olmak zorunda**.

**Tarayıcıda:** Fırat'ın oturumu `localhost` cookie'lerinde yaşıyor ve httpOnly cookie'nin üstüne
JS ile yazılamıyor. Bu yüzden test konsolu **ikinci bir Vite'ta, başka bir host'ta** açılır:

```bash
npm run dev --prefix web -- --host 127.0.0.1 --port 5174 --strictPort
```

`http://127.0.0.1:5174`'te `document.cookie = "iim.access=<token>; path=/; SameSite=Strict; max-age=14400"`
ve sayfa yenilenir. Cookie'ler host başına tutulduğu için iki oturum birbirine karışmaz.

**Seq (`:8081`):** tarayıcı panelinde Fırat bir kez giriş yaptı; oturum kalıcı. Trace okumak için
UI metni yerine Seq API'si daha güvenilir — oturumlu sekmede
`fetch('/api/events?filter=...&fromDateUtc=...&toDateUtc=...')` (filtre URL hash'ine yazılınca UI
sonuçları yenilemiyor, eski akışı gösteriyor — bir yanlış alarm buradan çıktı).

**İzolasyon probu:** `node tools/dev/org-isolation-probe.mjs` — gateway, incident, telemetry,
notification çalışırken. Bütün veri ve ayarların organizasyon bazlı olduğunu kanarya ↔ Acme
arasında yeniden kanıtlar (listeler, id'ler, yazmalar, roller, soketler). Organizasyona dokunan her
değişiklikten sonra koşturulmalı; çıkış kodu 0 değilse bir şey sızıyor demektir.

---

## Ortamı ayağa kaldırma

```bash
docker compose up -d                 # onay ister
```

Konteynerler makine uykusunda `Exited (255)` oluyor; bir API 500/502 verirse önce `docker ps -a`.

Servisler — **hepsi `--launch-profile http` ile** (yoksa ortam Production olur, user secrets
yüklenmez, `Jwt:SigningKey is not configured`). `.claude/launch.json`'da yedi yapılandırma var,
hepsi `--no-build`: önce `dotnet build`, sonra başlat.

| Süreç | Port | Not |
|---|---|---|
| Gateway.API | **5100** | tarayıcının ve collector'ların konuştuğu tek yer (`/api`, `/hubs`, `/otlp`) |
| IdentityService.API | 5240 | |
| IncidentService.API | 5203 | |
| AgentOrchestrator.API | 5130 | **Anthropic API key gerekir** |
| NotificationService.API | 5210 | |
| TelemetryIngestionService.API | 5220 | `/otlp/v1/logs` burada |
| web (Vite) | 5173 | `/api`, `/hubs`, `/otlp` gateway'e |
| demo/MonitoredShop | 5300 | izlenen sistem taklidi |

**Önizleme aracı en fazla 5 sunucu açıyor.** Fazlası Bash'te `run_in_background` ile
(`dotnet run --project … --launch-profile http --no-build`).

**MonitoredShop'un iki modu:** `Logging:Sink = Seq` (varsayılan; seq-demo'ya yazar, **Acme'nin**
Seq kaynağı okur → Acme'nin Jira'sı) ya da `Otlp` (OTel SDK ile doğrudan IIM'e; `Otlp:Key`
zorunlu). Kanarya testleri için **Otlp modu + kanaryanın OTLP kaynağının anahtarı**:
`Logging__Sink=Otlp Otlp__Key=<anahtar> dotnet run --project demo/MonitoredShop --launch-profile http --no-build`.
Kanaryadaki "OTLP acceptance (IIM-115)" kaynağının anahtarı yalnızca oluşturulduğu an görüldü;
yenisi için `POST /api/telemetry-sources/{id}/rotate-key` (kanarya Admin token'ıyla).

---

## İhlal edilmemesi gereken kararlar

Bunlar tartışılıp karara bağlandı; değiştirmeden önce gerekçeyi oku.

- **Terfi yolunda AI yok.** Incident açma kapısı hızlı, ucuz ve sabah 3'te savunulabilir olmalı.
- **Signal ≠ Incident.** `≥0.90` incident · `0.60–0.89` zayıf sinyal · `<0.60` sadece kayıt.
- **Telemetri bir entegrasyon.** Kaynak müşteri tarafından konfigüre edilir; platform kendini
  değil müşterinin sistemini izler — `seq` ile `seq-demo` **asla karışmaz**.
- **Vendor başına connector yazma.** Uzun kuyruğun cevabı OTLP (Adım 13.5, yapıldı).
- **Her log satırı saklanmaz** (13.5): imza başına örnek + sayı. "Kaç tane" soran her şey
  `Occurrences`'ı **toplar**, satır saymaz. Fatal, parmak izsiz kayıt ve batch'in en yeni anı
  hiç katlanmaz.
- **Push'un organizasyonunu anahtar söyler**; organizasyonu **endpoint set eder** (açan set eder
  kuralı). Anahtar yalnızca hash'lenmiş saklanır ve düz hâli yalnızca onu üreten yanıtta vardır.
- **Trace hedefi Seq**, metrikler Adım 22. Agent span'leri **içerik taşımaz**
  (`EnableSensitiveData = false`) — trace deposunda organizasyon sınırı yok.
- **`DetectedAt` ≠ `CreatedAt`.** **Queue-per-service.** **Outbox sırası:** önce yan etki, sonra
  `ProcessedOn`. **ES türetilmiş görünüm.**
- **Organizasyon sınırı** (Adım 16): kapsamı handler değil açan set eder (middleware / bus /
  poller / outbox dispatcher / OTLP endpoint); query filter'lar tek tek; DbContext org'u sorgu
  anında okur; sınırı geçen okuma yalnızca adlandırılmış metotla (`…ForPollingAsync`,
  `…ForIngestAsync`); unique index'ler org'a göre (**tek istisna** ingest anahtarının hash'i — kimin
  olduğu bilinmeden aranıyor); hub yayını asla `Clients.All`.
- **Token asla response gövdesinde değil** (ingest anahtarı bir token değil; bir kez gösterilir).
- **Açık kayıt ekranı yok, bilerek** — hesaplar davetle açılır (Adım 16.5). Organizasyona ait
  her ayar yalnız Admin'e, GET dahil (`PlatformPolicies.Administer`); yeni bir org ayarı eklenirse
  izolasyon probuna da eklenir.
- **Tek kullanımlık bağlantılar** (davet, sıfırlama) ingest anahtarıyla aynı desen: 256 bit, yalnız
  SHA-256 saklanır, düz hâli yalnızca üreten yanıtta ve e-postada; geçersiz / süresi dolmuş /
  kullanılmış tek ve aynı 404.

---

## Bilinen tuzaklar

- **Birim testleri veritabanı kısıtlarını ve gerçek gönderenleri görmez.** Bu oturumda ikisi de
  ısırdı: .NET OTel SDK'sının `{OriginalFormat}` göndermediği ancak canlı koşuda çıktı. Şema ya da
  tel formatı değişikliğinden sonra canlı doğrula.
- **`.gitignore`'daki `logs/` kuralı** `logs` adlı her dizini yutar — OTLP proto'ları için istisna
  eklendi. Yeni bir `logs` dizini açarsan `git status --ignored` ile bak; build yerelde geçer,
  temiz klon kırılır.
- `dotnet ef database update --no-build` yeni migration'ı eklemeden önceki derlemeyi kullanır →
  "pending model changes". Migration ekledikten sonra `--no-build`'siz çalıştır.
- `docker exec` ile heredoc SQL: **`-i`** olmadan stdin gitmez, komut sessizce hiçbir şey yapmaz.
- `dotnet build` çalışan servisler varken başarısız olur (MSB3027) — önce durdur.
- Vite'ta sözlük dosyası değişince HMR `useLanguage must be used inside LanguageProvider`
  üretebiliyor; sayfa yenilenince geçer, gerçek hata değil.
- Windows PowerShell 5.1 cookie/Set-Cookie davranışı güvenilmez; HTTP testleri için Python
  `urllib` ya da `HttpClient`. `curl`/`wget` `deny` listesinde.
- Jira MCP bir kez tamamen yanıt vermez oldu; takılırsa durumu `getJiraIssue` ile teyit et.

## İzinler

Jira write (create/edit/transition/comment) ve `dotnet ef` **ön onaylı**.
`migrations remove` ve `docker compose up/down` hâlâ sorar; `database drop`, force push,
`reset --hard`, `rm -rf` **yasak**. `.env` ve `appsettings.*.json` okunmaz/yazılmaz.

## Dosya haritası

`PROGRESS.md` yol haritası + tech-debt · `production_necessaries.MD` üretim engelleri ·
`CLAUDE.md` çalışma anlaşması · `src/Gateway/` YARP · `src/BuildingBlocks/` 7 paylaşılan proje
(`Observability` = log + tracing, `Outbox` = trace ve org satırda) ·
`src/Services/TelemetryIngestionService/…Infrastructure/Otlp/` proto'lar + codec ·
`src/Services/` 5 servis × 4 katman · `web/` konsol · `demo/MonitoredShop` izlenen sistem taklidi ·
`demo/otel-collector/collector.yaml` müşteri tarafı örnek.
