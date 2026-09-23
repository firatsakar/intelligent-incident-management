import type { Dictionary } from './en'

/**
 * Türkçe.
 *
 * Several entries take a parameter they never use — `_count`, `_word`, `_signatureCount`. That is
 * not dead weight: Turkish does not inflect a noun after a numeral ("3 sinyal", never
 * "3 sinyaller"), so a count English needs in order to choose a word form is simply not part of
 * the Turkish sentence. The parameter stays because the signature is shared with `en.ts` and has
 * to remain assignable to it; the underscore says the omission is deliberate.
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

  session: {
    defaultOrganization: 'Varsayılan organizasyon',
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


  incidents: {
    list: {
      title: 'Olaylar',
      intro: 'Platformun açtığı her şey — elle ya da kendiliğinden.',
      matching: (count: number) => `${count} kayıt bu filtrelere uyuyor`,
      onRecord: (count: number) => `Kayıtta ${count} olay`,

      filterStatus: 'Duruma göre süz',
      filterPriority: 'Önceliğe göre süz',
      anyStatus: 'Her durum',
      anyPriority: 'Her öncelik',
      anyStatusInline: 'herhangi bir durumda',
      anyPriorityInline: 'herhangi bir öncelikte',
      clear: 'Temizle',
      clearFilters: 'Filtreleri temizle',

      caption: 'Olaylar, en yenisi başta. Her satır kendi olayına bağlanıyor.',
      columns: {
        priority: 'Öncelik',
        incident: 'Olay',
        source: 'Kaynak',
        status: 'Durum',
        analysis: 'Analiz',
        detected: 'Tespit',
        latency: 'Gecikme',
      },

      loadError: 'Olaylar yüklenemedi',

      emptyFilteredTitle: 'Bu filtrelere uyan bir şey yok.',
      emptyFiltered: (status: string, priority: string) =>
        `Kayıtta olaylar var; hiçbiri aynı anda hem ${status} hem ${priority} değil.`,
      emptyTitle: 'Kayıtta hiç olay yok.',
      empty: 'Elle açılmış bir şey yok, ve henüz hiçbir şey bir tespit kuralını aşmadı.',

      page: (current: number, total: number) => `Sayfa ${current} / ${total}`,
      previous: 'Önceki',
      next: 'Sonraki',

      analysisFailed: 'analiz başarısız',
      awaitingAnalysis: 'analiz bekleniyor',
      analysed: 'analiz edildi',
      toOpen: (duration: string) => `açılışa +${duration}`,
      openedByHand: 'Elle açıldı — bunu hiçbir şey tespit etmedi',
    },

    detail: {
      loadErrorTitle: 'Bu olay yüklenemedi',
      unknownError: 'Bilinmeyen hata',
      started: (relative: string) => `${relative} başladı`,

      statusLabel: 'Olay durumu',
      assignPlaceholder: 'Bir ekip ata',
      reassignPlaceholder: 'Başka ekibe ata…',
      assignLabel: 'Bir ekip ata',
      reassignLabel: 'Başka bir ekibe ata',
      assign: 'Ata',
      assigning: 'Atanıyor…',

      whatHappened: 'Ne oldu',
      fromDetector:
        'Tespit eden tarafından doğrudan log kayıtlarından yazıldı — analizin okuduğu metnin aynısı.',
      fromOperator: 'Olay açılırken girildiği gibi.',

      openedByHand: 'Elle açıldı',
      openedByHandDetail:
        'Bunu hiçbir şey tespit etmedi, yani ölçülecek bir tespit gecikmesi de yok — platform fark etmedi, kendisine söylendi. Aşağıdaki puan dökümünün olmamasının sebebi de aynı.',
      problemStarted: 'Sorun başladı',
      sourceClock: 'kaynağın saatiyle',
      incidentOpened: 'Olay açıldı',
      ourClock: 'bizimkiyle',
      detectionLatency: 'Tespit gecikmesi',
      clockDisagreement: 'Saat uyuşmazlığı',
      latencyHintLabel: 'Tespit gecikmesi neyi ölçüyor',
      latencyHint:
        'Kaynağın damgaladığı ilk log satırından bu kaydın açıldığı ana kadar — log deposunun saatinden bizimkine. İçinde poll aralığı, tespit geçişi ve puanlama var; platformun bunu kendi başına fark etmek için harcadığı sürenin tamamı bu.',
      skewHint:
        'Kaynak, bunun biz kaydı açtıktan sonra başladığını bildiriyor; bu ancak iki saatin uyuşmadığı anlamına gelir. Gördüğünüz rakam bir gecikme değil, o uyuşmazlığın büyüklüğü.',
      noticed: 'söylenmeden fark edildi',
      skewNote: 'kaynağın saati bizimkinin ilerisinde',
    },

    timeline: {
      title: 'Zaman çizelgesi',
      description:
        'Servislerin ayrı ayrı kaydettiği beş an. Bir zaman eksikse, bu ekrandan değil kayıttan eksiktir.',

      problemStarted: 'Sorun başladı',
      onSourceClock: 'Kaynağın saatiyle, bizimkiyle değil.',
      notRecorded: 'Kaydedilmedi — bu olay elle açıldı.',

      incidentOpened: 'Olay açıldı',
      toDetect: (duration: string) => `tespit için +${duration}`,
      ourClockGap: 'Bizim saatimiz. Yukarıdaki aralık tespitin maliyeti.',

      analysisApplied: 'Analiz uygulandı',
      categorised: (category: string, priority: string) =>
        `${category} olarak sınıflandı, öncelik ${priority} yapıldı`,
      applied: 'Uygulandı.',
      analysisFailed: 'Analiz çalıştı ve hiçbir şey döndürmedi. Sebebi için panele bakın.',
      analysisWaiting: 'Analiz servisi bekleniyor.',

      peopleNotified: 'İnsanlara haber verildi',
      afterOpening: (duration: string) => `açılıştan +${duration} sonra`,
      channelsDelivered: (sent: number, total: number) =>
        `${total} kanaldan ${sent} tanesi teslim etti`,
      noDeliveryYet: 'Henüz kayıtlı teslimat yok.',
      everyChannelFailed:
        'Yapılandırılmış her kanal başarısız oldu — bildirimler paneline bakın.',

      lastChanged: 'Son değişiklik',
      anyEdit: 'Herhangi bir değişiklik — durum, ekip, ya da analizin inmesi.',

      doneUntimed: 'yapıldı · zamanı kaydedilmedi',
      notYet: 'henüz değil',
    },

    score: {
      title: 'Kapı bunu nasıl puanladı',
      description:
        'Bir yargı değil, belirlenimci bir puan. Karar sonradan tartışılabilsin diye her terim kayıtta.',

      total: 'Toplam',
      totalNote: 'yukarıdaki terimlerin toplamı, 1.00 ile sınırlı',
      confidence: 'Güven',
      confidenceNote: 'puanlama atlandı',

      defaultReason: 'Kendi tespit kuralının terfi eşiğini karşıladı.',

      signature: 'İmza',
      muted: 'susturulmuş',
      occurrences: (count: number, promotions: number, real: number, falsePositive: number) =>
        `toplam ${count} kez görüldü · ${promotions}× terfi, ${real} gerçek olduğu doğrulandı, ${falsePositive} yanlış pozitif`,

      measuresLabel: (term: string) => `${term} neyi ölçüyor`,
    },

    notifications: {
      title: 'Bildirimler',
      summary: (sent: number, total: number) => `${total} teslimattan ${sent} tanesi ulaştı`,
      failed: (count: number) => `${count} başarısız`,
      empty:
        'Henüz bir şey gönderilmedi. Bildirimler analiz tamamlanınca, filtreleri uyan her etkin entegrasyona gidiyor.',
      deletedIntegration: 'silinmiş entegrasyon',
      took: (duration: string) => `${duration} sürdü`,
      queued: (when: string) => `${when} kuyruğa girdi`,
      attempts: (count: number) => `${count} deneme`,
    },

    analysis: {
      title: 'AI analizi',
      description:
        'Belirlenimci kapının üstüne eklenen zenginleştirme — kategoriyi ve önceliği o belirledi, bunun yükseltilip yükseltilmeyeceğini değil.',

      failedDescription:
        'Analiz çalıştı ve bir sonuç üretmedi. Kendiliğinden gelecek başka bir şey yok — aşağıdaki öncelik ve kategori tespitin koyduğu değerler.',
      failedTitle: 'Analiz başarısız',
      failedFooter: 'Sonraki başarılı bir deneme bunu temizler ve paneli doldurur.',

      waiting:
        'Analiz servisi bekleniyor. Olayın üstünde taşınan kanıt özetini okuyor, yani onun adına fazladan bir çağrı yapılmıyor.',

      confidence: 'Güven',
      confidenceHintLabel: 'Güven rakamı ne anlama geliyor',
      confidenceHint:
        'Analizin kendi kategorisinden ve önceliğinden ne kadar emin olduğu — olayın ne kadar ciddi olduğu değil, ve kapının bir şeyin bozulduğundan ne kadar emin olduğu da değil. O ikincisi soldaki puan.',
      noConfidence:
        'Analiz buna bir sayı koymadı. Bu emin olmamakla aynı şey değil — sayısallaştırmayı reddetti, yani çizilecek bir şey yok.',

      reasoning: 'Gerekçe',
    },
  },

  scoreTerms: {
    label: {
      fatal: 'ölümcül hata',
      burstBase: 'patlama tabanı',
      overThreshold: 'eşik aşımı',
      rateAnomaly: 'hız anomalisi',
      precedent: 'emsal',
      blastRadius: 'etki alanı',
      falsePositivePrecedent: 'yanlış pozitif geçmişi',
      muted: 'susturulmuş imza',
    },

    help: {
      fatal:
        'Süreç çöktü. Çökme bir yargı meselesi değil, o yüzden puanlamayı tamamen atlayıp doğrudan geçiyor.',
      burstBase: 'Her patlamanın, tespit kuralını aşmış olmaktan aldığı başlangıç puanı.',
      overThreshold:
        'Patlamanın kuralın eşiğini ne kadar aştığı — katlanarak sayılıyor ve bir tavanı var: iki katı anlamlı şekilde daha kötü, elli katı değil.',
      rateAnomaly:
        'İmzanın kendi hız geçmişi, bu hacmin onun için olağandışı olduğunu söylüyor. İkinci bir veri kaynağı olmadan elde edilebilecek en güçlü destek.',
      precedent: 'Bu imza daha önce gerçek olduğu doğrulanmış bir olay üretti.',
      blastRadius: 'Bunu bir servis değil, iki ya da daha fazlası bildiriyor.',
      falsePositivePrecedent:
        'Bu imza daha önce yanlış pozitif olarak işaretlendi, o yüzden puan aşağı çekiliyor.',
      muted:
        'Birisi bu imzayı susturmuş. Yine de puanlanıyor, ve susturulmuş olduğu için ağır ceza alıyor.',
    },
  },


  telemetry: {
    otherSignatures: 'diğer imzalar',
    unknownError: 'bilinmeyen hata',
    unknownService: 'bilinmeyen servis',

    signals: {
      title: 'Sinyaller',
      intro:
        'Tespit kapısının baktığı her şey — kimseyi uyandırmamaya karar verdikleri dahil.',
      loadError: 'Sinyaller yüklenemedi',

      allSignals: 'Tüm sinyaller',
      shown: (count: number) => `${count} gösteriliyor`,
      inWindow: (total: number) => `bu pencerede ${total} tane`,
      loaded: (loaded: number, total: number) => `${total} sinyalden ${loaded} tanesi yüklü`,
      chips: 'çipler kapının puan bileşenleri, güven ise onların toplamı',
      clearFilter: 'Filtreyi temizle',

      emptyTileTitle: 'Bu kutucuk için sinyal yok.',
      emptyTile:
        'Mevcut pencerede bu kutucuğa uyan bir şey yok. Bağlantı başka bir pencereye göre üretilmiş olabilir, ya da imzalar o zamandan beri yeniden sıralanmış olabilir.',
      emptyWindowTitle: 'Bu pencerede sinyal yok.',
      emptyWindow:
        'Henüz hiçbir şey bir tespit kuralını aşmadı. Sakin bir pencere ile okunmayan bir kaynak buradan aynı görünür — hangisi olduğunu Ayarlar › Telemetri söyler.',

      loadMore: (count: number) => `${count} tane daha yükle`,
      notLoaded: (count: number) =>
        `Bu pencerede ${count} daha eski sinyal yüklü değil, yani yukarıdaki harita onları saymıyor.`,
      ceiling: (max: number, total: number, remaining: number) =>
        `Bu uç bir seferde en fazla ${max} tane veriyor, bu pencerede ise ${total} var. Geri kalanı görmenin yolu daha kısa bir pencere — kalan ${remaining} tanesi yukarıdaki her şeyden daha eski.`,

      occurrences: (count: number) => `${count} kez`,
      viewIncident: 'Olaya git',
    },

    heatmap: {
      title: 'Hatalar nerede',
      description:
        'Her kutucuk bir hata imzası. Boyut da renk de ne sıklıkta tetiklendiği — en büyük ve en kırmızı sol üstte — köşedeki işaret kapının onu nereye kadar götürdüğü, ve bir kutucuk kendisine ait bir sinyal geldiğinde kendini çerçeveliyor.',
      partial: (loaded: number, total: number) =>
        `Bu pencerenin ${total} sinyalinden en yeni ${loaded} tanesiyle kuruldu — aşağıdaki Daha fazla yükle bunu genişletir.`,
      empty: 'Bu pencerede sinyal yok. Henüz hiçbir şey bir tespit kuralını aşmadı.',

      allServices: 'Tüm servisler',
      counts: (services: number, tiles: number, occurrences: number) =>
        `${services} servis · ${tiles} kutucuk · ${occurrences} kez`,

      band: {
        Promoted: 'bir olay açtı',
        Deduplicated: 'açık bir olayın içine sayıldı',
        Weak: 'zayıf — gösterildi, yükseltilmedi',
        Recorded: 'yalnızca kaydedildi',
        Suppressed: 'bastırıldı (susturulmuş imza)',
      },

      tile: (
        service: string,
        label: string,
        occurrences: number,
        signals: number,
        band: string,
      ) => `${service} · ${label} — ${signals} sinyalde ${occurrences} kez — ${band}`,
      justUpdated: ' — az önce güncellendi',

      panelCounts: (_occurrences: number, signals: number) => `kez · ${signals} sinyal`,
      incidentLink: 'Olay →',

      legendOccurrences: 'görülme',
      noMark: 'işaretsiz — yalnızca kaydedildi',
      legendFlash: 'son birkaç saniyede güncellendi',
      unplaced: (count: number) =>
        `${count} sinyal yerleştirilemedi — imzaları artık yok.`,
    },

    evidence: {
      title: 'Kanıt',
      intro: 'Ham malzeme: ne loglandı, neye toplandı, ve bu ne üretti.',
      loadError: 'Kanıt yüklenemedi',

      filterService: 'Servise göre süz',
      clearService: 'Servis filtresini temizle',

      logRecords: 'Log kayıtları',
      showingRecent: (shown: number, total: number) =>
        `${total} kayıttan en yeni ${shown} tanesi gösteriliyor`,
      inWindow: (total: number) => `bu pencerede ${total} tane`,
      logListLabel: 'Bu penceredeki log kayıtları',
      emptyLogTitle: 'Bu pencerede loglanan bir şey yok.',
      emptyLogForService: (service: string) =>
        `Pencerede "${service}" kaynağından kayıt yok. Ya sessizdi, ya da bu adla hiçbir şey okunmuyor — adın kaynağın bildirdiğiyle birebir eşleşmesi gerekiyor.`,
      emptyLog:
        'Tespit kendi log deponuzdan belirli aralıklarla okuyor, yani boş bir pencere ya sessiz bir dönem ya da okunmayan bir kaynak demek.',

      signatures: 'İmzalar',
      signaturesCount: (count: number) =>
        `Aşağıdaki sinyallerin arkasındaki ${count} ayrı hata`,
      signaturesTruncated: ' — pencerenin tamamının değil, gösterilenlerin arkasındaki',
      signaturesAllTime: '. Her birinin sayaçları bu pencereyi değil tüm zamanı kapsıyor.',
      signaturesListLabel: 'Bu penceredeki sinyallerin arkasındaki imzalar',
      emptySignaturesTitle: 'Burada imza yok.',
      emptySignatures:
        'Bir imza, bir hata ilk kez normalize edildiğinde oluşur; yani boş bir liste penceredeki hiçbir şeyin hata olmadığı anlamına gelir.',

      signals: 'Sinyaller',
      signalsDescription: 'Kapının bu penceredeki o imzalardan çıkardığı sonuç',
      signalsRecent: (shown: number, total: number) => `${total} taneden en yeni ${shown} tanesi`,
      signalsListLabel: 'Bu penceredeki sinyaller',
      emptySignalsTitle: 'Burada sinyal yok.',
      emptySignals:
        'Hatalar loglandı ama hiçbir patlama bir tespit kuralını aşmadı, yani kapının karar verecek bir şeyi olmadı.',

      arrived: (records: number) =>
        `Bu pencere okunduğundan beri ${records} yeni log kaydı alındı`,
      acrossPolls: (polls: number) => `, ${polls} poll boyunca`,
      allServicesNote: ' Yalnızca burada süzülen servis için değil, tüm servisler için sayıldı.',
      reread: 'Pencereyi yeniden oku',
      rereading: 'Yeniden okunuyor…',

      clockSkew: 'saat kayması',
      ingestionLag: 'Alım gecikmesi — kaynağın zaman damgasından bizimkine',
      muted: 'susturulmuş',
      signatureCounts: (
        total: number,
        promotions: number,
        real: number,
        falsePositive: number,
      ) =>
        `toplam ${total} · ${promotions}× terfi · ${real} gerçek, ${falsePositive} yanlış`,
      signalCounts: (occurrences: number, when: string) => `${occurrences} kez · ${when}`,
    },

    funnel: {
      title: 'Sinyal hunisi',
      intro:
        'Platformun okuduğu her şey, neyi bir araya katladığı, ve bunun ne kadarını birini uyandırmaya değmez bulduğu.',
      loadError: 'Huni yüklenemedi',

      notRaised: 'Yükseltilmedi',
      notRaisedDescription: (scope: string) =>
        `Kapının puanlayıp bilerek olduğu yerde bıraktığı sinyaller — ${scope}.`,
      ofScored: (signals: number) => `/ kapının puanladığı ${signals} sinyal`,
      heldBack: 'geri tutuldu',
      actedOn: 'işlem yapıldı',

      zeroHeldBack:
        'Kapı bu pencerede puanladığı her şeye işlem yaptı — {count} tanesinin hepsi eşiği aştı, yani geri tutulacak bir şey kalmadı. {emphasis} Neye baktığı yanındaki sayı, ve aşağıdaki aşamalar.',
      zeroEmphasis:
        'Buradaki sıfır, kapının hiçbir şeyi reddetmediği anlamına gelir — hiçbir şeye bakmadığı değil.',
      someHeldBack: (notRaised: string, signals: string) =>
        `${signals} sinyalden ${notRaised} tanesi puanlandı ve olduğu yerde bırakıldı: olay yok, çağrı yok, e-posta yok. Bir alerting kuralının yapamayacağı şey tam olarak bu — geri okuyabileceğiniz bir aritmetiğe dayanarak, bunun bir insana değmediğine karar vermek.`,
      deduplicated: (count: number) =>
        `${count} tekilleştirilmiş sinyal, geri tutulmuş değil işlem yapılmış sayılıyor. Her biri zaten açık olan bir olayın içine katlandı, yani birisi uyandırıldı — sadece daha önce.`,

      nothingScored: 'Bu pencerede hiçbir şey puanlanmadı.',
      nothingScoredRecords: (records: string, _count: number) =>
        `${records} log kaydı geldi ve hiçbiri bir tespit kuralını aşmadı, yani hiçbir patlama puanlamaya ulaşmadı. Buradaki süzme, bu sayının ölçtüğünden bir aşama önce oldu — onu okuyacağınız yer aşağıdaki aşamalar.`,
      nothingArrived:
        'Bu pencerede hiç telemetri gelmedi, yani kapının bakacak bir şeyi olmadı. Sakin bir pencere ile okunmayan bir kaynak buradan aynı görünür — hangisi olduğunu Ayarlar › Telemetri söyler.',

      stagesTitle: 'Log kayıtlarından sinyallere',
      stagesDescription: (scope: string) =>
        `Üç aşama, tek ölçekte — ${scope}. Farklı şeyler sayıyorlar: kayıtlar log satırları, imzalar onlardan kesilmiş ayrı parmak izleri, sinyaller ise kapıdan puanlaması istenen patlamalar.`,
      stageLogRecords: 'Log kayıtları',
      stageSignatures: 'İmzalar',
      stageSignals: 'Sinyaller',
      stagesChartLabel: (records: string, signatures: string, signals: string) =>
        `Boru hattı aşamaları. ${records} log kaydı, ${signatures} imza, ${signals} sinyal.`,

      nothingToFingerprint: 'Hiçbir şey gelmedi, yani parmak izi çıkarılacak bir şey de yoktu.',
      nothingFingerprinted:
        'Bu pencerede hiçbir şeyin parmak izi çıkarılmamış, ki bu olmamalı — kayıtlar imzasız gelmiş.',
      folding: (perSignature: string, signatures: string, records: string, _count: number) =>
        `İmza başına yaklaşık ${perSignature} kayıt. Parmak izinin kazandırdığı katlama bu: kapı ${records} şey üzerine değil, ${signatures} şey üzerine akıl yürütüyor.`,

      neverScored:
        'Hiçbir patlama bir tespit kuralını aşmadı, yani kapıdan hiçbir zaman puanlama istenmedi.',
      bursting: (
        signatures: string,
        _signatureCount: number,
        signals: string,
        _signalCount: number,
      ) =>
        `${signatures} imza, kapının puanlaması için ${signals} patlama üretti.`,
      burstingWider:
        ' Bir imza birden fazla kez tetiklenebilir; bu aşamanın üstündekinden dar değil geniş olmasının sebebi bu.',

      whatArrived: 'Ne geldi',
      noLogRecord: 'Bu pencereye hiç log kaydı gelmedi.',

      verdictsTitle: 'Kapı nasıl karar verdi',
      verdictsDescription: (scope: string) =>
        `Kapının verebileceği her karar, ve her birine kaç tanesinin düştüğü — ${scope}.`,
      noVerdicts: 'Kapı bu pencerede hiçbir şey puanlamadı, yani bunların hiçbirine ulaşmadı.',

      wokenHeading: 'Birisi uyandırıldı',
      wokenNote: 'Geri tutulmuş sayılmıyor.',
      notWokenHeading: 'Kimse uyandırılmadı',
      notWokenNote: '"Yükseltilmedi" tam olarak bu üçünü sayıyor.',

      verdict: {
        Promoted: 'Çizgiyi aştı, ve kapı onun için bir olay açtı.',
        Deduplicated: 'Zaten açık olan bir olaya katlandı. Birisi uyandırıldı — daha önce.',
        Weak: 'Puanlandı, ve çizginin altında puanlandı. Görebileceğiniz yerde tutuldu; kimse aranmadı.',
        Recorded: 'Kayıt için tutuldu, fazlası değil.',
        Suppressed: 'İmza susturulmuş, yani kapı onu puanladı ve sonra bilerek sustu.',
      },
    },

    services: {
      title: 'Servis sağlığı',
      intro: 'Hataların nereden geldiği, ve boru hattında nereye kadar çıktığı.',
      produced: (count: string, _raw: number) =>
        `Bu pencerede ${count} servis bir şey üretti.`,
      loadError: 'Servis sağlığı yüklenemedi',

      caption: (scope: string) =>
        `Servis başına log hacmi, sinyaller ve olaylar — ${scope}. En sık imza dışındaki her kolona göre sıralanabilir.`,

      columnService: 'Servis',
      columnLogRecords: 'Log kaydı',
      columnSignals: 'Sinyal',
      columnPromoted: 'Terfi',
      columnIncidents: 'Olay',
      columnTopSignature: 'En sık imza',
      columnLastSignal: 'Son sinyal',

      emptyTitle: 'Bu pencereye hiçbir şey gelmedi.',
      empty:
        'Hiçbir servis log kaydı yazmadı ve hiçbir şey bir tespit kuralını aşmadı. Gerçekten sakin bir pencere ile okunmayan bir telemetri kaynağı buradan aynı görünür — hangisi olduğunu Ayarlar › Telemetri söyler.',

      goneHintLabel: '(imza silinmiş) ne demek',
      goneHint:
        'Bu sinyaller tespit edildi, ama arkalarındaki hata imzası o zamandan beri silindi — ve servis adı imzanın üstünde duruyordu. Sayılar gerçek; ait oldukları ad geri getirilemez.',

      folded: (records: string, promoted: string, incidents: string) =>
        `${records} kayıt · ${promoted} terfi · ${incidents} olay · `,
      occurrences: (_count: number) => 'kez',
      noSignal: 'Bu pencerede bu servisten sinyal yok',

      signatureGone: 'imza artık kayıtta değil',
      nothingCrossed: 'bu pencerede hiçbir şey bir tespit kuralını aşmadı',

      footnote:
        'Sinyal ve imza tarafından sayıldı. {incidents}, bu servisin sinyallerinin ulaştığı ayrı olay sayısı; yani elle açılmış bir olay hiçbir servise atfedilmiyor — bir olay kaydı servis taşımıyor, ve başlıktan okumaya çalışmak tahmin olurdu.',
    },
  },


  dashboard: {
    title: 'Panel',
    intro: 'Şu anda ne açık, ne geldi, ve bunun ne kadarını platform kendi buldu.',
    days: (count: number) => `${count} gün`,
    loadError: 'Rakamlar yüklenemedi',

    open: {
      title: 'Şu anda açık',
      description:
        'Ne kadar eski olursa olsun hâlâ Açık ya da Devam ediyor olan her olay. Altı hafta önce açılmış ve hiç kapanmamış olan, en çok görmeniz gereken olaydır; o yüzden bu sayım pencereyi bilerek yok sayıyor.',
      notWindowed: 'pencereden bağımsız',
      open: 'açık',
    },

    byDate: {
      title: 'Tarihe göre olaylar',
      description:
        'Olayların ne zaman açıldığı, önceliğe göre yığılmış — {scope}. Günler sunucuda sizin diliminizde değil {utc} olarak kesiliyor, ve son kolon bugün — hâlâ doluyor.',
    },

    detection: {
      title: 'Tespit',
      description: (scope: string) =>
        `Platformun ne kadarını kendisine söylenmeden fark ettiği, ve fark ettiklerini açmasının ne kadar sürdüğü — ${scope}, UTC.`,
      empty:
        'Bu pencerede olay yok, yani fark edilmiş olacak bir şey de yok. Daha uzun bir pencere deneyin.',
      share: 'platformun kendisi fark etti',
      noticed: 'Fark edildi',
      filed: 'Elle açıldı',
      median: 'Medyan gecikme',
      p95: '95. yüzdelik',
      nothingNoticed:
        'Bu pencerede hiçbir şey otomatik olarak fark edilmedi, yani ölçülecek bir gecikme yok. Bu sıfır gecikme değil — gecikmenin yokluğu.',
      allSkewed:
        'Bu penceredeki her tespit, kayıt sorun başlamadan önce açılmış olarak döndü; bu bir gecikme değil, iki saatin uyuşmaması. O satırlar yüzdeliklerin dışında kalıyor.',
    },

    sources: {
      title: 'Olaylar nereden geliyor',
      description: (scope: string) => `Her olayın nasıl açıldığı — ${scope}, UTC.`,
      empty: 'Bu pencerede hiçbir kaynaktan olay yok.',
      meaning: {
        Telemetry: 'platform onu log akışında buldu',
        Alert: 'dışarıdan bir alarm onu yükseltti',
        Manual: 'birisi onu elle açtı',
      },
    },

    latest: {
      title: 'Son olaylar',
      description: (rows: number) =>
        `Ne zaman açılmış olursa olsun en yeni ${rows} tanesi. Soketten geliyor — yenileme yok, pencere yok.`,
      loadError: 'Olaylar yüklenemedi',
      emptyTitle: 'Kayıtta hiç olay yok.',
      empty: 'Elle açılmış bir şey yok, ve henüz hiçbir şey bir tespit kuralını aşmadı.',
      all: (total: number) => `${total} olayın tamamı`,
    },

    chart: {
      summary: (total: number, days: number) => `${days} günde ${total} olay açıldı`,
      busiest: (count: number) => ` · en yoğun gün ${count}`,
      hint: ' · tek bir gün için grafiğin üstüne gelin ya da odaklayın',
      dayTotal: (total: number) => `${total} olay`,
      ariaLabel: (days: number) =>
        `${days} gün boyunca UTC günü başına açılan olaylar. Günleri tek tek okumak için ok tuşlarını kullanın.`,
      emptyPlot: 'Bu pencerede hiçbir şey açılmadı. İçindeki her gün boş.',
      caption: 'UTC günü başına açılan olaylar, önceliğe göre.',
      columnDay: 'Gün (UTC)',
      columnTotal: 'Toplam',
      legendNote: 'Kritik her çubuğun tabanında',
    },
  },


  deliveries: {
    title: 'Teslimat sağlığı',
    intro:
      'Her kanalın ulaşıp ulaşmadığı — tek bir olay üzerinden değil, pencerenin tamamında.',
    loadError: 'Teslimat sağlığı yüklenemedi',

    deleted: 'Silinmiş entegrasyon',

    totals: {
      title: 'Teslimatlar',
      description: (scope: string) =>
        `Bu platformun denediği her bildirim — ${scope}.`,
      emptyLead: 'Bu pencerede hiçbir şey gönderilmedi. ',
      empty:
        'Bildirimler bir analiz bittiğinde çıkıyor; yani içinde hiç olay olmayan bir pencere ile durmuş bir dağıtıcı buradan birebir aynı görünür. Hangisi olduğunu olaylar ekranı söyler.',

      failedOf: (total: string, _totalCount: number, channels: string, _channelCount: number) =>
        `/ ${channels} entegrasyonda toplam ${total} teslimat başarısız oldu`,

      failed: 'başarısız',
      sent: 'gönderildi',
      pending: 'beklemede',

      someFailing: (failing: string, _count: number) =>
        `Bu pencerede ${failing} entegrasyon bir başarısızlık kaydetti. Hangisi, ne zaman, ve kanalın ne cevap verdiği aşağıdaki satırlarda.`,
      allThrough: 'Bu penceredeki her teslimat ulaştı. ',
      stillQueued: (pending: string, _count: number) =>
        `${pending} teslimat hâlâ kuyrukta ve henüz denenmedi — kuyrukta olmak gönderilmiş olmak değil.`,
      nothingQueued: 'Kuyrukta bir şey yok, bekleyen bir şey de yok.',
    },

    dispatch: {
      title: 'Gönderim süresi',
      description: (scope: string) =>
        `Her kanalın bir bildirimi kabul etmesinin ne kadar sürdüğü, medyan — ${scope}. Teslimatın yazılmasından kanalın onu onaylamasına kadar ölçülüyor.`,
      empty: 'Bu pencerede hiçbir şey gönderilmedi, yani süresi ölçülecek bir şey de yok.',
      noneSucceededLead: 'Bu pencerede hiçbiri başarılı olmadı, ',
      noneSucceeded:
        'yani çizilecek bir gönderim süresi yok. Bu sıfır gönderim süresi değil — sürenin yokluğu.',
      chartLabel: (scope: string, detail: string) =>
        `Entegrasyon başına medyan gönderim süresi, ${scope}. ${detail}.`,
      dashNote:
        'Uzun tire, o entegrasyon için bu pencerede hiçbir şeyin başarılı olmadığı anlamına gelir, yani medyanı yok. Bu sıfır gönderim süresi değil.',
    },

    verdict: {
      failing: 'Başarısız',
      recovered: 'Toparladı',
      delivering: 'Teslim ediyor',
      queued: 'Kuyrukta',
      silent: 'Gönderim yok',
    },

    byIntegration: {
      title: 'Entegrasyona göre',
      description: (scope: string) =>
        `Teslimat denemiş her entegrasyon için bir satır — ${scope}. Sunucunun döndürdüğü sırada, yani en kötüsü başta.`,
      emptyTitle: 'Hiçbir entegrasyon teslimat denemedi.',
      empty:
        'Bir entegrasyon burada ancak raporlayacak bir şeyi olduğunda görünür. Yapılandırılmış ve etkin ama bu pencerede hiç ulaşılmamış olan bu listede değildir — neyin var olduğunun listesi Ayarlar › Entegrasyonlar.',
      medianNote:
        'Medyan gönderimin — olması, o entegrasyon için bu pencerede hiçbir şeyin başarılı olmadığı anlamına gelir. Bu bir ölçümün yokluğu, sıfır ölçümü değil.',

      disabled: 'devre dışı',
      deletedNote:
        'Bu entegrasyon silindi. Teslimatları bilerek saklanıyor — birine haber verildiğinin kaydı onlar — yani aşağıdaki sayılar hâlâ doğru, ait oldukları ad ve kanal ise yok.',
      id: (short: string) => `id ${short}`,
      lastFailure: 'Son başarısızlık',

      sent: 'Gönderildi',
      failed: 'Başarısız',
      pending: 'Beklemede',
      median: 'Medyan gönderim',
      lastSent: 'Son gönderim',
    },
  },


  settings: {
    shared: {
      availableNow: 'Şu anda mevcut',
      comingSoon: 'Yakında',
      counts: (total: number, enabled: number) => `${total} bağlı · ${enabled} etkin`,
      connected: ' bağlı',
      notConnected: 'Bağlı değil.',
      test: 'Test et',
      testing: 'Test ediliyor…',
      edit: 'Düzenle',
      cancel: 'Vazgeç',
      deleting: 'Siliniyor…',
      paused: 'Duraklatıldı',
      name: 'Ad',
      dismissTest: 'Test sonucunu kapat',
      testReport: '{status} · {time} — {detail}',
      secretKept: 'Boş bırakılan bir sır, saklanan değeri korur.',
      saving: 'Kaydediliyor…',
      saveChanges: 'Değişiklikleri kaydet',
      connect: 'Bağlan',
      enabledSwitch: (name: string) => `${name} etkin`,
      deleteAria: (name: string) => `${name} sil`,
      deleteTitle: (name: string) => `“${name}” silinsin mi?`,
    },

    config: {
      keepCurrent: 'Mevcut değeri korumak için boş bırakın',
      strayIntegration: 'Bu entegrasyonda saklı; bu form onun şeklini bilmiyor.',
      straySource: 'Bu kaynakta saklı; bu form onun şeklini bilmiyor.',

      fields: {
        'email.host': 'SMTP sunucusu',
        'email.port': 'Port',
        'email.from': 'Gönderen',
        'email.to': 'Alıcı',
        'email.username': 'Kullanıcı adı',
        'email.password': 'Parola',
        'webhook.url': 'URL',
        'webhook.timeout': 'Zaman aşımı (saniye)',
        'webhook.authorization': 'Authorization başlığı',
        'jira.baseUrl': 'Temel URL',
        'jira.projectKey': 'Proje anahtarı',
        'jira.email': 'Hesap e-postası',
        'jira.apiToken': 'API token',
        'jira.issueType': 'Issue tipi',
        'seq.url': 'Seq URL',
        'seq.apiKey': 'API anahtarı',
        'seq.filter': 'Filtre',
        'seq.serviceProperty': 'Servis alanı',
        'seq.initialLookback': 'İlk geriye bakış (dakika)',
      },

      hints: {
        'webhook.authorization':
          'Header: ön ekiyle başlayan her ayar bir istek başlığı olarak gönderilir.',
        'seq.apiKey':
          'Yalnızca Seq örneğinde kimlik doğrulama açıksa gerekir. Anahtarın Read yetkisi olmalı.',
        'seq.filter':
          'Seq filtre ifadesi. Boş bırakılırsa connector error ve fatal kayıtları okur.',
        'seq.serviceProperty':
          'Bir log satırının hangi servisten geldiğini söyleyen event alanının adı.',
        'seq.initialLookback':
          'İlk poll ne kadar geriye bakar. Sonraki pollar sonuncunun bıraktığı yerden devam eder.',
      },
    },

    integrations: {
      title: 'Entegrasyonlar',
      intro:
        'Bir analiz tamamlandığında bildirimin nereye gideceği. Bir hedef türü birden çok entegrasyon tutabilir — farklı filtrelere sahip iki E-posta kaydı normal bir kurulumdur.',
      loadError: 'Entegrasyonlar yüklenemedi.',

      silenceNone: 'Hiçbir şey bağlı değil. Bir analiz tamamlandığında kimseye haber verilmez.',
      silenceOne:
        'Tek entegrasyon duraklatılmış. Bir analiz tamamlandığında kimseye haber verilmez.',
      silenceMany: (total: number) =>
        `${total} entegrasyonun hepsi duraklatılmış. Bir analiz tamamlandığında kimseye haber verilmez.`,

      comingSoonNote:
        'Henüz arkasında kod olmayan planlanmış hedefler — burada yapılandırılacak bir şey yok. Onlar gelene kadar, kendi uç noktanıza Webhook üzerinden ulaşılabilir; Webhook herhangi bir sağlayıcının payload formatını değil, bu platformun kendi JSON’unu gönderir.',

      addAnother: (name: string) => `Bir ${name} daha ekle`,
      connectOne: (name: string) => `${name} bağla`,

      summary: {
        Email: 'Bir posta kutusuna ya da dağıtım listesine SMTP.',
        Webhook: 'Olayın ve analizinin, sizin kontrol ettiğiniz bir uca HTTP POST edilmesi.',
        Jira: 'Bir projede issue açar, gerekçe de açıklamasına girer.',
      },

      planned: {
        slack: 'Bir kanala gönderir.',
        teams: 'Bir takım kanalına gönderir.',
        pagerduty: 'Nöbetçi kim ise onu çağırır.',
        discord: 'Bir kanala gönderir.',
      },

      deleted: 'Entegrasyon silindi',
      updated: 'Entegrasyon güncellendi',
      connected: (channel: string) => `${channel} bağlandı`,

      testOk: 'Kanal bir test bildirimini kabul etti.',
      testRejected: 'Kanal testi reddetti.',
      testOkLabel: 'Test gönderildi',
      testFailLabel: 'Test başarısız',

      deleteBody: (channel: string) =>
        `Bu ${channel} hedefi ve onunla saklanan kimlik bilgileri kaldırılıyor. Zaten gönderilmiş olaylara ait teslimat geçmişi korunuyor.`,
      deleteConfirm: 'Entegrasyonu sil',

      editTitle: (channel: string) => `${channel} entegrasyonunu düzenle`,
      connectTitle: (channel: string) => `${channel} bağla`,
      namePlaceholder: (channel: string) => `${channel} — nöbet`,
      nameHint: 'Bu hedefin entegrasyonlar sayfasında nasıl tanınacağı.',
      minPriority: 'Asgari öncelik',
      anyPriority: 'Her öncelik',
      andAbove: (priority: string) => `${priority} ve üstü`,
      categoryFilter: 'Kategori filtresi',
      categoryPlaceholder: 'Her kategori',

      sends: (parts: string) => `Gönderir: ${parts}`,
      sendsEverything: 'Her olayı gönderir — filtre ayarlanmamış',
      category: (value: string) => `kategori ${value}`,
      pausedNote: '— buraya bir şey gönderilmiyor.',
      whenResumed: 'devam ettirildiğinde.',
    },

    telemetry: {
      title: 'Telemetri',
      intro:
        'Tespitin nereden okuduğu. Platform kendi log deponuzdan belirli aralıklarla çekiyor — kendini asla izlemiyor, ve buradan bağlamadığınız hiçbir şey ona ulaşmıyor.',
      loadError: 'Kaynaklar yüklenemedi.',

      blindnessNone: 'Hiçbir kaynak bağlı değil. Hiçbir şey okunmuyor, yani hiçbir şey tespit edilemeyecek.',
      blindnessOne: 'Tek kaynak duraklatılmış. Hiçbir şey okunmuyor, yani hiçbir şey tespit edilmeyecek.',
      blindnessMany: (total: number) =>
        `${total} kaynağın hepsi duraklatılmış. Hiçbir şey okunmuyor, yani hiçbir şey tespit edilmeyecek.`,

      comingSoonNote:
        '“Loglarım Seq’te değil” sorusunun genel cevabı bu ikisi, ve henüz ikisi de yapılmadı — burada yapılandırılacak bir şey yok. Amaç bilerek sağlayıcı başına connector değil: tek bir standart tel formatı, ve uzun kuyruğu zaten çalıştırdığınız shipper’ın halletmesi.',

      addAnother: (name: string) => `Bir ${name} kaynağı daha ekle`,
      connectOne: (name: string) => `${name} bağla`,

      summary: {
        Seq: 'Bir Seq örneğinin sorgu API’sinden belirli aralıklarla çeker.',
      },

      planned: {
        otlp: {
          name: 'OTLP log alımı',
          summary: 'Sizin collector’ınız gönderir; sağlayıcı başına connector yok.',
        },
        alerts: {
          name: 'Alarm webhook alımı',
          summary: 'Log değil, izleme sisteminizden gelen alarmlar.',
        },
      },

      deleted: 'Kaynak silindi',
      updated: 'Kaynak güncellendi',
      connected: (kind: string) => `${kind} bağlandı`,

      probeAnswered: 'Kaynak yoklamaya cevap verdi.',
      probeNoMatch:
        'Kaynak cevap verdi, ama yoklanan pencerede filtreye uyan hiçbir şey yoktu. Ya pencere sessiz ya da filtre fazla dar.',
      probeMatched: (count: number) =>
        `Kaynak cevap verdi, görünen ${count} eşleşen kayıt var.`,
      probeRejected: 'Kaynak yoklamayı reddetti.',
      testOkLabel: 'Yoklama başarılı',
      testFailLabel: 'Yoklama başarısız',

      deleteBody: (kind: string) =>
        `Bu ${kind} kaynağı ve onunla saklanan kimlik bilgileri kaldırılıyor, ve tespit hemen ondan okumayı bırakıyor. Zaten alınmış loglar ve imzalar korunuyor — onlar zaten açılmış olayların arkasındaki kanıt. Buraya yeniden bağlanan bir kaynak, bunun bıraktığı yerden değil kendi ilk geriye bakış penceresinden başlar.`,
      deleteConfirm: 'Kaynağı sil',

      editTitle: (kind: string) => `${kind} kaynağını düzenle`,
      connectTitle: (kind: string) => `${kind} bağla`,
      namePlaceholder: (kind: string) => `${kind} — üretim`,
      nameHint: 'Bu kaynağın telemetri sayfasında ve tespit loglarında nasıl tanınacağı.',
      pollLabel: 'Poll aralığı (saniye)',
      pollInvalid: (minimum: number) =>
        `Tam sayı olarak ${minimum} saniye ya da daha fazlasını girin. Bundan hızlı poll etmek kaynağı boşuna yorar.`,

      polls: (seconds: number, what: string) => `Her ${seconds}sn'de bir ${what} için bakıyor`,
      matching: (filter: string) => `${filter} ile eşleşen kayıtlar`,
      defaultFilter: 'error ve fatal kayıtlar',
      pausedNote: '— buradan bir şey okunmuyor.',
      whenResumed: 'devam ettirildiğinde.',
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

    goneService: '(imza silinmiş)',

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
