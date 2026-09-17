using HT_SECIM_BESLEME.Cekirdek;
using HT_SECIM_BESLEME.Gonderici;
using HT_SECIM_BESLEME.Model;
using HT_SECIM_BESLEME.Okuyucu;

namespace HT_SECIM_BESLEME.Servis;

/// <summary>
/// Gelen klasorunu izler, biten dosyalari okur ve isler.
///
/// FileSystemWatcher DEGIL, duzenli tarama kullaniliyor. Watcher ag
/// surucusunde olay kacirabiliyor ve olaylar yeniden baglanma sonrasi
/// sessizce durabiliyor. Ajans klasoru neredeyse kesin bir ag paylasimi
/// olacak; 5 saniyede bir klasore bakmak hem daha ucuz hem daha
/// guvenilir.
/// </summary>
public class KlasorIzleyici : BackgroundService
{
    /// <summary> Dosya adi -> ilk gorulme ani. Bekleme siniri icin. </summary>
    private readonly Dictionary<string, DateTime> ilkGorulme = new();

    /// <summary> Dosya adi -> son olculen boyut. Sakinlik kontrolu icin. </summary>
    private readonly Dictionary<string, long> sonBoyut = new();

    /// <summary> Dosya adi -> boyutun en son degistigi an. </summary>
    private readonly Dictionary<string, DateTime> sonDegisim = new();

    private readonly Dogrulayici dogrulayici = new();
    private readonly ApiGonderici gonderici = new();

