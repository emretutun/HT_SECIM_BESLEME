using System.Globalization;

namespace HT_SECIM_BESLEME.Cekirdek;

/// <summary>
/// Exe'nin yanindaki "besleme" dosyasindan okunan ayarlar.
///
/// appsettings.json yerine duz metin kullaniliyor: bu servisi yayin
/// gecesi ayarlayacak kisi gelistirici olmayabilir ve JSON'da unutulan
/// bir virgul butun dosyayi okunamaz yapar. Duz metinde bozuk bir satir
/// yalnizca kendini kaybettirir, gerisi calismaya devam eder.
///
/// Ayni bicim reji ve C ekrani uygulamalarinda da kullaniliyor.
/// </summary>
public static class Ayarlar
{
    public static string Kok { get; private set; } = AppContext.BaseDirectory;

    // ---------------------------------------------------------- klasorler

    public static string Gelen  { get; private set; } = "";
    public static string Arsiv  { get; private set; } = "";
    public static string Hatali { get; private set; } = "";

    public static string Desen { get; private set; } = "*.xml";

    // ------------------------------------------------------------ zamanlar

    public static int TaramaAralik  { get; private set; } = 5;
    public static int Sakinlik      { get; private set; } = 3;
    public static int BeklemeSiniri { get; private set; } = 120;

    // ----------------------------------------------------------------- api

    public static string ApiAdres   { get; private set; } = "";
    public static string ApiAnahtar { get; private set; } = "";
    public static int ApiZamanasimi { get; private set; } = 30;

    // ----------------------------------------------------------- dogrulama

    public static int SicramaUyari { get; private set; } = 20;
    public static int AsgariIl     { get; private set; } = 81;
    public static bool GeriGitmeKorumasi { get; private set; } = true;

    // ------------------------------------------------------------- uretici

    public static bool Uretici { get; private set; }
    public static int UreticiAralik { get; private set; } = 30;
    public static int UreticiSure   { get; private set; } = 10;
    public static string UreticiKaynak { get; private set; } = "PROVA";

    /// <summary> Prova baslarken API'deki sayaclar sifirlansin mi. </summary>
    public static bool UreticiSifirla { get; private set; }
    public static List<string> UreticiSecimler { get; } = new();

    public static string ApiOkumaAnahtar { get; private set; } = "";

    /// <summary> Gecerli oyun yuzde kaci kadar gecersiz oy varsayilacak. </summary>
    public static double GecersizYuzde { get; private set; } = 2.5;

    /// <summary> Bir sandiga kac secmen dusuyor. </summary>
    public static int SandikBasinaSecmen { get; private set; } = 300;

