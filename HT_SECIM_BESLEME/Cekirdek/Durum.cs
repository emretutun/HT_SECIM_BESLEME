namespace HT_SECIM_BESLEME.Cekirdek;

/// <summary>
/// Servisin o anki sagligi.
///
/// SESSIZ BASARISIZLIK en kotusudur: ajans 40 dakikadir dosya
/// gondermiyorsa ekran eski veriyi guvenle gostermeye devam eder ve
/// kimse fark etmez. Buradaki degerler tam olarak bunu gorunur kilmak
/// icin var.
///
/// Birden fazla thread'den yaziliyor (izleyici ve uretici), okuma
/// /durum ucundan geliyor; o yuzden kilit altinda.
/// </summary>
public static class Durum
{
    private static readonly object kilit = new();

    public static DateTime Baslangic { get; } = DateTime.Now;

    // ------------------------------------------------------- son dosya

    public static DateTime? SonDosyaZamani { get; private set; }
    public static string SonDosyaAdi { get; private set; } = "";
    public static bool SonKabul { get; private set; }
    public static string SonAciklama { get; private set; } = "";
    public static long SonSira { get; private set; }

    /// <summary> Son kabul edilen anlik goruntunun API surumu. </summary>
    public static int SonSurum { get; private set; }

    // -------------------------------------------------------- sayaclar

    public static long ToplamKabul { get; private set; }
    public static long ToplamRed { get; private set; }

    /// <summary>
    /// Ust uste kac dosya reddedildi. Ucten buyukse ciddi bir sorun var:
    /// ajans bicimi degismis ya da veri bozulmus olabilir.
    /// </summary>
    public static int UstUsteRed { get; private set; }

    /// <summary> Son kabul edilen dosyadaki il satiri sayisi. </summary>
    public static int SonIlSayisi { get; private set; }

    public static void Kabul(string dosya, long sira, int surum, int ilSayisi, string aciklama)
    {
        lock (kilit)
        {
            SonDosyaZamani = DateTime.Now;
            SonDosyaAdi = dosya;
            SonSira = sira;
            SonKabul = true;
            SonAciklama = aciklama;
            SonSurum = surum;
            SonIlSayisi = ilSayisi;

            ToplamKabul++;
            UstUsteRed = 0;
        }
    }

    public static void Red(string dosya, long sira, string sebep)
    {
        lock (kilit)
        {
            SonDosyaZamani = DateTime.Now;
            SonDosyaAdi = dosya;
            SonSira = sira;
            SonKabul = false;
            SonAciklama = sebep;

            ToplamRed++;
            UstUsteRed++;
        }
    }

    /// <summary> Son dosyanin uzerinden gecen saniye. Hic gelmediyse null. </summary>
    public static double? Yas =>
        SonDosyaZamani.HasValue ? (DateTime.Now - SonDosyaZamani.Value).TotalSeconds : null;

    /// <summary>
    /// Tek kelimelik saglik: YESIL / SARI / KIRMIZI.
    ///
    /// Esikler ajansin 30-60 saniyede bir gonderdigi varsayimina gore.
    /// Ust uste uc red, dosya akiyor olsa bile kirmizi: akan ama kabul
    /// edilmeyen veri, hic akmayan veriden daha tehlikeli - ekranda eski
    /// sayilar durur ve dosyalar geliyor diye kimse suphelenmez.
    /// </summary>
    public static string Saglik
    {
        get
        {
            if (UstUsteRed >= 3) return "KIRMIZI";

            double? yas = Yas;
            if (yas is null) return "SARI";

            if (yas > 300) return "KIRMIZI";
            if (yas > 120) return "SARI";

            return "YESIL";
        }
    }
}
