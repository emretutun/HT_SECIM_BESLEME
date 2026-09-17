using System.Globalization;
using System.Xml.Linq;
using HT_SECIM_BESLEME.Cekirdek;
using HT_SECIM_BESLEME.Model;

namespace HT_SECIM_BESLEME.Okuyucu;

/// <summary>
/// Ajans XML'ini <see cref="Anlik"/> nesnesine cevirir.
///
/// UYARLAYICI KATMANI BURASI. Gercek ajansin bicimi bundan farkli
/// olacak; o gun yalnizca bu dosya degisir, dogrulama ve gonderme
/// kodu oldugu gibi kalir.
///
/// Beklenen bicim:
///
///   &lt;besleme kaynak="ANKA" uretim="2027-05-14T21:15:00" sira="412"&gt;
///     &lt;secim kod="MV_2027"&gt;
///       &lt;il plaka="6" toplamSandik="12658" acilanSandik="9821"
///           secmenSayisi="3985412" gecerliOy="3210554" gecersizOy="41208"&gt;
///         &lt;oy kod="AKP" sayi="1023441" /&gt;
///       &lt;/il&gt;
///     &lt;/secim&gt;
///   &lt;/besleme&gt;
///
/// Cozulemeyen dosya icin istisna firlatilmiyor; null donuyor ve neden
/// gunluge yaziliyor. Cagiran dosyayi hatali klasorune tasiyip devam
/// ediyor - tek bozuk dosya yuzunden servis durmamali.
/// </summary>
public static class XmlOkuyucu
{
    public static Anlik? Oku(string dosya)
    {
        XDocument belge;

        try
        {
            belge = XDocument.Load(dosya);
        }
        catch (Exception ex)
        {
            Kayit.Hata("XML COZULEMEDI", $"{Path.GetFileName(dosya)} - {ex.Message}");
            return null;
        }

        XElement? kok = belge.Root;

        if (kok == null || !kok.Name.LocalName.Equals("besleme", StringComparison.OrdinalIgnoreCase))
        {
            Kayit.Hata("BEKLENMEYEN XML", $"{Path.GetFileName(dosya)} - kok eleman 'besleme' degil");
            return null;
        }

        Anlik anlik = new()
        {
            DosyaAdi = Path.GetFileName(dosya),
            Kaynak   = Metin(kok, "kaynak"),
            Sira     = Tamsayi(kok, "sira", 0),
            Uretim   = Zaman(kok, "uretim")
        };

        foreach (XElement secimDugumu in kok.Elements().Where(e => Ad(e, "secim")))
        {
            AnlikSecim secim = new() { Kod = Metin(secimDugumu, "kod") };

            if (secim.Kod.Length == 0)
            {
                Kayit.Uyari("SECIM KODU BOS", anlik.DosyaAdi + " - bu blok atlandi");
                continue;
            }

            foreach (XElement ilDugumu in secimDugumu.Elements().Where(e => Ad(e, "il")))
            {
                AnlikIl? il = IlOku(ilDugumu, anlik.DosyaAdi, secim.Kod);
                if (il != null) secim.Iller.Add(il);
            }

            anlik.Secimler.Add(secim);
        }

        if (anlik.Secimler.Count == 0)
        {
            Kayit.Hata("BOS DOSYA", $"{anlik.DosyaAdi} - icinde secim yok");
            return null;
        }

        return anlik;
    }

    private static AnlikIl? IlOku(XElement dugum, string dosyaAdi, string secimKod)
    {
        int plaka = (int)Tamsayi(dugum, "plaka", -1);

        if (plaka < 0)
        {
            Kayit.Uyari("PLAKA OKUNAMADI", $"{dosyaAdi} / {secimKod} - satir atlandi");
            return null;
        }

        // Turkiye geneli satirini ajans gonderse bile almiyoruz: API
        // illerden topluyor. Iki kaynaktan gelen toplam birbirini
        // tutmazsa ekranin ustu ile alti celisir.
        if (plaka == 0)
        {
            Kayit.Ayrinti("TURKIYE SATIRI ATLANDI", $"{dosyaAdi} / {secimKod}");
            return null;
        }

        AnlikIl il = new()
        {
            Plaka        = plaka,
            ToplamSandik = (int)Tamsayi(dugum, "toplamSandik", 0),
            AcilanSandik = (int)Tamsayi(dugum, "acilanSandik", 0),
            SecmenSayisi = (int)Tamsayi(dugum, "secmenSayisi", 0),
            GecerliOy    = Tamsayi(dugum, "gecerliOy", 0),
            GecersizOy   = Tamsayi(dugum, "gecersizOy", 0)
        };

        foreach (XElement oyDugumu in dugum.Elements().Where(e => Ad(e, "oy")))
        {
            string kod = Metin(oyDugumu, "kod");
            if (kod.Length == 0) continue;

            il.Oylar.Add(new AnlikOy { Kod = kod, Sayi = Tamsayi(oyDugumu, "sayi", 0) });
        }

        return il;
    }

    private static bool Ad(XElement e, string ad)
        => e.Name.LocalName.Equals(ad, StringComparison.OrdinalIgnoreCase);

    /// <summary> Oznitelik adi buyuk/kucuk harf duyarsiz araniyor. </summary>
    private static XAttribute? Oznitelik(XElement e, string ad)
        => e.Attributes().FirstOrDefault(a =>
               a.Name.LocalName.Equals(ad, StringComparison.OrdinalIgnoreCase));

    private static string Metin(XElement e, string ad)
        => Oznitelik(e, ad)?.Value.Trim() ?? "";

    /// <summary>
    /// Sayilar HER ZAMAN InvariantCulture ile okunuyor.
    /// Turkce kulturde "1.234" bin iki yuz otuz dort degil, bir nokta
    /// iki uc dort olarak yorumlanabiliyor; ajans dosyasi kulturden
    /// bagimsiz okunmali.
    /// </summary>
    private static long Tamsayi(XElement e, string ad, long varsayilan)
    {
        string ham = Metin(e, ad);
        if (ham.Length == 0) return varsayilan;

        return long.TryParse(ham, NumberStyles.Integer, CultureInfo.InvariantCulture, out long sonuc)
            ? sonuc : varsayilan;
    }

    /// <summary>
    /// Uretim zamani okunamazsa "simdi" kabul ediliyor.
    /// Zaman damgasi bozuk diye dosyayi reddetmenin anlami yok; sayilar
    /// saglamsa yayina girmeli.
    /// </summary>
    private static DateTime Zaman(XElement e, string ad)
    {
        string ham = Metin(e, ad);

        return DateTime.TryParse(ham, CultureInfo.InvariantCulture,
                   DateTimeStyles.AssumeLocal, out DateTime sonuc)
            ? sonuc : DateTime.Now;
    }
}
