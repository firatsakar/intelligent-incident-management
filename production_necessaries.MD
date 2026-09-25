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

Beş servisin `appsettings.json` dosyasında Postgres parolaları ve RabbitMQ kimlik bilgileri
düz metin duruyor ve bu dosyalar repoda. Geliştirme için kabul edilebilir, üretim için değil.

- Etkilenen: `IncidentService.API`, `AgentOrchestrator.API`, `NotificationService.API`,
  `TelemetryIngestionService.API`, ve Adım 16'dan beri `IdentityService.API`
- **Adım 16 bir sır daha ekledi: `Jwt:SigningKey`.** Beş servisin hepsi token'ı kendi başına
  doğruluyor, yani anahtar beş yerde. Geliştirmede user secrets'ta (git'e girmiyor), ama
  **user secrets yalnızca Development ortamında yükleniyor** — üretimde ortam değişkeni ya da
  secret store'dan gelmek zorunda. Aynı şey `Identity:Seed:*` için de geçerli. Anahtarı
  değiştirmek tüm oturumları anında geçersiz kılar, ki rotasyonun bedeli bu.
- Yapılacak: bağlantı dizelerini ortam değişkeni / secret store'a taşı, repodaki değerleri
  yer tutucuya indir. Mevcut parolalar repo geçmişinde kaldığı için **rotasyon gerekir**.

### 🔴 Müşteri credential'ları veritabanında düz metin

`Integration.config` ve `TelemetrySource.config` (jsonb) müşterinin SMTP parolasını, Jira API
token'ını ve log sistemi anahtarını olduğu gibi saklıyor. Adım 17.5'ten beri bunlara
**`github_connections.Token`** da eklendi: müşterinin koduna salt-okunur erişim veren token.
Bu token API yanıtlarında hiç yer almıyor, `hasToken` dışında maskeli hâli bile dönmüyor; ama
veritabanında düz metin.

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

### ✅ ~~Üç SignalR hub'ı da herkese yayın yapıyor~~ — **kapandı** (Adım 16, `IIM-106`)

Üç hub `OrganizationHub`'dan türüyor: `[Authorize]`, ve `OnConnectedAsync` bağlantıyı kendi
claim'inden alınan **tek bir** gruba koyuyor. Notifier'lar kapsamdaki organizasyona yayın
yapıyor, asla herkese değil; kapsam yoksa hiçbir yere. İki organizasyon için imzalanmış iki
token'la gateway üzerinden ölçüldü: B'nin olayını B'nin soketi duydu, A'nınki duymadı, anonim
soket 401 aldı.

### ✅ ~~Hiçbir API'de kimlik doğrulama yok~~ — **kapandı** (Adım 16, `IIM-99`)

Beş serviste fallback authorization policy: token istemek varsayılan, muafiyet yazılması
gereken şey. Yalnızca iki uç muaf, ikisi de kullanıcı token'ı taşıyamaz:
`api/dev/seed-demo-incidents` (Development dışında 404) ve `api/Analyses/reindex`.
Yazma uçları ayrıca `Operate` policy'si istiyor (Admin veya Engineer); Viewer 403 alıyor.

### ✅ ~~Frontend'deki giriş ekranı kozmetiktir~~ — **kapandı** (Adım 15 + 16)

Parola soruluyor, IdentityService'te BCrypt ile doğrulanıyor, oturum iki httpOnly +
SameSite=Strict cookie'de taşınıyor (15 dk access, 14 gün rotasyonlu refresh). `session.ts`
başındaki "NOTHING IN THIS FILE IS SECURITY" uyarısı, anlattığı şeyle birlikte kaldırıldı.

### ✅ Organizasyon izolasyonu — envanter ve tekrar koşturulabilir prob (Adım 16.5, `IIM-124`)

Fırat'ın kuralı: bütün veri ve bütün ayarlar organizasyon bazlı; bir organizasyonun hiçbir şeyi
başkasına ulaşmaz. Bu bir kez ölçülüp geçilecek bir şey değil — her değişiklikten sonra yeniden
sorulacak bir soru. Cevabı iki parça:

**Envanter** — organizasyon nereden geliyor, nerede süzülüyor:

