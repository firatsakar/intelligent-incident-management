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
    signOut: 'Oturumu kapat',
  },

  nav: {
    skip: 'İçeriğe geç',
    open: 'Menüyü aç',
    sections: 'Bölümler',
    drawer: 'Konsolun bölümleri arasında geçin.',

    groups: {
      operations: 'Operasyon',
      pipeline: 'Boru hattı',
    },

    items: {
      dashboard: 'Panel',
      incidents: 'Olaylar',
      signals: 'Sinyaller',
      evidence: 'Kanıt',
      funnel: 'Huni',
      services: 'Servisler',
      deliveries: 'Teslimatlar',
      settings: 'Ayarlar',
    },
  },

  account: {
    menu: (name: string) => `Hesap — ${name}`,
    unverifiedTitle: 'Doğrulanmamış oturum',
    unverified:
      'Kim olduğunuzu hiçbir şey doğrulamadı. Bu ad yalnızca oturumu etiketliyor; konsolun arkasındaki servisler kendilerine ulaşabilen herkese cevap veriyor.',
  },

  realtime: {
    status: {
      connecting: 'bağlanıyor',
      live: 'canlı',
      reconnecting: 'yeniden bağlanıyor',
      offline: 'çevrimdışı',
    },

    hint: {
      connecting: 'Güncelleme kanalı açılıyor.',
      live: 'Güncellemeler olduğu anda gönderiliyor.',
      reconnecting: 'Güncelleme kanalı düştü, yeniden kuruluyor.',
      offline: 'Ekranlar çalışmaya devam ediyor ama kendiliğinden güncellenmiyor.',
    },

    suffix: '— canlı bağlantı',
  },

  settingsNav: {
    title: 'Ayarlar',
    intro:
      'Profiliniz, bu platformun okuduğu log kaynakları, ve bulduklarını gönderdiği hedefler.',
    sections: 'Ayar bölümleri',
    pages: {
      profile: 'Profil',
      telemetry: 'Telemetri',
      integrations: 'Entegrasyonlar',
    },
  },

  login: {
    tagline: 'Yaptığı işi gösteriyor.',
    taglineDetail:
      'Platform log kaynağınızı izliyor, bir şeyin birini uyandırmaya değip değmediğine kayıt üzerinden karar veriyor, ve sonra vardığı kararı açıklıyor.',

    points: {
      gate: {
        title: 'Okuyabileceğiniz bir kapı',
        detail:
          'Sinyaller, herhangi bir model devreye girmeden önce açık kurallarla puanlanıyor, ve aritmetik olayın üstünde kalıyor — negatif çıkan bileşenler dahil.',
      },
      restraint: {
        title: 'Ve yükseltmedikleri',
        detail:
          'Zayıf ve bastırılmış sinyaller, tespit gecikmesinin yanında kayıtta duruyor; böylece sakin bir saat açıklanamamış değil, sakin okunuyor.',
      },
      routing: {
        title: 'Yönlendirilir, yayınlanmaz',
        detail:
          'Biten bir analiz, önceliğe göre süzülerek yapılandırdığınız hedeflere ulaşıyor, ve her deneme kendi teslimat sonucunu saklıyor.',
      },
    },

    heading: 'Oturum aç',
    subheading: 'Bu oturumun altında çalışacağı adı seçin.',
    organisation: 'Organizasyon',
    ownership:
      'Olaylar, sinyaller ve entegrasyonlar onları açan kişiye değil organizasyona ait. Bu sürümde tek bir organizasyon var.',
    nameLabel: 'Adınız',
    nameHint: 'Bu oturumu konsolda etiketler. Hiçbir şey onu doğrulamaz.',
    nameRequired: 'Bir ad girin. Yalnızca bu oturumu etiketlemek için kullanılıyor.',
    noPasswordTitle: 'Parola yok, çünkü onu doğrulayacak bir şey yok',
    noPassword:
      'Bu sürümde kimlik doğrulama yok. Bu konsolun arkasındaki servisler kendilerine ulaşabilen herkese cevap veriyor, ve buradan giriş yapmak yalnızca oturumun kimin adını taşıyacağını belirliyor. Gerçek oturum açma gateway ile geliyor.',
    submit: 'Konsola gir',
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