    public override void Dispose()
    {
        gonderici.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken iptal)
    {
        Kayit.Bilgi("IZLEYICI BASLADI", $"{Ayarlar.Gelen} / {Ayarlar.Desen}");

        while (!iptal.IsCancellationRequested)
        {
            try
            {
                await TaraAsync(iptal);
            }
            catch (Exception ex)
            {
                // Tarama dongusu ASLA olmemeli: ag surucusu bir an
                // kaybolsa bile servis ayakta kalip geri gelmesini
                // beklemeli.
                Kayit.Hata("TARAMA HATASI", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(Ayarlar.TaramaAralik), iptal);
        }
    }

    private async Task TaraAsync(CancellationToken iptal)
    {
        if (Ayarlar.Gelen.Length == 0 || !Directory.Exists(Ayarlar.Gelen)) return;

        // Eski dosya once islensin: ajans birden fazla dosya birakmis
        // olabilir ve sira onemli.
        IEnumerable<string> dosyalar = Directory
            .EnumerateFiles(Ayarlar.Gelen, Ayarlar.Desen)
            .OrderBy(File.GetLastWriteTimeUtc);

        foreach (string dosya in dosyalar)
        {
            if (iptal.IsCancellationRequested) return;
            if (!Bitti(dosya)) continue;

            Unut(dosya);
            await IsleAsync(dosya, iptal);
        }
    }

    /// <summary>
    /// Dosya yazilmasi bitti mi.
    ///
    /// FTP'NIN KLASIK TUZAGI: klasorde dosyanin gorunmesi, dosyanin
    /// bitmis olmasi demek degil. Yarim yazilmis dosyayi okuyup yayina
    /// vermek bu isin en sik hatasi.
    ///
    /// Iki sart birden araniyor:
    ///   1. Boyut SAKINLIK saniye boyunca degismedi
    ///   2. Dosya ozel erisimle acilabiliyor (baska surec yazmiyor)
    ///
    /// Ikincisi tek basina yeterli gorunebilir ama FTP sunuculari
    /// dosyayi parca parca yazip arada kapatabiliyor; boyut sakinligi
    /// o durumu yakaliyor.
    /// </summary>
    private bool Bitti(string dosya)
    {
        string ad = Path.GetFileName(dosya);
        DateTime simdi = DateTime.Now;

        if (!ilkGorulme.ContainsKey(ad)) ilkGorulme[ad] = simdi;

        long boyut;

        try
        {
            boyut = new FileInfo(dosya).Length;
        }
        catch
        {
            // Dosya tam bu anda tasiniyor ya da kilitli; sonraki turda.
            return false;
        }

        if (!sonBoyut.TryGetValue(ad, out long onceki) || onceki != boyut)
        {
            sonBoyut[ad]   = boyut;
            sonDegisim[ad] = simdi;
            return false;
        }

        // Yarim kalmis yuklemeler sonsuza kadar beklenmiyor.
        if ((simdi - ilkGorulme[ad]).TotalSeconds > Ayarlar.BeklemeSiniri)
        {
            Kayit.Hata("DOSYA BITMEDI",
                $"{ad} - {Ayarlar.BeklemeSiniri} sn boyunca tamamlanmadi, hatali klasorune alindi");

            Unut(dosya);
            Arsiv.Reddet(dosya);
            return false;
        }

        if ((simdi - sonDegisim[ad]).TotalSeconds < Ayarlar.Sakinlik) return false;

        return YazilmiyorMu(dosya);
    }

    /// <summary>
    /// Dosyayi paylasimsiz acmayi deniyor. Acilabiliyorsa baska bir
    /// surec uzerinde yazmiyor demektir.
    /// </summary>
    private static bool YazilmiyorMu(string dosya)
    {
        try
        {
            using FileStream akis = File.Open(dosya, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void Unut(string dosya)
    {
        string ad = Path.GetFileName(dosya);

        ilkGorulme.Remove(ad);
        sonBoyut.Remove(ad);
        sonDegisim.Remove(ad);
    }

    /// <summary>
    /// Dosyayi oku, denetle, API'ye gonder, sonucuna gore tasi.
    ///
    /// Uc farkli sonuc var ve ucu de farkli davranis istiyor:
    ///
    ///   okunamadi / denetimden gecemedi -> HATALI klasoru (dosya suclu)
    ///   API reddetti                    -> HATALI klasoru (veri suclu)
    ///   API'ye ulasilamadi              -> DOSYA YERINDE KALIR
    ///
    /// Sonuncusu onemli: baglanti koptugu icin dosyayi hataliya atmak,
    /// API geri geldiginde o anlik goruntuyu tamamen kaybetmek olurdu.
    /// Yerinde kalan dosya sonraki turda yeniden denenir.
    /// </summary>
    private async Task IsleAsync(string dosya, CancellationToken iptal)
    {
        string ad = Path.GetFileName(dosya);

        Anlik? anlik = XmlOkuyucu.Oku(dosya);

        if (anlik == null)
        {
            Durum.Red(ad, 0, "okunamadi");
            Arsiv.Reddet(dosya);
            return;
        }

        Kayit.Bilgi("DOSYA GELDI", $"{ad} / {anlik}");

        // --- dosya seviyesi denetim
        Dogrulayici.Sonuc denetim = dogrulayici.Denetle(anlik);

        foreach (string uyari in denetim.Uyarilar) Kayit.Uyari("SIÇRAMA", uyari);

        if (!denetim.Gecerli)
        {
            Kayit.Hata("DOSYA REDDEDILDI", $"{ad} - {denetim.Sebep}");

            Durum.Red(ad, anlik.Sira, denetim.Sebep);
            Arsiv.Reddet(dosya);
            return;
        }

        // --- API'ye gonder
        ApiGonderici.Cevap? cevap = await gonderici.GonderAsync(anlik, iptal);

        if (cevap == null)
        {
            // Baglanti sorunu: dosya yerinde kaliyor, sonraki turda
            // yeniden denenecek.
            Kayit.Uyari("GONDERILEMEDI", $"{ad} - dosya beklemede, yeniden denenecek");
            return;
        }

        foreach (string uyari in cevap.Uyarilar) Kayit.Uyari("API UYARISI", uyari);

        if (!cevap.Kabul)
        {
            Kayit.Hata("API REDDETTI", $"{ad} - {cevap.Aciklama}");

            Durum.Red(ad, anlik.Sira, cevap.Aciklama);
            Arsiv.Reddet(dosya);
            return;
        }

        dogrulayici.KabulEdildi(anlik);
        Durum.Kabul(ad, anlik.Sira, cevap.Surum, cevap.IlSayisi, cevap.Aciklama);

        Kayit.Bilgi("KABUL", $"{ad} / sürüm {cevap.Surum} / {Ozet(anlik)}");

        Arsiv.Kabul(dosya);
    }

    private static string Ozet(Anlik anlik)
    {
        List<string> parcalar = new();

        foreach (AnlikSecim secim in anlik.Secimler)
        {
            int acilan = secim.Iller.Sum(i => i.AcilanSandik);
            int toplam = secim.Iller.Sum(i => i.ToplamSandik);

            string oran = toplam > 0 ? $"%{100.0 * acilan / toplam:0.0}" : "-";
            parcalar.Add($"{secim.Kod} {oran}");
        }

        return string.Join(" / ", parcalar);
    }
}