| Yer | Organizasyon nereden | Nerede süzülüyor |
|---|---|---|
| `incidents` | kapsamı açan (HTTP claim / bus) | `HasQueryFilter` |
| `integrations`, `notification_deliveries` | aynı | `HasQueryFilter` (ikisi de) |
| `analyses` (Agent) | bus | `HasQueryFilter` |
| `telemetry_sources`, `source_cursors`, `log_records`, `error_signatures`, `signals`, `detection_rules` | poller: kaynağın satırı; OTLP: ingest anahtarının satırı; HTTP: claim | `HasQueryFilter` (altısı da) |
| `outbox_messages` (Agent, Identity, Telemetry) | satır yazıldığı transaction'da damgalanıyor | filtresiz, bilerek: dispatcher her mesajı **satırdaki organizasyonla kendi scope'unda** işliyor (`IIM-112`'den beri) |
| IdentityService: `users`, `refresh_tokens`, `organizations` | — | **filtresiz, bilerek**: giriş e-postayla, organizasyon bilinmeden yapılıyor. Oturum `sub` ve refresh hash'iyle bulunuyor; üye yönetimi uçları (Adım 16.5) organizasyonu **açıkça** karşılaştırıyor |
| Elasticsearch `incident-analyses` | belge `organizationId` taşıyor | aramada filtre bağlamında `Term organizationId`; model hangi geçmişi aradığını seçemiyor (closure) |
| Soketler | bağlantının token'ı | `org:{id}` grubu; organizasyon yapılandırması yalnız `org:{id}:admins`'e (`IIM-123`). `Clients.All` yok |
| Arayüz dili | — | kişisel tercih, tarayıcıda; organizasyonla ilgisi yok. **AI dili (Adım 20.6)** ise bir org ayarı olacak ve yalnız Admin değiştirecek |

Filtreyi aşan her okuma adlandırılmış bir metot ve sayısı dört:
`GetAllCompletedForReindexAsync` (yalnız `/internal` reindex), `GetEnabledForPollingAsync`,
`GetForPollingAsync` (poller), `FindByIngestKeyHashForIngestAsync` (OTLP). Beşincisi Adım 16.5'te
gelecek: davet kabulü (`FindByTokenHashForAcceptAsync`).

**Prob** — `node tools/dev/org-isolation-probe.mjs` (yalnız dev; imza anahtarını user secrets'tan
okur, yazdırmaz). Gateway'e iki organizasyon olarak konuşur: kanarya (gerçek Admin, Engineer ve
Viewer test kullanıcıları) ve Acme (yalnız okunur). Kontrolleri:

- her liste yalnızca kendi organizasyonunun satırlarını döndürüyor;
- öbürünün id'leri okumada ve yazmada **404** (403 değil);
- yapılandırma Engineer ve Viewer'a 403 (GET dahil), Admin'e 200;
- soketler yalnızca kendi organizasyonlarını duyuyor ve Admin olmayan soket yapılandırma
  yayınlarını duymuyor.

Yazma denemeleri tek yönlü: Acme'nin token'ıyla kanaryanın satırlarına. İzolasyon bozuk çıksa
değişen tek şey kanaryanın test verisi olur. İki taraf da boş olan bir liste "karşılaştırılmadı"
diye yazılır, geçmiş sayılmaz. **İlk koşu (2026-09-25): 55/55.** Karşılaştırılan satırlar:
olaylar 100/100, sinyaller 2/86, log kayıtları 23/200, imzalar 2/64, kaynaklar 1/1,
entegrasyonlar 1/3.

### ✅ ~~Kullanıcı yönetimi yok~~ — **kapandı** (Adım 16.5, `IIM-122`)

Herkese açık kayıt değil, **davet** (Fırat, 2026-09-25): B2B'de hesabı organizasyonun Admin'i
açar; açık kayıt herkesin organizasyon açabilmesi demek ve her incident ücretli bir AI analizi
tetikliyor. Admin, Ayarlar → Üyeler'den davet eder (e-posta + Admin'e bir kez gösterilen bağlantı,
7 gün), rol değiştirir, hesabı kapatır ya da açar ve parola sıfırlama bağlantısı üretir (24 saat).
Kişi kendi parolasını Profil'den, mevcut parolasıyla değiştirir. Bağlantılar 256 bit ve tek
kullanımlık; yalnızca SHA-256'ları saklanıyor. Geçersiz, süresi dolmuş, kullanılmış ve geri
çekilmiş bağlantı **tek ve aynı** 404'ü alıyor. Kimse kendi hesabına dokunamıyor ve **son aktif
Admin** düşürülemiyor. Organizasyona ait her ayar (üyeler, entegrasyonlar, telemetri kaynakları)
yalnız Admin'e açık, GET dahil. Kalanlar:

