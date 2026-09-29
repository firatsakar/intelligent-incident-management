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
 *   dashboard             genel bakış
 *   event (log)           kayıt / log kaydı — asla "olay"
 *   false positive        yanlış alarm (olay kapatılırken verilen karar; eski puan dökümlerindeki
 *                         "yanlış pozitif geçmişi" etiketi kayıtlı veriyi okuduğu için kaldı)
 *
 * Son iki satır çakışma önlemek için. **"olay" `incident`'a harcandı**, o yüzden Seq event'i ya
 * da log event'i asla "olay" olamaz — bu bir kez `seq.serviceProperty` ipucunda oldu ve düzeltildi.
 * **"panel" de konsolun kartlarına harcandı**: `incidents.timeline` içinde iki cümle operatöre
 * *"panele bakın"* diyor ve ray girişi de "Panel" olsaydı o cümleler aynı anda iki yeri
 * işaret ederdi. Dashboard bu yüzden `Genel bakış`.
 *
 * `Alert` (kaynak) **alarm**, `Warning` (şiddet) **uyarı** — ikisi de "uyarı" olsaydı incident'ın
 * nereden geldiğiyle ne kadar ciddi olduğu aynı kelimeyle anlatılırdı.
 */
export const tr: Dictionary = {
  common: {
    selected: 'Seçili',
    close: 'Kapat',
    signOut: 'Oturumu kapat',
    copy: 'Kopyala',
    copied: 'Kopyalandı',
    unreachable: 'Sunucuya ulaşılamadı. Bağlantınızı kontrol edip tekrar deneyin.',
    tooMany: 'Çok fazla deneme yapıldı. Bir dakika bekleyip tekrar deneyin.',
    serverError: 'Sunucu şu anda cevap vermiyor. Birazdan tekrar deneyin.',
  },


  nav: {
    skip: 'İçeriğe geç',
    open: 'Menüyü aç',
    sections: 'Bölümler',
    drawer: 'Konsolun bölümleri arasında geçin.',

    groups: {
      operations: 'Operasyon',
      pipeline: 'İşleme hattı',
    },

    items: {
      dashboard: 'Genel bakış',
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

    suffix: '— gerçek zamanlı bağlantı',
  },

  settingsNav: {
    title: 'Ayarlar',
    sections: 'Ayar bölümleri',
    pages: {
      profile: 'Profil',
      organization: 'Organizasyon',
      integrations: 'Entegrasyonlar',
    },
  },

  login: {
    tagline: 'Yaptığı işi gösteriyor.',
    taglineDetail:
      'Platform log kaynağınızı izliyor, bir şeyin birini uyandırmaya değip değmediğine kayıt üzerinden karar veriyor ve sonra vardığı kararı açıklıyor.',

    points: {
      gate: {
        title: 'Okuyabileceğiniz bir kapı',
        detail:
          'Sinyaller, herhangi bir model devreye girmeden önce açık kurallarla puanlanıyor ve aritmetik olayın üstünde kalıyor — negatif çıkan bileşenler dahil.',
      },
      restraint: {
        title: 'Ve yükseltmedikleri',
        detail:
          'Zayıf ve bastırılmış sinyaller, tespit gecikmesinin yanında kayıtta duruyor; böylece sakin bir saat, açıklanamamış olarak değil sakin olarak okunuyor.',
      },
      routing: {
        title: 'Yönlendirilir, yayınlanmaz',
        detail:
          'Biten bir analiz, önceliğe göre süzülerek yapılandırdığınız kanallara ulaşıyor ve her deneme kendi teslimat sonucunu saklıyor.',
      },
    },

    heading: 'Oturum aç',
    emailLabel: 'E-posta adresi',
    passwordLabel: 'Parola',
    required: 'E-posta adresinizi ve parolanızı girin.',
    failed: 'Bu e-posta adresi ve parola etkin bir hesapla eşleşmiyor.',
    unreachable: 'Sunucuya ulaşılamadı. Bağlantınızı kontrol edip tekrar deneyin.',
    tooMany: 'Çok fazla deneme yapıldı. Bir dakika bekleyip tekrar deneyin.',
    serverError: 'Oturum açma şu anda cevap vermiyor. Birazdan tekrar deneyin.',
    submit: 'Konsola gir',
    submitting: 'Giriş yapılıyor…',
    forgot: 'Parolanızı mı unuttunuz? Organizasyonunuzun bir Yöneticisi size yeni parola belirleme bağlantısı gönderebilir.',
  },

  setup: {
    title: 'Bu kurulumu hazırlayın',
    subtitle: 'Burada henüz kimse oturum açmadı. Organizasyonu ve ilk Yöneticisini — sizi — oluşturun.',
    code: 'Kurulum kodu',
    codeHint:
      'Kimlik servisi başlarken log’una bir kez yazdı; “First-run setup” ile başlayan satırda. Yalnız bu sunucuyu çalıştıran okuyabilir — kanıtladığı da bu.',
    codeRequired: 'Log’daki kurulum kodunu girin.',
    organization: 'Organizasyon adı',
    organizationHint: 'Ekibiniz ya da şirketiniz. Bir Yönetici sonradan Ayarlar → Organizasyon’dan değiştirebilir.',
    organizationRequired: 'Organizasyona bir ad verin.',
    name: 'Adınız',
    nameRequired: 'Adınızı girin.',
    email: 'E-posta adresi',
    emailInvalid: 'Bu bir e-posta adresine benzemiyor.',
    password: 'Parola',
    repeat: 'Parolayı tekrar girin',
    submit: 'Organizasyonu oluştur',
    submitting: 'Kuruluyor…',
    rejected:
      'Bu kod kurulumu açmıyor. Servis yeniden başladıysa log’daki yeni kodu kullanın; kurulum zaten yapıldıysa oturum açın.',
    doneTitle: 'Bu kurulum zaten hazır',
    done: 'Hesabınızla oturum açın. Yeni kişiler bir Yöneticinin davetiyle katılır.',
    toSignIn: 'Oturum açmaya git',
  },

  passwordRules: {
    hint: 'En az 12 karakter. Birbiriyle ilgisiz birkaç kelime, tek bir akıllıca kelimeden hem daha kolay hatırlanır hem daha zor tahmin edilir.',
    tooShort: 'En az 12 karakter kullanın.',
    tooLong: 'Çok uzun — yalnızca ilk 72 bayt sayılır. Daha az karakter kullanın.',
    mismatch: 'İki parola aynı değil.',
  },

  oneTimeLink: {
    checking: 'Bağlantı kontrol ediliyor',
    deadTitle: 'Bu bağlantı çalışmıyor',
    dead: 'Süresi dolmuş, yerine daha yenisi gönderilmiş ya da zaten kullanılmış olabilir. Organizasyonunuzun bir Yöneticisinden yenisini isteyin.',
    toSignIn: 'Oturum açmaya git',
    unavailableTitle: 'Bağlantı kontrol edilemedi',
    retry: 'Tekrar dene',
  },

  invite: {
    title: (organization: string) => `${organization} organizasyonuna katılın`,
    subtitle: 'Ekibinizin göreceği adı ve bir parola seçin.',
    email: 'E-posta',
    role: 'Rol',
    name: 'Adınız',
    nameHint: 'Organizasyonunuzdaki diğer kişiler sizi bu adla görecek.',
    nameRequired: 'Adınızı girin.',
    password: 'Parola',
    repeat: 'Parolayı tekrar girin',
    signedInAs: (name: string) =>
      `Bu tarayıcıda ${name} olarak oturum açık. Hesabı oluşturmak, oturumu yeni hesaba geçirir.`,
    expires: (when: string) => `Bu davet bir kez ve ${when} tarihine kadar geçerli.`,
    submit: 'Hesabı oluştur',
    submitting: 'Hesap oluşturuluyor…',
  },

  reset: {
    title: 'Yeni bir parola seçin',
    subtitle: (name: string, email: string) => `${name} · ${email}`,
    password: 'Yeni parola',
    repeat: 'Yeni parolayı tekrar girin',
    sessions:
      'Bu hesabın bütün oturumları kapatılır ve bu tarayıcıda yeni parolayla oturum açılır.',
    expires: (when: string) => `Bu bağlantı bir kez ve ${when} tarihine kadar geçerli.`,
    submit: 'Parolayı belirle',
    submitting: 'Parola belirleniyor…',
  },

  language: {
    change: 'Dili değiştir',
    title: 'Dil',
    legend: 'Dil',
  },

  theme: {
    change: 'Temayı değiştir',
    title: 'Görünüm',
    legend: 'Tema',
    options: {
      light: { label: 'Açık' },
      dark: { label: 'Koyu' },
      system: { label: 'Sistem' },
    },
  },

  profile: {
    title: 'Profil',
    identity: {
      title: 'Kimlik',
    },

    password: {
      title: 'Parola',
      current: 'Mevcut parola',
      currentRequired: 'Mevcut parolanızı girin.',
      wrongCurrent: 'Bu, mevcut parolanız değil.',
      next: 'Yeni parola',
      repeat: 'Yeni parolayı tekrar girin',
      same: 'Mevcut paroladan farklı bir parola seçin.',
      submit: 'Parolayı değiştir',
      submitting: 'Değiştiriliyor…',
      changed: 'Parola değişti. Diğer oturumlar kapatıldı.',
    },
  },


  incidents: {
    list: {
      title: 'Olaylar',

      filterStatus: 'Durum filtresi',
      filterPriority: 'Öncelik filtresi',
      anyStatus: 'Her durum',
      anyPriority: 'Her öncelik',
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
      emptyTitle: 'Kayıtta hiç olay yok.',

      page: (current: number, total: number) => `Sayfa ${current} / ${total}`,
      previous: 'Önceki',
      next: 'Sonraki',

      analysisFailed: 'analiz başarısız',
      awaitingAnalysis: 'analiz bekleniyor',
      analysed: 'analiz edildi',
      toOpen: (duration: string) => `açılışa +${duration}`,
      openedByHand: 'Elle açıldı — bunu hiçbir şey tespit etmedi',
      sentThroughApi: 'Olay API’si üzerinden gönderildi — burada hiçbir şey tespit etmedi',
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
      sentWith: (key) => `${key} anahtarıyla`,

      openedByHand: 'Elle açıldı',
      sentWithKey: (key) => `${key} anahtarıyla gönderildi`,
      problemStarted: 'Sorunun başlangıcı',
      incidentOpened: 'Olayın açılışı',
      detectionLatency: 'Tespit gecikmesi',
      clockDisagreement: 'Saat uyuşmazlığı',
      latencyHintLabel: 'Tespit gecikmesi neyi ölçüyor',
      latencyHint:
        'Kaynağın damgaladığı ilk log satırından bu kaydın açıldığı ana kadar — log deposunun saatinden bizimkine. İçinde sorgulama aralığı, tespit geçişi ve puanlama var; platformun bunu kendi başına fark etmek için harcadığı sürenin tamamı bu.',
      skewHint:
        'Kaynak, bunun biz kaydı açtıktan sonra başladığını bildiriyor; bu ancak iki saatin uyuşmadığı anlamına gelir. Gördüğünüz rakam bir gecikme değil, o uyuşmazlığın büyüklüğü.',

      closed: (relative: string) => `${relative} kapandı`,
      statusUpdated: 'Durum güncellendi',
      assignedTo: (team: string) => `${team} ekibine atandı`,

      verdictDialog: {
        title: 'Bu gerçek bir sorun muydu?',
        description: (status: string) =>
          `${status} olarak işaretlenmeden önce bir kez soruluyor. Cevap olayın üzerinde kalır; olayı dedektör açtıysa, o hatanın geçmişine de sayılır.`,
        effect: {
          Real: 'Gerçekten bir şey bozuktu. Aynı hatanın bir sonraki patlamasının olay açma ihtimali biraz artar.',
          FalsePositive:
            'Yapılacak bir şey yoktu. Aynı hatanın bir sonraki patlamasının olay açabilmesi için daha güçlü olması gerekir.',
        },
        cancel: 'Vazgeç',
        confirm: (status: string) => `${status} olarak işaretle`,
      },
    },

    timeline: {
      title: 'Zaman çizelgesi',

      problemStarted: 'Sorunun başlangıcı',

      incidentOpened: 'Olayın açılışı',
      toDetect: (duration: string) => `tespit için +${duration}`,

      analysisApplied: 'Analizin işlenmesi',
      categorised: (category: string, priority: string) =>
        `${category} olarak sınıflandı, öncelik ${priority} yapıldı`,

      peopleNotified: 'Bildirim gönderimi',
      afterOpening: (duration: string) => `açılıştan +${duration} sonra`,
      channelsDelivered: (sent: number, total: number) =>
        `${total} kanaldan ${sent} tanesi ulaştı`,
      everyChannelFailed: 'Her kanal başarısız oldu.',

      lastChanged: 'Son değişiklik',

      doneUntimed: 'yapıldı · zamanı kaydedilmedi',
      notYet: 'henüz değil',
    },

    activity: {
      title: 'Olay geçmişi',
      loadError: 'Olay geçmişi yüklenemedi.',

      someone: 'Bir üye',
      apiKey: (name: string) => `${name} API anahtarı`,
      detector: 'Telemetri dedektörü',
      ai: 'AI analizi',

      opened: 'olayı açtı',
      statusChanged: (from: string, to: string) => `durumu değiştirdi: ${from} → ${to}`,
      teamAssigned: (team: string) => `${team} ekibine atadı`,
      teamReassigned: (from: string, to: string) => `ekibi değiştirdi: ${from} → ${to}`,
      analysed: (category: string | null) =>
        category ? `${category} olarak sınıflandırdı` : 'analiz etti',
      priorityChanged: (from: string, to: string) => `öncelik ${from} → ${to}`,
      priorityKept: (priority: string) => `öncelik ${priority} kaldı`,
      analysisFailed: 'analiz edemedi',
      commented: 'yorum yazdı',
      withDetail: (action: string, detail: string) => `${action} · ${detail}`,

      commentLabel: 'Yorum',
      commentPlaceholder: 'Ne denediniz, ne buldunuz, sırada ne var',
      commentHint: 'Yorumlar sonradan düzenlenemez ve silinemez.',
      remaining: (count: number) => `${count} karakter kaldı`,
      send: 'Yorum ekle',
      sending: 'Gönderiliyor…',
    },

    score: {
      title: 'Kapı bunu nasıl puanladı',

      total: 'Toplam',
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
      empty: 'Henüz bir şey gönderilmedi.',
      deletedIntegration: 'silinmiş entegrasyon',
      took: (duration: string) => `${duration} sürdü`,
      queued: (when: string) => `${when} kuyruğa girdi`,
      attempts: (count: number) => `${count} deneme`,
    },

    analysis: {
      title: 'AI analizi',

      failedTitle: 'Analiz başarısız',

      waiting: 'Analiz servisi bekleniyor.',

      confidence: 'Güven',
      confidenceHintLabel: 'Güven rakamı ne anlama geliyor',
      confidenceHint:
        'Analizin kendi kategorisinden ve önceliğinden ne kadar emin olduğu — olayın ne kadar ciddi olduğu değil; kapının bir şeyin bozulduğundan ne kadar emin olduğu da değil. O ikincisi soldaki puan.',

      reasoning: 'Gerekçe',

      suspectedChanges: 'Şüpheli değişiklikler',
      suspectedChangesHintLabel: 'Şüpheli değişiklikler nereden geliyor',
      suspectedChangesHint:
        'Sorun başlamadan önceki 48 saatte bu servisin repo’suna gelen ve analizin okuyup sorunu açıkladığını düşündüğü commit’ler. GitHub’ı yalnızca okur, hiçbir şey yazmaz ve kendisine gösterilmemiş bir şeyi anmaz.',
      openChange: (sha: string, title: string) => `${sha} commit’ini GitHub’da aç: ${title}`,
    },
  },

  scoreTerms: {
    label: {
      fatal: 'ölümcül hata',
      burstBase: 'patlama tabanı',
      overThreshold: 'eşik aşımı',
      rateAnomaly: 'hız anomalisi',
      history: 'geçmiş',
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
      history:
        'Bu imzanın önceki olayları kapatılırken ne çıktığı: gerçek bulunanların oranı, az karar varken indirimli. Her zaman −0,25 (hepsi yanlış alarm) ile +0,15 (hepsi gerçek) arasında.',
      precedent: 'Bu imza daha önce gerçek olduğu doğrulanmış bir olay üretti.',
      blastRadius: 'Bunu bir servis değil, iki ya da daha fazlası bildiriyor.',
      falsePositivePrecedent:
        'Bu imza daha önce yanlış pozitif olarak işaretlendi, o yüzden puan aşağı çekiliyor.',
      muted:
        'Birisi bu imzayı susturmuş. Yine de puanlanıyor ve susturulmuş olduğu için ağır ceza alıyor.',
    },
  },


  telemetry: {
    otherSignatures: 'diğer imzalar',
    unknownError: 'bilinmeyen hata',
    unknownService: 'bilinmeyen servis',

    signals: {
      title: 'Sinyaller',
      loadError: 'Sinyaller yüklenemedi',

      allSignals: 'Tüm sinyaller',
      clearFilter: 'Filtreyi temizle',

      emptyTileTitle: 'Bu kutucuk için sinyal yok.',
      emptyWindowTitle: 'Bu pencerede sinyal yok.',

      loadMore: (count: number) => `${count} tane daha yükle`,
      notLoaded: (count: number) => `${count} eski sinyal yüklenmedi`,
      ceiling: (max: number, total: number) => `${total} sinyalin en yeni ${max} tanesi gösteriliyor.`,

      occurrences: (count: number) => `${count} kez`,
      viewIncident: 'Olaya git',
    },

    heatmap: {
      title: 'Hatalar nerede',
      empty: 'Bu pencerede sinyal yok.',

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

      panelCounts: '{occurrences} kez · {signals} sinyal',
      incidentLink: 'Olay →',

      legendOccurrences: 'görülme sayısı',
      noMark: 'işaretsiz — yalnızca kaydedildi',
      legendFlash: 'son birkaç saniyede güncellendi',
      unplaced: (count: number) =>
        `${count} sinyal yerleştirilemedi — imzaları artık yok.`,
    },

    evidence: {
      title: 'Kanıt',
      loadError: 'Kanıt yüklenemedi',

      filterService: 'Servis filtresi',
      clearService: 'Servis filtresini temizle',

      logRecords: 'Log kayıtları',
      logListLabel: 'Bu penceredeki log kayıtları',
      emptyLogTitle: 'Bu pencerede loglanan bir şey yok.',

      signatures: 'İmzalar',
      signaturesListLabel: 'Bu penceredeki sinyallerin arkasındaki imzalar',
      emptySignaturesTitle: 'Burada imza yok.',

      signals: 'Sinyaller',
      signalsListLabel: 'Bu penceredeki sinyaller',
      emptySignalsTitle: 'Burada sinyal yok.',

      arrived: (records: number) =>
        `Bu pencere okunduğundan beri ${records} yeni log kaydı alındı.`,
      arrivedAcrossPolls: (records: number, polls: number) =>
        `Bu pencere okunduğundan beri ${polls} sorgulamada ${records} yeni log kaydı alındı.`,
      reread: 'Pencereyi yeniden oku',
      rereading: 'Yeniden okunuyor…',

      clockSkew: 'saat kayması',
      folded: (count: number) => `×${count}`,
      foldedTitle: (count: number) =>
        `Bu satır bir patlamanın ${count} olayını temsil ediyor. Patlamanın geri kalanı ayrı ayrı saklanmak yerine bu satıra sayıldı.`,
      ingestionLag: 'Alım gecikmesi — kaynağın zaman damgasından bizimkine',
      lag: (duration: string) => `+${duration}`,
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
      loadError: 'Huni yüklenemedi',

      notRaised: 'Yükseltilmedi',
      ofScored: (signals: number) => `/ kapının puanladığı ${signals} sinyal`,
      heldBack: 'geri tutulan',
      actedOn: 'işlem yapılan',

      nothingScored: 'Bu pencerede hiçbir şey puanlanmadı.',
      nothingArrived: 'Bu pencerede telemetri gelmedi.',

      stagesTitle: 'Log kayıtlarından sinyallere',
      stageLogRecords: 'Log kayıtları',
      stageSignatures: 'İmzalar',
      stageSignals: 'Sinyaller',
      stagesChartLabel: (records: string, signatures: string, signals: string) =>
        `İşleme hattı aşamaları. ${records} log kaydı, ${signatures} imza, ${signals} sinyal.`,

      whatArrived: 'Gelenler',
      noLogRecord: 'Bu pencereye hiç log kaydı gelmedi.',

      verdictsTitle: 'Kapı nasıl karar verdi',

      wokenHeading: 'Biri uyandırıldı',
      notWokenHeading: 'Kimse uyandırılmadı',
    },

    services: {
      title: 'Servis sağlığı',
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

      goneHintLabel: '(imza silinmiş) ne demek',
      goneHint:
        'Bu sinyaller tespit edildi, ama arkalarındaki hata imzası o zamandan beri silindi — ve servis adı imzanın üstünde duruyordu. Sayılar gerçek; ait oldukları ad geri getirilemez.',

      folded: (records: string, promoted: string, incidents: string, signature: string) =>
        `${records} kayıt · ${promoted} terfi · ${incidents} olay · ${signature}`,
      occurrences: (count: string, _raw: number) => `${count} kez`,
      topSignature: (signature: string, occurrences: string) => `${signature} · ${occurrences}`,
      noSignal: 'Bu pencerede bu servisten sinyal yok',

      signatureGone: 'imza artık kayıtta değil',
      nothingCrossed: 'bu pencerede hiçbir şey bir tespit kuralını aşmadı',

    },
  },


  dashboard: {
    title: 'Genel bakış',
    days: (count: number) => `${count} gün`,
    loadError: 'Rakamlar yüklenemedi',

    open: {
      title: 'Şu anda açık',
      open: 'açık',
    },

    byDate: {
      title: 'Tarihe göre olaylar',
    },

    detection: {
      title: 'Tespit',
      empty: 'Bu pencerede olay yok.',
      share: 'platformun kendi fark ettiği olaylar',
      noticed: 'Fark edilen',
      filed: 'Elle açılan',
      median: 'Medyan gecikme',
      p95: '95. yüzdelik',
    },

    sources: {
      title: 'Olaylar nereden geliyor',
      empty: 'Bu pencerede olay yok.',
    },

    latest: {
      title: 'Son olaylar',
      loadError: 'Olaylar yüklenemedi',
      emptyTitle: 'Kayıtta hiç olay yok.',
      all: (total: number) => `${total} olayın tamamı`,
    },

    chart: {
      summary: (total: number, days: number) => `${days} günde ${total} olay açıldı`,
      busiest: (count: number) => ` · en yoğun gün ${count}`,
      dayTotal: (total: number) => `${total} olay`,
      ariaLabel: (days: number) =>
        `${days} gün boyunca UTC günü başına açılan olaylar. Günleri tek tek okumak için ok tuşlarını kullanın.`,
      emptyPlot: 'Bu pencerede hiçbir şey açılmadı. İçindeki her gün boş.',
      dayReadout: '{day} · {total} — {breakdown}',
      dayReadoutEmpty: '{day} · {total}',
      priorityCount: (count: number, priority: string) => `${count} ${priority}`,
      caption: 'UTC günü başına açılan olaylar, önceliğe göre.',
      columnDay: 'Gün (UTC)',
      columnTotal: 'Toplam',
      dayResolved: (count: number) => ` · ${count} kapatıldı`,
      resolvedSummary: (count: number) => ` · ${count} kapatıldı`,
      legendResolved: 'Kapatılan',
      columnResolved: 'Kapatılan',
    },

    resolution: {
      title: 'Çözüm süresi',
      empty: 'Bu pencerede kapatılan olay yok.',
      median: 'medyan, sorunun başlangıcından kapanışa',
      p95: '95. yüzdelik',
      closed: 'Kapatılan',
    },

    accuracy: {
      title: 'Tespit doğruluğu',
      empty: 'Bu pencerede kararla kapatılan olay yok.',
      real: 'gerçek',
      none: 'karar yok',
      counts: (real: number, falsePositive: number) => `${real} gerçek · ${falsePositive} yanlış alarm`,
      unknown: (count: number) => ` · ${count} olay karar sorulmadan önce kapandı`,
    },

    ai: {
      title: 'AI analizi',
      empty: 'Bu pencerede olay yok.',
      share: 'analiz edildi',
      analysed: 'Analiz edilen',
      failed: 'Başarısız',
      pending: 'Bekleyen',
      confidence: 'Medyan güven',
    },
  },


  deliveries: {
    title: 'Teslimat sağlığı',
    loadError: 'Teslimat sağlığı yüklenemedi',

    deleted: 'Silinmiş entegrasyon',

    totals: {
      title: 'Teslimatlar',
      empty: 'Bu pencerede hiçbir şey gönderilmedi.',

      failedOf: (total: string, _totalCount: number, channels: string, _channelCount: number) =>
        `/ ${channels} entegrasyonda denenen ${total} teslimattan başarısız`,

      failed: 'başarısız',
      sent: 'gönderilen',
      pending: 'bekleyen',

    },

    dispatch: {
      title: 'Gönderim süresi',
      empty: 'Bu pencerede hiçbir şey gönderilmedi.',
      noneSucceeded: 'Bu pencerede hiçbir teslimat başarılı olmadı.',
      chartLabel: (scope: string, detail: string) =>
        `Entegrasyon başına medyan gönderim süresi, ${scope}. ${detail}.`,
      chartRow: (name: string, value: string) => `${name} ${value}`,
    },

    verdict: {
      failing: 'Ulaşmıyor',
      recovered: 'Düzeldi',
      delivering: 'Teslim ediyor',
      queued: 'Kuyrukta',
      silent: 'Gönderim yok',
    },

    byIntegration: {
      title: 'Entegrasyona göre',
      emptyTitle: 'Hiçbir entegrasyon teslimat denemedi.',

      disabled: 'Duraklatıldı',
      id: (short: string) => `id ${short}`,
      lastFailure: 'Son başarısızlık',

      // Same rule as the swatch labels above: these are <dt> elements naming a count, so the
      // participle. 'Başarısız' is already adjectival.
      sent: 'Gönderilen',
      failed: 'Başarısız',
      pending: 'Bekleyen',
      median: 'Medyan süre',
      lastSent: 'Son gönderim',
    },
  },


  settings: {
    incidentApi: {
      heading: 'Olay API’si',
      count: (n) => `${n} anahtar`,
      title: 'Kendi sistemlerinizden olay açın',
      endpointLabel: 'Uç (POST)',
      endpointHint: (header) => `JSON gövde; anahtarı ${header} başlığında gönderin.`,
      loadError: 'Anahtarlar yüklenemedi.',
      empty: 'Henüz anahtar yok.',
      create: 'Yeni anahtar',
      createTitle: 'Yeni API anahtarı',
      createBody:
        'Onu kullanacak sistemin adını verin. Ad, anahtarın açtığı her olayda görünür.',
      namePlaceholder: 'Grafana, CI hattı…',
      nameHint: 'En fazla 64 karakter, bu organizasyonda tekil.',
      nameRequired: 'Anahtara bir ad verin.',
      nameTaken: 'Bu adla bir anahtar zaten var.',
      creating: 'Oluşturuluyor…',
      createConfirm: 'Anahtarı oluştur',
      issuedTitle: (name) => `“${name}” anahtarı`,
      once: 'Bu anahtar yalnız bir kez gösterilir. Yalnız hash’i saklanır, yeniden gösterilemez — kaybolursa yenisini üretin.',
      keyLabel: 'API anahtarı',
      curlLabel: 'Örnek istek',
      curlHint:
        'externalId isteğe bağlıdır: onunla açılmış bir olay açıkken aynısını göndermek yeni olay açmaz, o olayı döner.',
      done: 'Tamam',
      createdBy: (name, when) => `${name} oluşturdu, ${when}`,
      lastUsed: (when) => `son kullanım ${when}`,
      neverUsed: 'hiç kullanılmadı',
      deleteAria: (name) => `${name} anahtarını sil`,
      deleteBody:
        'Bu anahtarla gönderen sistemler bundan sonra 401 alır. Açtığı olaylar adını korur.',
      deleteConfirm: 'Anahtarı sil',
      deleted: 'Anahtar silindi.',
    },

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
      secretKept: 'Boş bırakılan bir gizli alan, saklanan değeri korur.',
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
        'jira.issueType': 'Issue türü',
        'seq.url': 'Seq URL',
        'seq.apiKey': 'API anahtarı',
        'seq.filter': 'Filtre',
        'seq.serviceProperty': 'Servis alanı',
        'seq.initialLookback': 'İlk geriye bakış (dakika)',
        'otlp.minimumSeverity': 'En düşük şiddet',
      },

      hints: {
        'webhook.authorization':
          'Header: ön ekiyle başlayan her ayar bir istek başlığı olarak gönderilir.',
        'seq.apiKey':
          'Yalnızca Seq örneğinde kimlik doğrulama açıksa gerekir. Anahtarın Read yetkisi olmalı.',
        'seq.filter':
          'Seq filtre ifadesi. Boş bırakılırsa hata ve ölümcül kayıtlar okunur.',
        'seq.serviceProperty':
          'Bir log satırının hangi servisten geldiğini söyleyen alanın adı.',
        'seq.initialLookback':
          'İlk sorgulama ne kadar geriye bakar. Sonrakiler sonuncunun bıraktığı yerden devam eder.',
        'otlp.minimumSeverity':
          'Warning ya da Error. Boş bırakılırsa yalnızca hata ve ölümcül kayıtlar saklanır; altındaki her şey gelir gelmez atılır.',
      },
    },

    integrations: {
      loadError: 'Entegrasyonlar yüklenemedi.',

      silenceNone: 'Hiçbir şey bağlı değil. Bir analiz tamamlandığında kimseye haber verilmez.',
      silenceOne:
        'Tek entegrasyon duraklatılmış. Bir analiz tamamlandığında kimseye haber verilmez.',
      silenceMany: (total: number) =>
        `${total} entegrasyonun hepsi duraklatılmış. Bir analiz tamamlandığında kimseye haber verilmez.`,

      addAnother: (name: string) => `Bir ${name} daha ekle`,
      connectOne: (name: string) => `${name} bağla`,

      summary: {
        Email: 'Bir posta kutusuna ya da dağıtım listesine SMTP.',
        Webhook: 'Olayın ve analizinin, denetlediğiniz bir uç noktaya HTTP POST edilmesi.',
        Jira: 'Bir projede issue açar, gerekçe de açıklamasına girer.',
      },

      deleted: 'Entegrasyon silindi',
      updated: 'Entegrasyon güncellendi',
      connected: (channel: string) => `${channel} bağlandı`,

      testOk: 'Kanal bir test bildirimini kabul etti.',
      testRejected: 'Kanal testi reddetti.',
      testOkLabel: 'Test gönderildi',
      testFailLabel: 'Test başarısız',

      deleteBody: (channel: string) =>
        `Bu ${channel} entegrasyonu ve onunla saklanan kimlik bilgileri kaldırılıyor. Zaten gönderilmiş olaylara ait teslimat geçmişi korunuyor.`,
      deleteConfirm: 'Entegrasyonu sil',

      editTitle: (channel: string) => `${channel} entegrasyonunu düzenle`,
      connectTitle: (channel: string) => `${channel} bağla`,
      namePlaceholder: (channel: string) => `${channel} — nöbet`,
      nameHint: 'Bu entegrasyonun entegrasyonlar sayfasında nasıl tanınacağı.',
      minPriority: 'En düşük öncelik',
      anyPriority: 'Her öncelik',
      andAbove: (priority: string) => `${priority} ve üstü`,
      categoryFilter: 'Kategori filtresi',
      categoryPlaceholder: 'Her kategori',

      sends: (parts: string) => `Gönderir: ${parts}.`,
      sendsEverything: 'Her olayı gönderir — filtre ayarlanmamış.',
      category: (value: string) => `kategori ${value}`,
      pausedNote: (parts: string) =>
        `— buraya bir şey gönderilmiyor. Devam ettirilirse gönderecekleri: ${parts}.`,
      pausedNoteEverything:
        '— buraya bir şey gönderilmiyor. Devam ettirilirse her olayı gönderecek.',
    },

    telemetry: {
      loadError: 'Kaynaklar yüklenemedi.',

      blindnessNone: 'Hiçbir kaynak bağlı değil. Hiçbir şey okunmuyor, yani hiçbir şey tespit edilemeyecek.',
      blindnessOne: 'Tek kaynak duraklatılmış. Hiçbir şey okunmuyor, yani hiçbir şey tespit edilmeyecek.',
      blindnessMany: (total: number) =>
        `${total} kaynağın hepsi duraklatılmış. Hiçbir şey okunmuyor, yani hiçbir şey tespit edilmeyecek.`,

      addAnother: (name: string) => `Bir ${name} kaynağı daha ekle`,
      connectOne: (name: string) => `${name} bağla`,

      summary: {
        Seq: 'Bir Seq örneğinin sorgu API’sinden belirli aralıklarla çeker.',
        Otlp: 'Collector’ınız ya da SDK’nız logları OpenTelemetry’nin aktarım formatında gönderir — hangi log deposunu kullanırsanız kullanın.',
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
        `Bu ${kind} kaynağı ve onunla saklanan kimlik bilgileri kaldırılıyor ve tespit hemen ondan okumayı bırakıyor. Zaten alınmış loglar ve imzalar korunuyor — onlar zaten açılmış olayların arkasındaki kanıt. Buraya yeniden bağlanan bir kaynak, bunun bıraktığı yerden değil kendi ilk geriye bakış penceresinden başlar.`,
      deleteBodyPushed: (kind: string) =>
        `Bu ${kind} kaynağı kaldırılıyor ve anahtarı hemen çalışmaz oluyor — onunla göndermeye devam eden her şey 401 alır. Zaten alınmış loglar ve imzalar korunuyor; onlar zaten açılmış olayların arkasındaki kanıt.`,
      deleteConfirm: 'Kaynağı sil',

      editTitle: (kind: string) => `${kind} kaynağını düzenle`,
      connectTitle: (kind: string) => `${kind} bağla`,
      namePlaceholder: (kind: string) => `${kind} — üretim`,
      nameHint: 'Bu kaynağın telemetri sayfasında ve tespit loglarında nasıl tanınacağı.',
      pollLabel: 'Sorgulama aralığı (saniye)',
      pollInvalid: (minimum: number) =>
        `Tam sayı olarak ${minimum} saniye ya da daha fazlasını girin. Bundan daha sık sorgulamak kaynağı boşuna yorar.`,

      pushedSchedule: (minimum: string) =>
        `Gönderilen logları alır; ${minimum} ve üstünü saklar.`,
      pushedPausedNote:
        '— devam ettirilene kadar gönderimler 403 ile reddedilir; gönderen taraf 403’ü yeniden denemez.',
      endpointLabel: 'Adres',
      keyLabel: 'Anahtar',
      rotateKey: 'Yeni anahtar',
      rotatingKey: 'Üretiliyor…',
      rotated: 'Yeni anahtar üretildi. Eskisi şu an itibarıyla çalışmıyor.',
      lastReceived: (when: string) => `Son batch ${when} geldi.`,
      nothingReceived: 'Bir collector’ı ya da SDK’yı bu kaynağın anahtarıyla adrese yönlendirin.',
      receivedOkLabel: 'Alınıyor',
      receivedFailLabel: 'Hiçbir şey gelmedi',

      keyPanel: {
        title: (name: string) => `${name} kaynağına log gönderin`,
        once: 'Bu anahtar yalnızca bir kez gösteriliyor. Sadece hash’i saklanıyor, yani bir daha gösterilemez — kaybolursa yenisini üretin.',
        keyLabel: 'Alım anahtarı',
        endpointLabel: 'OTLP/HTTP adresi',
        endpointHint:
          'Protobuf ya da JSON; gzip olur. Exporter’lar /v1/logs’u bu adrese kendileri ekler.',
        headerHint: (header: string) => `Anahtarı ${header} başlığında gönderin.`,
        collectorLabel: 'OpenTelemetry Collector',
        collectorHint:
          'Göndermeden önce hatalara süzer. Süzmenin yeri kaynaktır; bu kaynak da en düşük şiddetin altındakini gelir gelmez atar.',
        sdkLabel: 'Doğrudan bir SDK’dan',
        sdkHint: 'Her OpenTelemetry SDK’sının okuduğu ortam değişkenleri; arada collector yok.',
        done: 'Tamam',
      },

      // "sorguluyor" rather than "bakıyor": the form label two rows above says "Sorgulama
      // aralığı", and one screen should not have two verbs for one action.
      polls: (seconds: number, what: string) =>
        `${what} için her ${seconds} saniyede bir sorguluyor.`,
      matching: (filter: string) => `${filter} ile eşleşen kayıtlar`,
      defaultFilter: 'hata ve ölümcül kayıtlar',
      pausedNote: (seconds: number, what: string) =>
        `— buradan bir şey okunmuyor. Devam ettirilirse ${what} için her ${seconds} saniyede bir sorgulayacak.`,
    },

    hub: {
      jumpTo: 'Bu sayfadaki gruplar',
      groups: {
        observability: {
          title: 'Gözlemlenebilirlik',
        },
        analysis: {
          title: 'Analiz',
        },
        notifications: {
          title: 'Bildirimler',
        },
      },
    },

    aiSources: {
      loadError: 'GitHub bağlantısı yüklenemedi.',

      github: {
        name: 'GitHub',
        status: {
          notConnected: 'Bağlı değil',
          connected: 'Bağlı',
          paused: 'Duraklatıldı',
        },
        token: 'Erişim token’ı',
        tokenPlaceholder: 'github_pat_…',
        tokenKept: 'Kayıtlı — korumak için boş bırakın',
        tokenHint:
          'Bu repo’larda Contents ve Metadata için yalnız okuma izni olan, ayrıntılı (fine-grained) bir kişisel erişim token’ı. Kaydettikten sonra bir daha gösterilmez.',
        repositories: 'Repo’lar',
        repositoriesHint:
          'Her servisin kodunun hangi repo’da olduğu — servis adı telemetrinizin bildirdiği adla. Listede olmayan bütün servisler için * kullanın. Servisinin repo’su olmayan bir olay GitHub’sız analiz edilir.',
        servicePlaceholder: 'servis adı ya da *',
        serviceLabel: (row: number) => `Servis ${row}`,
        repositoryPlaceholder: 'sahip/repo',
        repositoryLabel: (row: number) => `Repo ${row}`,
        branchPlaceholder: 'branch (boşsa varsayılan)',
        branchLabel: (row: number) => `Branch ${row}`,
        removeRow: (row: number) => `${row}. repo’yu kaldır`,
        addRow: 'Repo ekle',
        enabled: 'Analiz GitHub’ı okuyabilsin',
        save: 'Kaydet',
        saving: 'Kaydediliyor…',
        saved: 'GitHub bağlantısı kaydedildi.',
        disconnect: 'Bağlantıyı kaldır',
        disconnectTitle: 'GitHub bağlantısı kaldırılsın mı?',
        disconnectBody:
          'Token ve repo listesi silinir. Önceden yazılmış analizler bulduklarını korur; yenileri GitHub’ı okumaz.',
        cancel: 'Vazgeç',
        removed: 'GitHub bağlantısı kaldırıldı.',
        tokenRequired: 'Bağlanmak için bir token yapıştırın.',
        serviceRequired: 'Her repo’nun bir servis adı olmalı; geri kalanlar için *.',
        repositoryInvalid: 'Her repo’yu sahip/repo biçiminde yazın — örneğin acme/shop.',
        serviceTwice: 'Bir servis yalnız bir repo’ya eşlenebilir.',
        test: 'Test et',
        testing: 'Test ediliyor…',
        testResults: 'Test sonuçları',
        testOk: (changes: number) => `okundu; son 7 günde ${changes} değişiklik`,
        testFailed: (reason: string) => `okunamadı: ${reason}`,
      },
    },

    organization: {
      title: 'Organizasyon adı',
      label: 'Ad',
      required: 'Organizasyonun bir adı olmalı.',
      save: 'Kaydet',
      saving: 'Kaydediliyor…',
      saved: 'Organizasyonun adı değişti.',
    },

    aiLanguage: {
      title: 'AI yanıt dili',
      options: {
        English: 'İngilizce',
        Turkish: 'Türkçe',
      },
      save: 'Kaydet',
      saving: 'Kaydediliyor…',
      saved: 'AI yanıt dili kaydedildi.',
      loadError: 'Ayar yüklenemedi.',
    },

    members: {
      title: 'Üyeler',
      invite: 'Davet et',
      loadError: 'Üyeler yüklenemedi',
      you: 'Siz',
      deactivatedBadge: 'Kapalı',
      roleOf: (name: string) => `${name} için rol`,
      actionsFor: (name: string) => `${name} için işlemler`,
      issueReset: 'Parola sıfırlama bağlantısı gönder',
      deactivate: 'Hesabı kapat',
      activate: 'Hesabı aç',
      selfHint: 'Rolünüzü ve hesabınızı başka bir Yönetici değiştirir. Parolanız Profil sayfanızda.',
      lastAdminHint:
        'Son etkin Yönetici. Bu hesabı değiştirmeden önce başka birini Yönetici yapın.',
      roleChanged: (name: string, role: string) => `${name} artık ${role}.`,
      activated: (name: string) => `${name} yeniden oturum açabilir.`,
      deactivated: (name: string) => `${name} artık oturum açamaz.`,

      deactivateDialog: {
        title: (name: string) => `${name} hesabı kapatılsın mı?`,
        body: 'Artık oturum açamaz ve oturumları sona erer — açık duran bir sayfası en geç on beş dakika içinde çalışmayı bırakır. Yaptıkları kayıtta kalır. Hesabı daha sonra yeniden açabilirsiniz.',
        cancel: 'Vazgeç',
        confirm: 'Hesabı kapat',
      },

      pending: {
        title: 'Bekleyen davetler',
        empty: 'Bekleyen davet yok.',
        expires: (when: string) => `${when} tarihinde sona eriyor`,
        revoke: 'İptal et',
        revokeAria: (email: string) => `${email} adresine gönderilen daveti iptal et`,
        revoked: (email: string) => `${email} adresine gönderilen davet artık çalışmıyor.`,
      },

      inviteDialog: {
        title: 'Üye davet et',
        description:
          'Adını ve parolasını seçeceği bir bağlantıyla e-posta alır. Hesap bu organizasyona ait olur.',
        email: 'E-posta adresi',
        emailRequired: 'Bir e-posta adresi girin.',
        emailInvalid: 'Bu bir e-posta adresine benzemiyor.',
        hasAccount: 'Bu adresin zaten bir hesabı var, bu yüzden davet edilemez.',
        role: 'Rol',
        cancel: 'Vazgeç',
        submit: 'Daveti gönder',
        submitting: 'Gönderiliyor…',
      },

      link: {
        invitationTitle: (email: string) => `${email} için davet`,
        resetTitle: (name: string) => `${name} için parola sıfırlama`,
        once: 'Bu bağlantı bir kez gösterilir. Yalnızca özeti (hash) saklanıyor, bu yüzden bir daha gösterilemez — kaybolursa yenisini oluşturun.',
        label: 'Bağlantı',
        expires: (when: string) => `Bir kez ve ${when} tarihine kadar çalışır.`,
        emailed: (email: string) => `Ayrıca ${email} adresine e-postayla gönderildi.`,
        notEmailed: (email: string) =>
          `${email} adresine e-posta gönderilemedi. Bağlantı çalışıyor — kendiniz iletin.`,
        done: 'Tamam',
      },
    },
  },

  labels: {
    role: {
      Admin: 'Yönetici',
      Engineer: 'Mühendis',
      Viewer: 'İzleyici',
    },

    roleDetail: {
      Admin: 'Her şey; organizasyonun üyeleri, entegrasyonları ve telemetri kaynakları dahil.',
      Engineer: 'Olayları işler — durum, atama ve yorum. Organizasyonun ayarlarını görmez.',
      Viewer: 'Olayları, sinyalleri, kanıtı ve genel bakışı okur; hiçbir şeyi değiştirmez.',
    },

    incidentStatus: {
      Open: 'Açık',
      InProgress: 'Devam ediyor',
      Resolved: 'Çözüldü',
      Closed: 'Kapandı',
    },

    verdict: {
      Real: 'Gerçek sorun',
      FalsePositive: 'Yanlış alarm',
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
      Otlp: 'OTLP',
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
    percent: (value: number) => `%${value}`,

    // Turkish separates the abbreviation from the figure, and `minutesAgo` beside it already
    // did — the two halves of one object were disagreeing.
    span: {
      milliseconds: (value: number) => `${value} ms`,
      seconds: (value: string) => `${value} sn`,
      minutesSeconds: (minutes: number, seconds: number) => `${minutes} dk ${seconds} sn`,
      hoursMinutes: (hours: number, minutes: number) => `${hours} sa ${minutes} dk`,
    },

    justNow: 'az önce',
    minutesAgo: (minutes: number) => `${minutes} dk önce`,
    hoursAgo: (hours: number) => `${hours} sa önce`,
    daysAgo: (days: number) => `${days} gün önce`,
    notGiven: 'verilmedi',
    confidence: {
      high: 'yüksek güven',
      moderate: 'orta güven',
      low: 'düşük güven',
    },
  },
}
