# HT SEÇİM — BESLEME SERVİSİ

Seçim gecesi ajanstan gelen sonuç dosyalarını izleyen, doğrulayan ve API'ye
aktaran servis. Ayrıca gerçek ajans yokken **prova** için sahte ama gerçeğe
benzeyen bir seçim gecesi üretir.

.NET 8 · ASP.NET Core · ayarlar düz metin dosyalarında

---

## Verinin akışı

```
                    ┌──────────────────────────────────────┐
                    │  AJANS   (ANKA / AA / İHA)           │
                    │  FTP · SFTP · klasöre dosya bırakma  │
                    └───────────────┬──────────────────────┘
                                    │  XML anlık görüntü
                                    │  30-60 sn'de bir, TAM liste
                                    ▼
        ╔═══════════════════════════════════════════════════════╗
        ║  HT_SECIM_BESLEME                        (bu depo)    ║
        ║                                                       ║
        ║   klasör izleyici → dosya bitti mi? → XML okuyucu     ║
        ║        → denetim → API'ye gönder → arşiv / hatalı     ║
        ║                                                       ║
        ║   ÜRETİCİ (prova): ajansın yerine geçer               ║
        ╚═══════════════════════════┬═══════════════════════════╝
                                    │  POST /api/v1/besleme
                                    │  yazma anahtarı
                                    ▼
        ┌───────────────────────────────────────────────────────┐
        │  HT_SECIM_API                                         │
        │                                                       │
        │   doğrulama · atomik yazma · TEK sürüm artışı         │
        │   D'Hondt · baraj · TÜRKİYE toplamı · yüzdeler        │
        │                                                       │
        │   yönetim paneli:  /yonetim/                          │
        └──────────┬──────────────────────────┬─────────────────┘
                   │                          │
                   │ SQL Server               │ GET /api/v1/surum  (25 sn)
                   ▼                          │ GET /api/v1/veri   (sürüm
        ┌──────────────────────┐              │                değişince)
        │  HT_SECIM veritabanı │              │
        │                      │              ▼
        │  ham sayılar:        │   ┌──────────────────────────────┐
        │  sandık, seçmen,     │   │  HT_SECIM        (reji)      │
        │  geçersiz oy, oylar  │   │  HT_SECIM_C_EKRAN            │
        └──────────────────────┘   └──────────────┬───────────────┘
                                                  │  TCP 127.0.0.1:6100
                                                  ▼
                                   ┌──────────────────────────────┐
                                   │  VIZ ENGINE                  │
                                   │  reji     → MAIN_LAYER       │
                                   │  C ekranı → FRONT_LAYER      │
                                   └──────────────┬───────────────┘
                                                  ▼
                                              Y A Y I N
```

### Her adımda ne oluyor

| Adım | Taşınan | Sıklık | Not |
|---|---|---|---|
| Ajans → klasör | XML tam anlık görüntü | 30-60 sn | Fark değil, tam liste |
| Besleme → API | JSON, ham sayılar | dosya başına | Yazma anahtarı |
| API → SQL | tek transaction | dosya başına | Sürüm **bir kez** artar |
| API → TV | sürüm sorgusu (birkaç yüz bayt) | 25 sn | Değişmediyse indirme yok |
| API → TV | tam veri (~198 KB) | sürüm değişince | Atomik değişim |
| TV → Viz | sayfa başına 18-46 komut | 3-15 sn | Tek pakette |

### İki kural bütün tasarımı belirliyor

**Besleme yalnızca HAM SAYI taşır.** Yüzde, TÜRKİYE toplamı ve milletvekili
dağılımı dosyada yoktur — hepsini API hesaplar. Tek yerde hesaplanması,
ekranın altıyla üstünün birbiriyle çelişmesini imkânsız kılar.

**Veri atomik değişir.** Aktarım boyunca sürüm artışı baskılanır, iş bitince
bir kez artar. TV tarafı ya tamamen eskiyi ya tamamen yeniyi görür; Adana'nın
yeni, Ankara'nın eski sürümden geldiği bir an yoktur.

---

## Kurulum

**Gerekenler**

- .NET 8 SDK
- Çalışan [HT_SECIM_API](../HT_SECIM_API) (varsayılan `http://localhost:5188`)
- API veritabanında `sql/05_besleme.sql` çalıştırılmış olmalı