- 🟡 **Kendi kendine parola sıfırlama yok** ("parolamı unuttum"). Bağlantıyı bir Admin üretiyor.
  Tek Admin'li bir organizasyonda Admin parolasını unutursa, platform operatörünün veritabanına
  dokunması gerekir.
- 🟡 **Organizasyon oluşturma yok.** Yeni müşteri ve ilk Admin'i hâlâ seed ile ya da elle
  açılıyor. Bir platform-operatörü kimliği ya da bir kurulum komutu gerekiyor.
- 🟡 **Platform e-postası tek bir SMTP** (`Mail:*`; dev'de Mailpit). Gönderim başarısız olursa
  davet yine oluşur ve Admin'e `emailSent: false` gösterilir; başka kimse haberdar olmaz. Üretimde
  SPF/DKIM'li bir gönderici gerekir, ve **`Console:BaseUrl` konsolun gerçek adresi olmalı** —
  yanlışsa e-postadaki bağlantılar `localhost`'u gösterir.
- 🟡 **Kim kimi davet etti, kim sıfırladı** satırlarda duruyor (`InvitedByUserId`,
  `IssuedByUserId`), ama görüntüleyen bir denetim ekranı yok (Adım 14).
- SSO / SCIM / MFA → aşağıdaki "Kimlik doğrulama yalnızca yerel parola".

### 🟡 Oturum iptali yalnızca refresh tarafında

Access token 15 dakika boyunca geri alınamaz — tasarım gereği, çünkü beş servis onu kimseye
sormadan doğruluyor. Bir hesabı devre dışı bırakmak refresh'i hemen durdurur ama elde kalan
access token en fazla 15 dakika daha geçerlidir. Anında kesme gerekirse: kısa bir iptal listesi
ya da access ömrünü kısaltmak.

### 🟡 Kimlik doğrulama yalnızca yerel parola

SSO, SCIM, MFA yok. Kurumsal bir müşterinin ilk soracağı şeyler.

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

### ✅ ~~CORS yok~~ — **gereksiz kılındı** (Adım 15, `IIM-93`)

CORS eklenmedi ve eklenmeyecek: gateway tek origin sağlıyor, tarayıcı yalnızca onunla
konuşuyor. `dist/` artık gateway tarafından servis edilebiliyor (`Spa:Root`) ve uygulama Vite
olmadan çalışıyor — Adım 15'te ölçüldü.

### 🟡 Rate limiting yalnızca kimlik uçlarında

Gateway `/api/auth/login`'i adres başına dakikada 10 denemeyle sınırlıyor (429 +
`Retry-After`). Adım 16.5'ten beri parola belirlenen ya da sınanan her uç — davet önizleme/kabul,
sıfırlama önizleme/tamamlama, `POST /api/auth/password` — **aynı** sınırlayıcıda (paylaşılan
kova: dakikada toplam 10). Diğer uçlar sınırsız: `POST /api/incidents` bir AI analizi ve bildirim zinciri
tetikliyor, entegrasyon test uçları dışarıya istek atıyor. Kimliksiz erişim kapandığı için risk
artık yalnızca kötü niyetli bir *üye*den geliyor, ama sıfır değil.

- `dist/`'in gateway'e kopyalanması bir publish adımı olarak henüz yok (Adım 21/22).

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

### 🟡 Veritabanı kısıtları hiçbir testte yok

Adım 16'nın kabul koşusu, birim testlerinin göremeyeceği **üç hata** buldu: organizasyona göre
tekil olması gereken üç unique index tablo genelindeydi (`detection_rules.Name`,
`telemetry_sources.Name`, `error_signatures.Fingerprint`). Sonuncusu en ağırıydı: aynı
framework'ü çalıştıran iki organizasyon aynı parmak izini üretir ve ikincinin poll'u, diğeriyle
ortak tek bir hatası olduğu an tamamen başarısız olurdu. Repository'ler mock'landığı için hiçbir
test bir index görmüyor. Adım 23'ün (Testcontainers + gerçek Postgres) en somut gerekçesi bu.

