using HT_SECIM_BESLEME.Cekirdek;
using HT_SECIM_BESLEME.Gonderici;
using HT_SECIM_BESLEME.Model;
using HT_SECIM_BESLEME.Uretici;

namespace HT_SECIM_BESLEME.Servis;

/// <summary>
/// Prova modu: gercek ajansin yerine gecip GELEN klasorune dosya birakir.
///
/// Izleyici bu dosyalarin nereden geldigini bilmiyor - ajanstan gelmis
/// gibi isliyor. Boylece zincirin tamami (dosya -> dogrulama -> API ->
/// C ekrani -> Viz) gercek sekilde calisiyor, hicbir yeri taklit
/// edilmiyor. Gercek ajans baglaninca URETICI = 0 yapilir, baska
/// hicbir sey degismez.
/// </summary>
public class UreticiServisi : BackgroundService
{
    /// <summary> Servis basladiktan sonra uretimin baslamasi icin beklenen sure. </summary>
    private static readonly TimeSpan IlkGecikme = TimeSpan.FromSeconds(3);

    public double Ilerleme { get; private set; }
    public long UretilenDosya { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken iptal)
    {
        if (!Ayarlar.Uretici)
        {
            Kayit.Bilgi("URETICI KAPALI", "gercek ajans bekleniyor");
            return;
        }

        await Task.Delay(IlkGecikme, iptal);

        Hedef? hedef = Hedef.Yukle();

        if (hedef == null)
        {
            Kayit.Hata("URETICI BASLAMADI",
                "hedef veri alinamadi - API acik mi? (hedef.json de yok)");
            return;
        }

        // Sayaclari sifirla: veritabaninda nihai sonuclar duruyor, prova
        // sifirdan basliyor. Sifirlanmazsa geri gitme korumasi her
        // dosyayi hakli olarak reddeder.
        if (Ayarlar.UreticiSifirla)
        {
            List<string> secimler = hedef.Secimler.Select(s => s.Kod).ToList();

            using ApiGonderici gonderici = new();

            if (!await gonderici.SifirlaAsync(secimler, iptal))
            {
                Kayit.Hata("URETICI BASLAMADI",
                    "sayaclar sifirlanamadi - sifirlanmadan uretilen dosyalar reddedilir");
                return;
            }
        }

        AkisUretici uretici = new(hedef);

        DateTime baslangic = DateTime.Now;
        TimeSpan toplam = TimeSpan.FromMinutes(Ayarlar.UreticiSure);

        Kayit.Bilgi("URETICI BASLADI",
            $"{Ayarlar.UreticiSure} dk / {Ayarlar.UreticiAralik} sn'de bir dosya");

        bool bitti = false;

        while (!iptal.IsCancellationRequested)
        {
            try
            {
                double gecen = (DateTime.Now - baslangic).TotalSeconds;
                Ilerleme = Math.Clamp(gecen / toplam.TotalSeconds, 0, 1);

                Anlik anlik = uretici.Uret(Ilerleme);

                if (XmlYazici.Yaz(anlik, Ayarlar.Gelen))
                {
                    UretilenDosya++;
                    Kayit.Bilgi("URETILDI",
                        $"{anlik.DosyaAdi} / ilerleme %{Ilerleme * 100:0.0} / {Ozet(anlik)}");
                }

                // Son dosya da uretildikten sonra duruluyor; gece bitti.
                if (Ilerleme >= 1)
                {
                    if (bitti) break;
                    bitti = true;
                }
            }
            catch (Exception ex)
            {
                // Uretim dongusu de olmemeli: tek bir hatali tur provayi
                // bitirmesin.
                Kayit.Hata("URETIM HATASI", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(Ayarlar.UreticiAralik), iptal);
        }

        Kayit.Bilgi("URETICI BITTI", $"{UretilenDosya} dosya uretildi");
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
