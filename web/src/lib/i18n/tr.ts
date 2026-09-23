import type { Dictionary } from './en'

/**
 * Türkçe.
 *
 * Typed as `Dictionary`, so this file cannot be incomplete: a key that exists in `en.ts` and not
 * here fails the build, and so does one whose shape disagrees. Nothing falls back to English at
 * runtime, because nothing is allowed to be missing at compile time.
 *
 * Written as Turkish copy rather than as a translation. The English in this console carries the
 * design's argument — a sentence like "A zero here means the gate refused nothing, not that it
 * looked at nothing" exists to stop a specific misreading — and a word-for-word rendering keeps
 * the words while losing the reason they were chosen.
 *
 * ---- Terim sözlüğü -----------------------------------------------------------------------
 *
 * Otuz ekranda aynı terimin üç türlü çevrilmemesi için. Değiştirmek isteyen buradan değiştirsin,
 * tek tek cümlelerden değil.
 *
 *   incident              olay
 *   signal                sinyal
 *   error signature       hata imzası / imza
 *   gate (promotion gate) kapı
 *   promote / promotion   terfi
 *   burst                 patlama
 *   funnel                huni
 *   heat map              ısı haritası
 *   evidence              kanıt
 *   delivery              teslimat
 *   integration           entegrasyon
 *   telemetry source      telemetri kaynağı
 *   log record            log kaydı
 *   severity              şiddet
 *   confidence            güven
 *   score                 puan
 *   window                pencere
 *
 * `Alert` (kaynak) **alarm**, `Warning` (şiddet) **uyarı** — ikisi de "uyarı" olsaydı incident'ın
 * nereden geldiğiyle ne kadar ciddi olduğu aynı kelimeyle anlatılırdı.
 */
export const tr: Dictionary = {
  common: {
    selected: 'Seçili',
  },

  language: {
    change: 'Dili değiştir',
    title: 'Dil',
    description:
      'Bu tarayıcıda saklanıyor, adınızın altında değil — ikinci bir makine dili yine tarayıcıdan okur.',
    legend: 'Dil',
    options: {
      en: 'Konsolun kendi metni, tarihleri ve sayıları İngilizce.',
      tr: 'Konsolun kendi metni, tarihleri ve sayıları Türkçe.',
    },
    passthrough:
      'Konsolun kendi metni çevrildi. Servislerden gelen metin — bir analizin gerekçesi, bir tespitin nedeni, sağlayıcının hata mesajı — yazıldığı gibi, İngilizce aktarılıyor.',
  },

  theme: {
    change: 'Temayı değiştir',
    title: 'Görünüm',
    description:
      'Bu tarayıcıda saklanıyor, adınızın altında değil — ikinci bir makine yine Sistem ile açılır.',
    legend: 'Tema',
    options: {
      light: { label: 'Açık', detail: 'Her zaman açık palet.' },
      dark: { label: 'Koyu', detail: 'Her zaman koyu palet.' },
      system: { label: 'Sistem', detail: 'İşletim sisteminizi izler.' },
    },
    palette: { light: 'açık', dark: 'koyu' },
    resolved: (palette: string) => `Sisteminiz şu anda ${palette} paleti istiyor.`,
  },

  profile: {
    title: 'Profil',
    intro:
      'Bu oturumun altında çalıştığı ad, ekrandaki her şeyin ait olduğu organizasyon, ve siz buradayken konsolun nasıl göründüğü ve okunduğu.',
    identity: {
      title: 'Kimlik',
      description: 'Bu konsolun sizi kim sandığı — ve bunun ne değerde olduğu.',
      signOut: 'Oturumu kapat',
      notAnAccountTitle: 'Bu bir hesap değil',
      notAnAccount:
        'Yukarıdaki ad bu tarayıcıda saklanıyor ve hiçbir şey onu doğrulamadı. Parola yok, hiçbir sunucuda profil yok, ona bağlı bir yetki de yok — bu konsolun arkasındaki servisler, oturum hangi adı taşırsa taşısın, kendilerine ulaşabilen herkese cevap veriyor. Başka bir adla çalışmak için oturumu kapatıp o adı girin. Gerçek oturum açma gateway ile geliyor, ve bu sayfa onun ineceği yer.',
      ownership:
        'Olaylar, sinyaller, kaynaklar ve entegrasyonlar onları açan kişiye değil organizasyona ait. Bu sürümde tek bir organizasyon var.',
    },
  },

  labels: {
    incidentStatus: {
      Open: 'Açık',
      InProgress: 'Devam ediyor',
      Resolved: 'Çözüldü',
      Closed: 'Kapandı',
    },

    priority: {
      Critical: 'Kritik',
      High: 'Yüksek',
      Medium: 'Orta',
      Low: 'Düşük',
    },

    incidentSource: {
      Manual: 'Elle',
      Telemetry: 'Telemetri',
      Alert: 'Alarm',
    },

    signalStatus: {
      Promoted: 'Terfi etti',
      Weak: 'Zayıf',
      Recorded: 'Yalnızca kaydedildi',
      Deduplicated: 'Tekilleştirildi',
      Suppressed: 'Bastırıldı',
    },

    signalKind: {
      LogBurst: 'patlama',
      RateAnomaly: 'hız anomalisi',
    },

    severity: {
      Fatal: 'Ölümcül',
      Error: 'Hata',
      Warning: 'Uyarı',
      Information: 'Bilgi',
      Debug: 'Hata ayıklama',
      Verbose: 'Ayrıntılı',
    },

    deliveryStatus: {
      Pending: 'Beklemede',
      Sent: 'Gönderildi',
      Failed: 'Başarısız',
    },

    channel: {
      Email: 'E-posta',
      Webhook: 'Webhook',
      Jira: 'Jira',
    },

    telemetryKind: {
      Seq: 'Seq',
    },
  },

  window: {
    label: 'Zaman penceresi',

    option: {
      '30m': 'Son 30 dakika',
      '2h': 'Son 2 saat',
      '24h': 'Son 24 saat',
      '7d': 'Son 7 gün',
    },

    scope: {
      '30m': 'son 30 dakika',
      '2h': 'son 2 saat',
      '24h': 'son 24 saat',
      '7d': 'son 7 gün',
    },

    dayScope: (days: number) => `son ${days} gün`,
  },

  format: {
    justNow: 'az önce',
    minutesAgo: (minutes: number) => `${minutes} dk önce`,
    hoursAgo: (hours: number) => `${hours} sa önce`,
    daysAgo: (days: number) => `${days} g önce`,
    notGiven: 'verilmedi',
    confidence: {
      high: 'yüksek güven',
      moderate: 'orta güven',
      low: 'düşük güven',
    },
  },
}