**Adımlar**

1. Projeyi derle, çalıştır. Servis `http://localhost:5050` adresinde açılır.
2. `bin\Debug\net8.0\besleme` dosyasını ortamına göre düzenle.
3. Ajans dosyalarını `akis\gelen` klasörüne bırak (ya da üreticiyi aç).

---

## İki mod

Aynı uygulamanın iki modu var. Gerçek ajans bağlandığında `URETICI = 0`
yapılır, **başka hiçbir şey değişmez** — izleyici zaten klasöre bakıyor.

### İZLE — gerçek çalışma

```
akis\gelen\  →  oku  →  denetle  →  API'ye gönder  →  akis\arsiv\
                                                   ↘  akis\hatali\
```

### ÜRET — prova

Gerçek ajans yokken akışı taklit eder: mevcut seçim sonucunu hedef alıp
sıfırdan oraya doğru dosya üretir ve aynı klasöre bırakır. İzleyici farkı
anlamaz; zincirin tamamı gerçek çalışır.

Üç şey taklit ediliyor:

- **Dalgalar** — doğu illeri önce açılır. Türkiye'de oy verme doğu illerinde
  daha erken bittiği için sonuçlar oradan gelir. Sıra `besleme` dosyasındaki
  `DALGA` satırlarından ayarlanır.
- **Eğri** — sayım başta hızlı (küçük sandıklar), sonra yavaşlar.
- **Oynama** — ilk sonuçlar gerçek orandan sapar, sandıklar açıldıkça yakınsar.
  Bir partinin gece başında önde görünüp sonra geri düşmesi, ekranın doğru
  davranıp davranmadığını test etmenin en iyi yolu.

Sapma her tikte yeniden çekilmez; her (il, parti) için **bir kez sabit** bir
sapma çekilip sandıklar açıldıkça silinir. Tik başına rastgele gürültü ekranda
titrer ve sahte görünür. Tohum plakadan türetildiği için **aynı prova her
çalıştırmada aynı akar** — bir hatayı tekrar üretmek mümkündür.

---

## Doğrulama

Dosya iki kademeden geçer.

