using HT_SECIM_BESLEME.Cekirdek;
using HT_SECIM_BESLEME.Model;

namespace HT_SECIM_BESLEME.Uretici;

/// <summary>
/// Hedef sonuca dogru ilerleyen sahte bir secim akisi uretir.
///
/// Amac "rastgele sayi uretmek" degil, GERCEGE BENZEYEN bir gece
/// uretmek. Uc sey taklit ediliyor:
///
///   1. Dalgalar   - dogu illeri once acilir; Turkiye'de oy verme
///                   dogu illerinde daha erken bitiyor.
///   2. Egri       - sayim basta hizli (kucuk sandiklar), sonra yavaslar.
///   3. Oynama     - ilk sonuclar gercek orandan sapar, sandiklar
///                   acildikca gercege yakinsar. Bir partinin gece
///                   basinda onde gorunup sonra geri dusmesi tam da
///                   ekranin dogru davranip davranmadigini test eder.
///
/// MONOTONLUK GARANTI: uretilen her deger bir oncekinden kucuk olamaz.
/// Gercek besleme de boyledir; ayrica kendi dogrulayicimizin geri
/// gitme korumasina takilmamak icin sart.
/// </summary>
public class AkisUretici
{
    private readonly Hedef hedef;

    /// <summary> (secim, plaka, parti) -> son uretilen oy. Monotonluk icin. </summary>
    private readonly Dictionary<string, long> sonOy = new();

    /// <summary> (secim, plaka) -> son uretilen acilan sandik. </summary>
    private readonly Dictionary<string, int> sonAcilan = new();

    /// <summary> (secim, plaka, parti) -> o partinin sabit sapmasi. </summary>
    private readonly Dictionary<string, double> sapmalar = new();

    private long sira;

    public AkisUretici(Hedef hedef)
    {
        this.hedef = hedef;
        SapmalariKur();
    }

    /// <summary>
    /// Her (il, parti) icin bir kez sabit bir sapma cekiliyor.
    ///
    /// Tik basina rastgele gurultu yerine SABIT sapma kullanmanin sebebi:
    /// gurultu her karede zipladigi icin ekranda sayilar titrer ve sahte
    /// gorunur. Sabit sapma, sonuca dogru duzgun bir yakinsama uretiyor -
    /// gercek gecelerde de oyle olur.
    ///
    /// Tohum plakadan turetiliyor; ayni prova her calistirmada ayni
    /// sekilde akiyor, bir hatayi tekrar uretmek mumkun oluyor.
    /// </summary>
    private void SapmalariKur()
    {
        foreach (HedefSecim secim in hedef.Secimler)
        foreach (HedefIl il in secim.Iller)
        {
            Random rastgele = new(il.Plaka * 7919 + secim.Kod.GetHashCode(StringComparison.Ordinal) % 1000);

            foreach (HedefOy oy in il.Oylar)
                sapmalar[Anahtar(secim.Kod, il.Plaka, oy.Kod)] = (rastgele.NextDouble() - 0.5) * 0.7;
        }
    }

    /// <summary>
    /// Verilen ilerleme oraninda (0..1) bir anlik goruntu uretir.
    /// </summary>
    public Anlik Uret(double ilerleme)
    {
        ilerleme = Math.Clamp(ilerleme, 0, 1);
        sira++;

        Anlik anlik = new()
        {
            Kaynak = Ayarlar.UreticiKaynak,
            Uretim = DateTime.Now,
            Sira   = sira
        };

        foreach (HedefSecim hs in hedef.Secimler)
        {
            AnlikSecim secim = new() { Kod = hs.Kod };

            foreach (HedefIl il in hs.Iller)
                secim.Iller.Add(IlUret(hs.Kod, il, ilerleme));

            anlik.Secimler.Add(secim);
        }

        return anlik;
    }

    private AnlikIl IlUret(string secimKod, HedefIl il, double ilerleme)
    {
        double p = IlIlerlemesi(il, ilerleme);

        AnlikIl cikti = new()
        {
            Plaka        = il.Plaka,
            ToplamSandik = il.ToplamSandik,
            SecmenSayisi = il.SecmenSayisi
        };

        // --- acilan sandik, geri gitmeden
        int acilan = (int)Math.Round(il.HedefAcilan * p);
        string ilAnahtar = $"{secimKod}|{il.Plaka}";

        if (sonAcilan.TryGetValue(ilAnahtar, out int oncekiAcilan))
            acilan = Math.Max(acilan, oncekiAcilan);

        sonAcilan[ilAnahtar] = acilan;
        cikti.AcilanSandik = acilan;

        // --- oylar: gercek paya sapma eklenip normalize ediliyor
        double toplamPay = 0;
        Dictionary<string, double> paylar = new();

        foreach (HedefOy oy in il.Oylar)
        {
            double gercekPay = (double)oy.Oy / il.GecerliOy;

            // Sapma p ilerledikce siliniyor: gece sonunda gercek oran.
            double sapma = sapmalar.GetValueOrDefault(Anahtar(secimKod, il.Plaka, oy.Kod));
            double pay = gercekPay * (1 + sapma * (1 - p));

            if (pay < 0) pay = 0;

            paylar[oy.Kod] = pay;
            toplamPay += pay;
        }

        if (toplamPay <= 0) toplamPay = 1;

        long sayilanGecerli = (long)Math.Round(il.GecerliOy * p);
        long dagitilan = 0;

        foreach (HedefOy oy in il.Oylar)
        {
            long sayi = (long)Math.Round(sayilanGecerli * (paylar[oy.Kod] / toplamPay));

            string oyAnahtar = Anahtar(secimKod, il.Plaka, oy.Kod);
            if (sonOy.TryGetValue(oyAnahtar, out long onceki)) sayi = Math.Max(sayi, onceki);

            // Hicbir parti nihai oyunu asamaz.
            sayi = Math.Min(sayi, oy.Oy);

            sonOy[oyAnahtar] = sayi;
            cikti.Oylar.Add(new AnlikOy { Kod = oy.Kod, Sayi = sayi });

            dagitilan += sayi;
        }

        // Gecerli oy, dagitilan oylarin toplamindan kucuk olamaz -
        // yoksa kendi dogrulayicimiz dosyayi reddeder.
        cikti.GecerliOy  = Math.Max(sayilanGecerli, dagitilan);
        cikti.GecersizOy = (long)Math.Round(il.GecersizOy * p);

        return cikti;
    }

    /// <summary>
    /// Bir ilin kendi ilerlemesi.
    ///
    /// Once bolgesinin dalgasi bekleniyor, sonra sayim basliyor.
    /// Egri p^0.65: basta hizli, sonra yavas - kucuk sandiklar once
    /// biter, buyuk merkezler en son.
    /// </summary>
    private static double IlIlerlemesi(HedefIl il, double genel)
    {
        double gecikme = Ayarlar.Dalgalar.GetValueOrDefault(il.Bolge, 0) / 100.0;

        // Ayni bolgedeki iller de ayni anda acilmasin diye kucuk bir kaydirma.
        gecikme += (il.Plaka % 7) / 100.0;

        if (gecikme >= 0.9) gecikme = 0.9;
        if (genel <= gecikme) return 0;

        double p = (genel - gecikme) / (1 - gecikme);

        return Math.Pow(Math.Clamp(p, 0, 1), 0.65);
    }

    private static string Anahtar(string secim, int plaka, string kod) => $"{secim}|{plaka}|{kod}";
}