- Aynı boşluk sorgu süzgeçleri için de geçerli: `HasQueryFilter`'ın gerçekten süzdüğü birim
  testleriyle değil, veritabanına başka bir organizasyonun satırı elle eklenerek kanıtlandı.
- AgentOrchestrator'ın test projesi hâlâ yok — ve EF olmayan tek süzgeci (Elasticsearch'ün
  organizasyon filtresi) o tutuyor.

## 3. Veri yönetimi ve saklama

### 🔴 `log_records` sınırsız büyüyor

Telemetri her poll'da Error/Fatal kayıtları yazıyor ve **hiçbir temizlik yok**. Gerçek bir
müşteri hacminde tablo kısa sürede yönetilemez hale gelir ve pencere sorguları yavaşlar.

- **Adım 13.5 büyümenin eğimini kırdı** (`IIM-116`): her batch'te imza başına en fazla 10 örnek
  satır saklanıyor, gerisi en yenisinin `Occurrences`'ına sayılıyor — canlıda 300 olaylık bir
  fırtına 10 satır. Tespit, baseline ve huni satır saymak yerine topluyor. Push kaynağı ayrıca
  en düşük şiddetin altını geldiği anda atıyor.
- Hâlâ yapılacak: retention job (öneri: 7 gün) — `OutboxCleanupService` deseninin aynısı.
  Katlama sabit bir hata oranını sınırlamıyor, yalnızca patlamaları sıkıştırıyor.

### 🟡 Yedekleme ve geri yükleme prosedürü yok

Dört ayrı Postgres veritabanı var (incident / notification / telemetry / agent). Yedek planı,
saklama süresi ve **geri yükleme tatbikatı** tanımlı değil.

### ⚪ ES index lifecycle

`incident-analyses` index'i için ILM politikası yok.

---

## 3.5 Ürün döngüsündeki kopukluk

### ✅ ~~Çözülen incident telemetriye geri bildirilmiyor~~ — **kapandı** (Adım 24, `IIM-130`)

Olay kapatılırken bir kez **gerçek / yanlış alarm** kararı soruluyor. IncidentService bunu outbox
üzerinden (`IncidentResolvedEvent`) yayınlıyor. Telemetri tarafında imza olaydan ayrılıyor ve karar
imzanın sayacına yazılıyor. Puanlamadaki iki ölü bayrak, tek ve sınırlı bir **geçmiş** terimine
dönüştü: `(0.15·p − 0.25·(1−p)) × n/(n+2)`, her zaman [−0.25, +0.15] aralığında. Kanaryada canlı
doğrulandı; zincir Seq'te tek trace olarak görünüyor. Kalanlar:

- 🟡 **Karar düzeltilemiyor.** Yeniden açmak kararı temizliyor, ama telemetrinin öğrendiği geri
  alınmıyor. Yanlış verilmiş bir kararı geri almak için bir "düzeltme" olayı gerekir.
- 🟡 **Kenar durum:** imza, olay kapanmadan önce yeni bir olaya geçtiyse (sessizlik penceresi
  dolduysa) o karar sayılmıyor.