**Serviste** (dosya seviyesi — API'ye gitmesinin bile anlamı olmayan durumlar):

| Kontrol | Sonuç |
|---|---|
| Sıra numarası eski ya da tekrar | RED |
| Beklenenden az il (`ASGARI_IL`) | RED |
| Açılan sandıkta ani sıçrama (`SICRAMA_UYARI`) | UYARI |

**API'de** (canlı veriyle karşılaştırma gerektirenler):

| Kontrol | Sonuç |
|---|---|
| Açılan sandık ya da oy **geri gitti** | RED — dosyanın tamamı |
| Açılan > toplam sandık | RED |
| Oy toplamı > geçerli oy | RED |
| Kullanılan oy > seçmen sayısı | RED |
| Tanımsız seçim ya da il kodu | RED |
| Tanımsız parti/aday kodu | UYARI — o kalem atlanır |

**Geri gitme koruması en önemlisi.** Bu tek kural, geç gelen eski dosya,
ajansın sıfırlanması ve yanlış dosya gibi hataların çoğunu yakalar. Bir ilde
bile geriye gidiş varsa dosyanın tamamı reddedilir — tek ili düzeltip gerisini
almak, ekranda birbirini tutmayan sayılar bırakırdı.

**Gelmeyen kaleme dokunulmaz.** Ajans bir partiyi o turda göndermediyse
kayıtlı değeri silinmez; eksik gönderim ekranda o partiyi sıfırlamamalı.

---

## Dosya biçimi

```xml
<besleme kaynak="ANKA" uretim="2027-05-14T21:15:00" sira="412">
  <secim kod="MV_2027">
    <il plaka="6" toplamSandik="12658" acilanSandik="9821"
        secmenSayisi="3985412" gecerliOy="3210554" gecersizOy="41208">
      <oy kod="AKP" sayi="1023441" />
      <oy kod="CHP" sayi="922310" />
    </il>
  </secim>
  <secim kod="CB_2027"> ... </secim>
</besleme>
```

Tek dosyada birden fazla seçim taşınabilir — C ekranı CB ve MV'yi aynı anda
gösteriyor. Ajans ayrı dosya veriyorsa uyarlayıcı ikisini birleştirir.

**Dosyada olmayanlar:** yüzdeler, TÜRKİYE geneli satırı (plaka 0),
milletvekili sayıları. Hepsini API hesaplar; gönderilseler bile yok sayılır.

`Okuyucu/XmlOkuyucu.cs` **uyarlayıcı katmandır**. Gerçek ajansın biçimi bundan
farklı olacak; o gün yalnızca bu dosya değişir, doğrulama ve gönderme kodu
olduğu gibi kalır.

---

## FTP'nin klasik tuzağı

Klasörde dosyanın görünmesi, dosyanın **bitmiş** olması demek değil. Yarım
yazılmış dosyayı okuyup yayına vermek bu işin en sık hatasıdır.

Servis iki şartı birden arar:

1. Dosya boyutu `SAKINLIK` saniye boyunca değişmedi
2. Dosya özel erişimle açılabiliyor (başka süreç yazmıyor)

Ajans *"önce `.tmp` yaz, sonra adını değiştir"* yapabiliyorsa `SAKINLIK`
1'e indirilebilir; rename atomiktir. **Bunu ajanstan isteyin.** Üretici de
kendi dosyalarını böyle yazar.

`FileSystemWatcher` kullanılmıyor: ağ sürücüsünde olay kaçırıyor ve yeniden
bağlanma sonrası sessizce durabiliyor. Ajans klasörü neredeyse kesin bir ağ
paylaşımı olacak; 5 saniyede bir taramak hem ucuz hem güvenilir.

---

## Üç farklı sonuç, üç farklı davranış

| Durum | Dosya nereye gider |
|---|---|
| Okunamadı / denetimden geçemedi | `akis\hatali\` — dosya suçlu |
| API reddetti | `akis\hatali\` — veri suçlu |
| **API'ye ulaşılamadı** | **yerinde kalır**, sonraki turda yeniden denenir |

Sonuncusu önemli: bağlantı koptuğu için dosyayı hataliye atmak, API geri
geldiğinde o anlık görüntüyü tamamen kaybetmek olurdu.

---

## Sağlık

`GET http://localhost:5050/durum`

```json
{
  "saglik": "YESIL",
  "sonDosya": { "ad": "prova_00031.xml", "yasSaniye": 5.7,
                "kabul": true, "ilSayisi": 166 },
  "surum": 58,
  "sayaclar": { "kabul": 31, "red": 0, "ustUsteRed": 0 }
}
```

| Renk | Koşul |
|---|---|
| YEŞİL | Son dosya 2 dakikadan yeni |
| SARI | 2-5 dakika, ya da henüz dosya gelmedi |
| KIRMIZI | 5 dakikadan eski **veya üst üste 3 red** |

Üst üste üç red, dosya akıyor olsa bile kırmızıdır: **akan ama kabul
edilmeyen veri, hiç akmayan veriden daha tehlikelidir** — ekranda eski sayılar
durur ve dosyalar geliyor diye kimse şüphelenmez.

API tarafında da kayıt var: `GET /api/v1/besleme/gecmis` — son denemeler,
kabul mü red mi, neden. *"Ajans veri gönderiyor ama ekranda değişmiyor"*
durumunun tek açıklaması burasıdır.

---

## Gece başlamadan: sıfırlama

```
POST /api/v1/besleme/sifirla
{ "secimler": ["CB_2027", "MV_2027"], "onay": "SIFIRLA" }
```

Açılan sandık ve bütün oylar sıfırlanır. **Toplam sandık ve seçmen sayısı
kalır** — onlar geceden önce bilinen değerler, sayılacak olan şey değil,
sayımın paydası.

Kazara çalışmasın diye `onay` alanı tam olarak `"SIFIRLA"` olmalı; bu uç
bütün bir seçimin sayımını siler.

Provada `URETICI_SIFIRLA = 1` ile üretici bunu kendisi çağırır. **Gerçek
yayında 0 olmalı.**

---

## Ayar dosyası

Hepsi `bin\Debug\net8.0\besleme` içinde, düz metin, `//` ile yorum. Adres,
klasör ya da süre değişince **yeniden derlemek gerekmez**. Reji ve C ekranı
uygulamalarıyla aynı biçim — üçünü de aynı kişi ayarlayacak.

| Ayar | Ne için |
|---|---|
| `GELEN` `ARSIV` `HATALI` | Klasörler. Ağ yolu da yazılabilir |
| `DESEN` | İşlenecek dosya deseni (`*.xml`) |
| `TARAMA_ARALIK` | Klasöre kaç saniyede bir bakılacak |
| `SAKINLIK` `BEKLEME_SINIRI` | Yarım dosya koruması |
| `API_ADRES` `API_ANAHTAR` | Gönderim hedefi ve yazma anahtarı |
| `SICRAMA_UYARI` `ASGARI_IL` | Doğrulama eşikleri |
| `URETICI*` | Prova modu |
| `DALGA` | Bölgelerin açılma sırası |

---

## Kaynak düzeni

```
Cekirdek/
  Ayarlar.cs AyarOkuyucu.cs   besleme dosyası
  Kayit.cs                    LOG\ altına günlük dosyalar
  Arsiv.cs                    gelen dosyaları saklar
  Durum.cs                    sağlık durumu
Model/
  Anlik.cs                    ajans biçiminden bağımsız model
Okuyucu/
  XmlOkuyucu.cs               UYARLAYICI — ajans biçimi burada
Gonderici/
  Dogrulayici.cs              dosya seviyesi denetim
  ApiGonderici.cs             API'ye gönderim
Uretici/
  Hedef.cs                    prova hedefi (API'den ya da veri.json'dan)
  AkisUretici.cs              dalgalar, eğri, oynama
  XmlYazici.cs                .tmp yaz → adını değiştir
Servis/
  KlasorIzleyici.cs           tarama döngüsü
  UreticiServisi.cs           prova döngüsü
```

---

## Ölçülen değerler

10 dakikalık prova, 20 saniyede bir dosya:

| | |
|---|---|
| Üretilen / işlenen dosya | 32 / 32 — kayıp yok |
| Reddedilen | 0 |
| Dosya başına | 166 il satırı, 1245 oy satırı |
| Sürüm artışı | 28 → 59, dosya başına tam 1 |
| Toplam yazılan | 39.840 oy satırı |

Sürüm baskılamanın ölçümü:

```
Baskılamasız:  3 ayrı UPDATE  →  sürüm 3 arttı
Baskılamalı:   3 ayrı UPDATE  →  sürüm sabit,  Bitir → 1 arttı
```

---

## Gerçek ajansa geçerken

1. `URETICI = 0`, `URETICI_SIFIRLA = 0`
2. `GELEN` klasörünü ajansın bıraktığı yola çevir (`\\sunucu\...`)
3. `DESEN`'i ajansın dosya adına göre ayarla
4. Ajansın biçimine göre `Okuyucu/XmlOkuyucu.cs` uyarla
5. `ASGARI_IL` ve `SICRAMA_UYARI` eşiklerini gözden geçir
6. Gece başında `POST /api/v1/besleme/sifirla`

**Ajansla konuşurken sorulacaklar:** protokol (FTP/SFTP/HTTPS); tam anlık
görüntü mü fark mı; biçim ve örnek dosya; gönderim sıklığı; dosya adlandırma
ve "tamamlandı" işareti; **ham sayılar geliyor mu** (sandık, seçmen, geçersiz
oy — yalnızca yüzde gönderirlerse mimari değişir); il bazında mı ilçe bazında
mı; geçmiş bir gecenin dosyaları test için alınabilir mi.

---

## İlgili projeler

Zincirin diğer halkaları ayrı depolarda:

| Proje | Ne yapar |
|---|---|
| `HT_SECIM_API` | API + yönetim paneli + SQL şeması. Bu servisin yazdığı yer |
| `HT_SECIM` | Reji uygulaması — 21 sahne, Viz `MAIN_LAYER` |
| `HT_SECIM_C_EKRAN` | C ekranı — 24 saat dönen şeritler, Viz `FRONT_LAYER` |

Bu servis yalnızca API'yi tanır; TV tarafındaki iki uygulamayla doğrudan
konuşmaz. Aralarındaki tek bağ veritabanındaki sürüm numarasıdır.
