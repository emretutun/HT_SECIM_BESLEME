using HT_SECIM_BESLEME.Cekirdek;
using HT_SECIM_BESLEME.Model;

namespace HT_SECIM_BESLEME.Gonderici;

/// <summary>
/// API'ye gitmeden once yapilan kontroller.
///
/// Asil dogrulama (geri gitme, tutarlilik, tanimsiz kod) API'de:
/// orada canli veriyle karsilastirilabiliyor ve veriyi degistiren her
/// sey tek kapidan geciyor. Buradakiler DOSYA SEVIYESI kontroller -
/// API'ye gonderilmesinin bile anlami olmayan durumlar:
///
///   - Eski ya da tekrar gonderilmis dosya (sira numarasi)
///   - Yarim liste (asgari il sayisi)
///   - Ani siçrama (red degil, UYARI)
/// </summary>
public sealed class Dogrulayici
{
    /// <summary> Kabul edilen son dosyanin sira numarasi. </summary>
    private long sonSira = -1;

    /// <summary> (secim|plaka) -> son kabul edilen acilan sandik orani. </summary>
    private readonly Dictionary<string, double> sonOran = new();

    public sealed class Sonuc
    {
        public bool Gecerli { get; init; }
        public string Sebep { get; init; } = "";
        public List<string> Uyarilar { get; } = new();
    }

    public Sonuc Denetle(Anlik anlik)
    {
        Sonuc sonuc = new() { Gecerli = true };

        // --- eski ya da tekrar dosya
        //
        // Ajans sira numarasi vermiyorsa (0) bu kontrol atlanir; numarasiz
        // beslemede eski dosyayi yakalamanin tek yolu API'deki geri gitme
        // korumasi olur, o da zaten devrede.
        if (anlik.Sira > 0 && sonSira >= 0 && anlik.Sira <= sonSira)
        {
            return new Sonuc
            {
                Gecerli = false,
                Sebep = $"eski ya da tekrar dosya: sira {anlik.Sira}, son kabul edilen {sonSira}"
            };
        }

        // --- yarim liste
        foreach (AnlikSecim secim in anlik.Secimler)
        {
            if (secim.Iller.Count < Ayarlar.AsgariIl)
            {
                return new Sonuc
                {
                    Gecerli = false,
                    Sebep = $"{secim.Kod}: {secim.Iller.Count} il geldi, en az {Ayarlar.AsgariIl} bekleniyordu"
                };
            }
        }

        // --- ani siçrama: gecirilir ama operator gormeli
        foreach (AnlikSecim secim in anlik.Secimler)
        foreach (AnlikIl il in secim.Iller)
        {
            if (il.ToplamSandik <= 0) continue;

            double oran = 100.0 * il.AcilanSandik / il.ToplamSandik;
            string anahtar = $"{secim.Kod}|{il.Plaka}";

            if (sonOran.TryGetValue(anahtar, out double onceki) &&
                oran - onceki > Ayarlar.SicramaUyari)
            {
                sonuc.Uyarilar.Add(
                    $"{secim.Kod}/{il.Plaka}: açılan sandık %{onceki:0.0} → %{oran:0.0} (ani sıçrama)");
            }
        }

        return sonuc;
    }

    /// <summary>
    /// Kabul edilen dosyadan sonra cagriliyor.
    ///
    /// Kabul EDILMEYEN dosyanin degerleri hatirlanmiyor: reddedilen bir
    /// dosya veritabanina yazilmadi, onu temel almak sonraki dosyalari
    /// yanlis yerden olcmek olur.
    /// </summary>
    public void KabulEdildi(Anlik anlik)
    {
        sonSira = anlik.Sira;

        foreach (AnlikSecim secim in anlik.Secimler)
        foreach (AnlikIl il in secim.Iller)
        {
            if (il.ToplamSandik <= 0) continue;

            sonOran[$"{secim.Kod}|{il.Plaka}"] = 100.0 * il.AcilanSandik / il.ToplamSandik;
        }
    }
}