- ⚪ Karar geçmişi yalnız sayaç olarak tutuluyor. "Son N karar" gibi pencereli bir okuma
  istenirse, imza başına karar satırları gerekir (Fırat 2026-09-25'te ömür boyu oranı seçti).

Eski metin:

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

### 🔴 Outbox'ta yeniden deneme sınırı yok

`OutboxDispatcher` başarısız bir satırı her 5 saniyede **sınırsız** yeniden dener ve her denemede
handler'ın ilk yan etkisi (publish) tekrar olur. Adım 17'de canlı görüldü: organizasyon kapsamı
eksikliği yüzünden deterministik olarak düşen bir satır **519. denemedeydi** ve
`IncidentAnalyzedEvent`'i 43 dakikadır 5 saniyede bir yeniden yayınlıyordu. Kök neden kapandı
(`IIM-112`), sınırsızlık kapanmadı: bir sonraki deterministik hata aynı döngüyü kurar.

**Adım 17.5'te ikinci kez yaşandı (2026-09-25).** Şüpheli değişikliği olan ilk analiz
kaydedilebildi ama geri okunamadı: sabit boyutlu bir dizi varsayılanı, EF'in JSON koleksiyonunu
doldurmasını engelledi. Outbox handler'ı okumadan önce yayınladığı için satır ~20 kez yeniden
denendi ve olay her 5 saniyede yeniden yayınlandı. Kök neden `IIM-141` ile kapandı. Tüketiciler
idempotent olduğu için yalnız bir teslimat oluştu. Tavan hâlâ yok.

- Yapılacak: `RetryCount` için bir tavan (örn. 10) ve tavanı aşan satırı `GetPendingAsync`
  dışına alıp görünür kılmak (DLQ derinliği gibi alarm) — sessizce bırakmak değil.

### 🟡 AI analizi dış sisteme bağımlı: GitHub MCP (Adım 17.5)

Analiz, organizasyonun eşlediği servislerde GitHub'ın barındırdığı MCP sunucusunu
(`api.githubcopilot.com/mcp/x/repos/readonly`) çağırıyor.

- **Kullanılabilirlik:** GitHub ulaşılamazsa analiz GitHub'sız tamamlanıyor. Bu canlıda
  doğrulandı: oturum açılamayınca uyarı log'u yazılıyor, sonuç yine geliyor. Her çağrının 15 sn
  zaman aşımı var.
- **Rate limit:** analiz başına en fazla 2 liste ve 5 inceleme çağrısı yapılıyor, hepsi
  organizasyonun kendi token'ıyla. Yüksek hacimde token'ın GitHub kotası tükenebilir, bu durum
  izlenmiyor.
- **Prompt injection:** commit mesajları ve diff'ler dışarıdan gelen içerik. Bu yüzden:
  talimatlar bu içeriği veri sayıyor; model yalnız sha seçebiliyor ve yalnız bu analizde görülen
  sha'lar kabul ediliyor; URL'leri platform kuruyor; IncidentService ve konsol yalnız
  `https://github.com` linklerini tutuyor. Yine de bir commit mesajı modelin *gerekçesini*
  etkileyebilir. Bu kalan bir risk: gerekçe tavsiye niteliğinde, hiçbir şey otomatik
  yapılmıyor.
- **Salt-okunurluk:** sunucunun read-only modu canlıda doğrulandı; katalogda yazan hiçbir araç
  yok. Buna ek olarak istemci yalnız `list_commits` ve `get_commit` çağırıyor. `X-MCP-Tools`
  başlığı bu yolda kataloğu daraltmadı; hiçbir şey ona dayanmıyor.
- **Sürüm:** `ModelContextProtocol.Core` 2.2.0. GitHub tarafında araç şekli değişirse keşif
  log'u bunu gösterir: katalog süreç başına bir kez log'lanıyor ve ayrıştırıcı savunmacı.

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

### ✅ ~~Distributed tracing yok~~ — **kapandı** (Adım 17, `IIM-109`)

Altı süreç de (`AddPlatformTracing`) span'lerini OTLP ile log'larla aynı Seq'e yazıyor; bir log
satırı kendi trace'ini açıyor. HTTP, SQL, RabbitMQ (istemcinin kendi span'leri), outbox (trace
satırda taşınıyor), telemetri poll'u ve agent'ın adımları (`invoke_agent` / `chat` /
`execute_tool`) tek zincir. Üretim için kalanlar:

- **Örnekleme.** Bugün her trace saklanıyor (yalnızca kökü olmayan istemci çağrıları düşüyor).
  Gerçek trafikte oran tabanlı örnekleme ya da Collector'da tail sampling gerekir.
- **Trace deposu organizasyon sınırı taşımıyor.** Bu yüzden agent span'leri **içerik
  taşımıyor** (`EnableSensitiveData = false`) — model, token, süre, tool adı var; prompt ve
  cevap yok. Bu kural gevşetilecekse önce depo erişimi organizasyona göre bölünmeli.
- Seq burada hem log hem trace deposu; üretimde Collector üzerinden ayrı bir trace backend'i
  (Tempo/Jaeger) seçeneği açık — servisler OTLP konuştuğu için yalnızca endpoint değişir.

