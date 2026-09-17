using System.Text;

namespace HT_SECIM_BESLEME.Cekirdek;

/// <summary>
/// Gunluk. Hem konsola hem LOG klasorune yaziyor.
///
/// ILogger yerine bu: reji ve C ekrani uygulamalari da ayni duzende
/// LOG\actions_YYYY_MM_DD.log yaziyor. Yayin gecesi bir sorun
/// arastirilirken uc uygulamanin gunluklerinin ayni yerde, ayni
/// bicimde olmasi cok sey kazandiriyor.
///
/// Hatalar AYRICA error dosyasina da yaziliyor; gece yarisi
/// "bir sorun var mi" sorusunun cevabi tek dosyada duruyor.
/// </summary>
public static class Kayit
{
    private static readonly object kilit = new();

    private static string Klasor => Path.Combine(AppContext.BaseDirectory, "LOG");

    /// <summary> Normal akis: dosya geldi, kabul edildi, gonderildi. </summary>
    public static void Bilgi(string baslik, string ayrinti = "")
        => Yaz("actions", baslik, ayrinti, ConsoleColor.Gray);

    /// <summary> Operatorun gormesi gereken durumlar. </summary>
    public static void Uyari(string baslik, string ayrinti = "")
    {
        Yaz("actions", baslik, ayrinti, ConsoleColor.Yellow);
        Yaz("error", baslik, ayrinti, null);
    }

    /// <summary> Dosya reddedildi, klasor acilamadi, API cevap vermedi. </summary>
    public static void Hata(string baslik, string ayrinti = "")
    {
        Yaz("actions", baslik, ayrinti, ConsoleColor.Red);
        Yaz("error", baslik, ayrinti, null);
    }

    /// <summary> Ayrinti: yalnizca debug dosyasina, konsolu bogmasin. </summary>
    public static void Ayrinti(string baslik, string ayrinti = "")
        => Yaz("debug", baslik, ayrinti, null);

    private static void Yaz(string tur, string baslik, string ayrinti, ConsoleColor? renk)
    {
        string zaman = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss.fff");
        string satir = ayrinti.Length > 0
            ? $"[{zaman}]{{{baslik}}}{{{ayrinti}}}"
            : $"[{zaman}]{{{baslik}}}";

        if (renk.HasValue)
        {
            lock (kilit)
            {
                ConsoleColor eski = Console.ForegroundColor;
                Console.ForegroundColor = renk.Value;
                Console.WriteLine(satir);
                Console.ForegroundColor = eski;
            }
        }

        try
        {
            lock (kilit)
            {
                Directory.CreateDirectory(Klasor);

                string dosya = Path.Combine(Klasor,
                    $"{tur}_{DateTime.Now:yyyy_MM_dd}.log");

                File.AppendAllText(dosya, satir + Environment.NewLine, new UTF8Encoding(false));
            }
        }
        catch
        {
            // Gunluk yazamamak servisi durdurmaz. Disk dolu ya da klasor
            // kilitliyse bile besleme akmaya devam etmeli - yayin surer,
            // kayit tutulamaz. Tersi kabul edilemez.
        }
    }
}
