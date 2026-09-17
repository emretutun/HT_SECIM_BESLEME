using System.Text;

namespace HT_SECIM_BESLEME.Cekirdek;

/// <summary>
/// "anahtar = deger = deger ..." satirlarini okuyan basit config okuyucu.
/// "//" ve "#" ile baslayan satirlari ve bos satirlari atlar.
///
/// Reji ve C ekrani uygulamalarindaki ConfigReader ile ayni bicim;
/// ucunu ayni kisi ayarlayacak, bicimlerin farkli olmasi karisiklik olur.
/// </summary>
public static class AyarOkuyucu
{
    public static List<string[]> Satirlar(string dosya)
    {
        List<string[]> sonuc = new();

        if (!File.Exists(dosya))
        {
            Kayit.Hata("AYAR DOSYASI YOK", dosya);
            return sonuc;
        }

        string[] ham;

        try
        {
            // BOM varsa ona gore, yoksa UTF-8 kabul ediliyor.
            ham = File.ReadAllLines(dosya, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Kayit.Hata("AYAR DOSYASI OKUNAMADI", $"{dosya} - {ex.Message}");
            return sonuc;
        }

        foreach (string satir in ham)
        {
            string temiz = satir.Trim();

            if (temiz.Length == 0) continue;
            if (temiz.StartsWith("//")) continue;
            if (temiz.StartsWith("#")) continue;
            if (!temiz.Contains('=')) continue;

            string[] parcalar = temiz.Split('=');
            for (int i = 0; i < parcalar.Length; i++) parcalar[i] = parcalar[i].Trim();

            sonuc.Add(parcalar);
        }

        return sonuc;
    }
}