### 🟡 Metrik toplanmıyor

`TelemetryConstants.MetricNames` tanımlı ama **hiçbir yerde kullanılmıyor** — `incidents.created`,
`notifications.sent`, `ai.request_duration_seconds` hepsi ölü tanım.

- **Adım 17'de bilinçli olarak yapılmadı** (Fırat, 2026-09-24): trace hedefi Seq seçildi ve Seq
  metrik göstermiyor; görünmeyecek bir metrik yayınlamak bir ölü tanımı başka bir ölü tanımla
  değiştirmek olurdu. **Adım 22'ye** (Prometheus/Grafana ya da OTLP metrik backend'i) ait.

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

## 8.5 Dil (Adım 20.5, 2026-09-23)

### ⚪ Ekranlar karışık dilli — bilinçli ve yazılı

Konsol EN/TR konuşuyor ve **kendi metninin tamamı** çevrildi. Çevrilmeyen tek şey **servislerden
gelen düzyazı**: `aiReasoning`, `signal.reason`, `delivery.lastError`, ve `ApiError`'ın RFC 9457
gövdesinden çıkardığı mesaj. Yani Türkçe bir ekranda İngilizce paragraflar görünüyor.

Bu bir eksiklik değil, alınmış bir karar: alternatifi ya backend i18n'i (ayrı bir iş) ya da
**modelin gerekçesini uydurarak çevirmek**tir. Nerede durduğu profil ekranındaki Language
kartında bir kez söyleniyor, her İngilizce paragrafın yanında dipnot olarak değil.

**Kapatmanın maliyeti**, bir gün istenirse: `Accept-Language`'ın gateway'den (Adım 15) servislere
taşınması · FluentValidation mesajlarının ve ProblemDetails başlıklarının kaynaklanması ·
`SignalScoring`'in `Reason` metnini serbest metin yerine anahtar + parametre olarak üretmesi.
**Model çıktısı buna dahil değil** — o Adım 20.6'nın konusu ve ayrı bir karar.

### ⚪ Üçüncü dil eklemenin maliyeti bilerek düşük tutuldu

`web/src/lib/i18n/tr.ts` `en.ts`'in şekliyle tiplenmiş, yani eksik bir anahtar **derleme
hatası** — çalışma zamanında sessizce İngilizceye düşme yok. `web/scripts/find-untranslated.mjs`
(`npm run check:i18n`) sözlüğe hiç girmemiş metni buluyor; derleyicinin göremediği yarı bu.
Üçüncü bir dil, yeni bir dosya + `languages` dizisine bir üye demek.

**Bilinmesi gereken iki şey:** `<html lang>` altında CSS `uppercase` **dile göre** büyütüyor
(Türkçe'de `i` → `İ`), o yüzden case dönüşümünden geçen hiçbir yere veri konulmamalı — sekiz
kullanım denetlendi, hepsi kabuk. Ve tarih/sayı biçimi dile bağlı: `lib/format.ts`'in `Intl`
paketi aktif dile göre kuruluyor, tarayıcının diline göre değil.

---

## 9. Ürünleştirme öncesi operasyonel notlar

**Telemetri kaynağı bağlama:** iki tür.
- `kind: Seq` (çekilen) — `config.Url` zorunlu, `ApiKey` kimlik doğrulaması açık kaynaklarda
  gerekli. `POST {id}/test` ayarları kaydetmeden doğrular.
- `kind: Otlp` (gönderilen, Adım 13.5) — ayarı yok, isteğe bağlı `MinimumSeverity`
  (Warning|Error). Oluşturma yanıtı **anahtarı bir kez** döndürür; konsol onu adres, başlık
  adı ve doldurulmuş Collector config'iyle birlikte gösterir. Gönderen `POST {gateway}/otlp/v1/logs`'a
  OTLP/HTTP (protobuf ya da JSON, gzip) ile, anahtarı `X-IIM-Ingest-Key` başlığında yollar.
  Kaybolan anahtarın cevabı `POST {id}/rotate-key`. Elasticsearch/Loki/Datadog kullanan müşteri
  için yol budur: shipper'ı (OTel Collector, Fluent Bit, Vector) IIM'e yönlendirmek.
  Örnek: `demo/otel-collector/collector.yaml`.

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
