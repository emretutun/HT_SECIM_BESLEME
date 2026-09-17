namespace HT_SECIM_BESLEME.Cekirdek;

/// <summary>
/// Islenen dosyalari saklar.
///
/// Gelen HER dosya saklaniyor - kabul edilen de reddedilen de. Iki
/// sebep var:
///
///   1. "Saat 21:15'te ekranda neden 43,4 yaziyordu" sorusunun cevabi
///      yalnizca o anki dosyada.
///   2. Prova: kaydedilmis gercek bir gece, hizlandirilmis olarak
///      tekrar oynatilabiliyor. Zincirin tamamini gercek veriyle
///      denemenin baska yolu yok.
///
/// Dosya adinin basina zaman damgasi ekleniyor; ajans ayni adi tekrar
/// kullansa bile arsivde uzerine yazilmiyor.
/// </summary>
public static class Arsiv
{
    /// <summary> Kabul edilen dosyayi arsive tasir. </summary>
    public static void Kabul(string dosya) => Tasi(dosya, Ayarlar.Arsiv, "arsiv");

    /// <summary> Reddedilen dosyayi hatali klasorune tasir. </summary>
    public static void Reddet(string dosya) => Tasi(dosya, Ayarlar.Hatali, "hatali");

    private static void Tasi(string dosya, string hedefKlasor, string etiket)
    {
        if (hedefKlasor.Length == 0) return;

        try
        {
            Directory.CreateDirectory(hedefKlasor);

            string ad = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Path.GetFileName(dosya)}";
            string hedef = Path.Combine(hedefKlasor, ad);

            File.Move(dosya, hedef, overwrite: true);

            Kayit.Ayrinti("DOSYA TASINDI", $"{Path.GetFileName(dosya)} -> {etiket}");
        }
        catch (Exception ex)
        {
            // Tasiyamamak islemeyi bozmaz ama dosya gelen klasorunde
            // kalirsa sonsuz donguye girer; o yuzden hata seviyesinde.
            Kayit.Hata("DOSYA TASINAMADI", $"{Path.GetFileName(dosya)} - {ex.Message}");
        }
    }
}