    /// <summary>
    /// Bolge -> o bolgenin surenin yuzde kacinda acilmaya baslayacagi.
    /// Dogu illeri once, Marmara en son.
    /// </summary>
    public static Dictionary<string, int> Dalgalar { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public static void Yukle()
    {
        string dosya = Path.Combine(Kok, "besleme");

        foreach (string[] p in AyarOkuyucu.Satirlar(dosya))
        {
            if (p.Length < 2) continue;

            string anahtar = p[0].Trim().ToUpperInvariant();
            string deger   = p[1].Trim();

            switch (anahtar)
            {
                case "GELEN":  Gelen  = Yol(deger); break;
                case "ARSIV":  Arsiv  = Yol(deger); break;
                case "HATALI": Hatali = Yol(deger); break;
                case "DESEN":  if (deger.Length > 0) Desen = deger; break;

                case "TARAMA_ARALIK":  TaramaAralik  = Sayi(deger, TaramaAralik, 1);
                    break;
                case "SAKINLIK":       Sakinlik      = Sayi(deger, Sakinlik, 1); break;
                case "BEKLEME_SINIRI": BeklemeSiniri = Sayi(deger, BeklemeSiniri, 5); break;

                case "API_ADRES":      ApiAdres      = deger.TrimEnd('/'); break;
                case "API_ANAHTAR":    ApiAnahtar    = deger; break;
                case "API_ZAMANASIMI": ApiZamanasimi = Sayi(deger, ApiZamanasimi, 5); break;

                case "SICRAMA_UYARI": SicramaUyari = Sayi(deger, SicramaUyari, 1); break;
                case "ASGARI_IL":     AsgariIl     = Sayi(deger, AsgariIl, 0); break;
                case "GERI_GITME_KORUMASI": GeriGitmeKorumasi = deger == "1"; break;

                case "URETICI":         Uretici       = deger == "1"; break;
                case "URETICI_ARALIK":  UreticiAralik = Sayi(deger, UreticiAralik, 1); break;
                case "URETICI_SURE":    UreticiSure   = Sayi(deger, UreticiSure, 1); break;
                case "URETICI_KAYNAK":  UreticiKaynak = deger; break;
                case "URETICI_SIFIRLA": UreticiSifirla = deger == "1"; break;

                case "URETICI_SECIMLER":
                    UreticiSecimler.Clear();
                    foreach (string kod in deger.Split(','))
                        if (kod.Trim().Length > 0) UreticiSecimler.Add(kod.Trim());
                    break;

                case "API_OKUMA_ANAHTAR":    ApiOkumaAnahtar    = deger; break;
                case "SANDIK_BASINA_SECMEN": SandikBasinaSecmen = Sayi(deger, SandikBasinaSecmen, 1); break;

                case "GECERSIZ_YUZDE":
                    // Ondalik her zaman nokta ile okunuyor; Turkce kulturde
                    // "2.5" virgulle yorumlanip 25 olabiliyordu.
                    if (double.TryParse(deger.Replace(',', '.'), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out double yuzde) && yuzde >= 0)
                        GecersizYuzde = yuzde;
                    break;

                // DALGA = <bolge> = <yuzde>
                case "DALGA":
                    if (p.Length >= 3 &&
                        int.TryParse(p[2].Trim(), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out int gecikme))
                        Dalgalar[p[1].Trim()] = gecikme;
                    break;
            }
        }

        KlasorleriHazirla();

        Kayit.Bilgi("AYARLAR",
            $"gelen={Gelen} / tarama={TaramaAralik}sn / api={ApiAdres}" +
            (Uretici ? $" / URETICI ACIK ({UreticiSure} dk)" : ""));
    }

    /// <summary>
    /// Goreli yol verilmisse exe'nin yanina gore cozuluyor.
    /// Mutlak yol (C:\... ya da \\sunucu\...) oldugu gibi kaliyor -
    /// gercek ajans klasoru genelde ag surucusunde olacak.
    /// </summary>
    private static string Yol(string deger)
    {
        if (deger.Length == 0) return "";
        return Path.IsPathRooted(deger) ? deger : Path.Combine(Kok, deger);
    }

    /// <summary>
    /// Klasorler yoksa olusturuluyor.
    ///
    /// Ag surucusu erisilemiyorsa burada patlamak yerine hata yazip
    /// devam ediliyor: servis ayakta kalsin, klasor geri gelince
    /// calismaya devam etsin. Yayin gecesi bir surucu birkac saniye
    /// kaybolabiliyor.
    /// </summary>
    private static void KlasorleriHazirla()
    {
        foreach (string klasor in new[] { Gelen, Arsiv, Hatali })
        {
            if (klasor.Length == 0) continue;

            try { Directory.CreateDirectory(klasor); }
            catch (Exception ex) { Kayit.Hata("KLASOR ACILAMADI", $"{klasor} - {ex.Message}"); }
        }
    }

    /// <summary>
    /// Okunamayan deger eski degerini koruyor; bozuk bir satir yuzunden
    /// servisin sacma bir aralikla calismasi istenmiyor.
    /// </summary>
    private static int Sayi(string deger, int eski, int enAz)
    {
        if (!int.TryParse(deger, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sonuc))
        {
            Kayit.Hata("AYAR OKUNAMADI", $"'{deger}' sayi degil, {eski} kaldi");
            return eski;
        }

        return sonuc < enAz ? enAz : sonuc;
    }
}
