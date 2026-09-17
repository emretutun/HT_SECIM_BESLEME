using System.Text.Json;
using System.Text.Json.Serialization;
using HT_SECIM_BESLEME.Cekirdek;

namespace HT_SECIM_BESLEME.Uretici;

/// <summary>
/// Ureticinin hedefledigi son sonuc.
///
/// API'den BIR KEZ cekilip "hedef.json" dosyasina yaziliyor; sonraki
/// calisimalarda API kapali olsa bile prova yapilabiliyor. Prova
/// ortaminda API'nin ayakta olmasini sart kosmak, provanin kendisini
/// kirilgan yapardi.
///
/// API yuzde veriyor (katilim, acilan sandik), besleme ise ham sayi
/// tasimali. Aradaki ham sayilar iki varsayimla turetiliyor; ikisi de
/// ayar dosyasinda ve gercek ajans geldiginde onemleri kalmiyor.
/// </summary>
public class Hedef
{
    public List<HedefSecim> Secimler { get; set; } = new();

    public static Hedef? Yukle()
    {
        string dosya = Path.Combine(Ayarlar.Kok, "hedef.json");

        if (File.Exists(dosya))
        {
            try
            {
                Hedef? kayitli = JsonSerializer.Deserialize<Hedef>(File.ReadAllText(dosya));

                if (kayitli is { Secimler.Count: > 0 })
                {
                    Kayit.Bilgi("HEDEF YERELDEN", $"{kayitli.Secimler.Count} secim / hedef.json");
                    return kayitli;
                }
            }
            catch (Exception ex)
            {
                Kayit.Uyari("HEDEF OKUNAMADI", $"{ex.Message} - API'den yeniden cekilecek");
            }
        }

        // Once API, olmazsa yanindaki veri.json.
        //
        // Prova ortaminda API'nin ayakta olmasini sart kosmak istemiyoruz:
        // besleme servisi tek basina denenebilmeli. veri.json, API'nin
        // /api/v1/veri ciktisinin birebir aynisi - C ekraninin
        // veri_cache.json dosyasi da olur.
        Hedef? taze = ApidenCek() ?? DosyadanCek();
        if (taze == null) return null;

        try
        {
            File.WriteAllText(dosya, JsonSerializer.Serialize(taze,
                new JsonSerializerOptions { WriteIndented = false }));
        }
        catch (Exception ex)
        {
            // Yazamamak provayi durdurmaz, yalnizca her acilista API
            // gerekir.
            Kayit.Uyari("HEDEF YAZILAMADI", ex.Message);
        }

        return taze;
    }

    private static Hedef? ApidenCek()
    {
        if (Ayarlar.ApiAdres.Length == 0)
        {
            Kayit.Hata("HEDEF ALINAMADI", "besleme dosyasinda API_ADRES bos");
            return null;
        }

        string adres = Ayarlar.ApiAdres + "/api/v1/veri";

        try
        {
            using HttpClient istemci = new() { Timeout = TimeSpan.FromSeconds(Ayarlar.ApiZamanasimi) };
            istemci.DefaultRequestHeaders.Add("X-API-KEY", Ayarlar.ApiOkumaAnahtar);

            string json = istemci.GetStringAsync(adres).GetAwaiter().GetResult();

            ApiVeri? veri = JsonSerializer.Deserialize<ApiVeri>(json);

            if (veri == null || veri.Secimler.Count == 0)
            {
                Kayit.Hata("HEDEF BOS", adres + " - secim yok");
                return null;
            }

            Hedef hedef = Cevir(veri);

            Kayit.Bilgi("HEDEF API'DEN",
                $"{hedef.Secimler.Count} secim / " +
                string.Join(", ", hedef.Secimler.Select(s => $"{s.Kod} {s.Iller.Count} il")));

            return hedef;
        }
        catch (Exception ex)
        {
            Kayit.Hata("HEDEF ALINAMADI", $"{adres} - {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// API kapaliyken exe'nin yanindaki veri.json'dan hedef alir.
    /// Dosya, API'nin /api/v1/veri ciktisiyla ayni sekilde.
    /// </summary>
    private static Hedef? DosyadanCek()
    {
        string dosya = Path.Combine(Ayarlar.Kok, "veri.json");

        if (!File.Exists(dosya))
        {
            Kayit.Hata("HEDEF YOK", $"API kapali ve {dosya} de yok");
            return null;
        }

        try
        {
            ApiVeri? veri = JsonSerializer.Deserialize<ApiVeri>(File.ReadAllText(dosya));

            if (veri == null || veri.Secimler.Count == 0)
            {
                Kayit.Hata("HEDEF BOS", dosya + " - secim yok");
                return null;
            }

            Hedef hedef = Cevir(veri);

            Kayit.Uyari("HEDEF DOSYADAN",
                $"API kapali oldugu icin veri.json kullanildi / " +
                string.Join(", ", hedef.Secimler.Select(s => $"{s.Kod} {s.Iller.Count} il")));

            return hedef;
        }
        catch (Exception ex)
        {
            Kayit.Hata("HEDEF OKUNAMADI", $"{dosya} - {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// API cevabini hedef yapisina cevirir ve ham sayilari turetir.
    ///
    ///   gecersizOy   = gecerliOy * GECERSIZ_YUZDE / 100
    ///   secmenSayisi = (gecerli + gecersiz) / katilim
    ///   toplamSandik = secmenSayisi / SANDIK_BASINA_SECMEN
    ///
    /// Hedef acilan sandik %100 DEGIL: gercek secimde de sandiklarin
    /// tamami acilmiyor (2023'te ~%94,6). Hedef, API'deki son
    /// acilan sandik oranina gore belirleniyor.
    /// </summary>
    private static Hedef Cevir(ApiVeri veri)
    {
        Dictionary<int, string> bolgeler = veri.Iller
            .GroupBy(i => i.Plaka)
            .ToDictionary(g => g.Key, g => g.First().Bolge ?? "");

        Hedef hedef = new();

        foreach (ApiSecim secim in veri.Secimler)
        {
            // Yalnizca istenen secimler uretiliyor; veride alti secim var,
            // prova icin hepsini uretmenin anlami yok.
            if (Ayarlar.UreticiSecimler.Count > 0 &&
                !Ayarlar.UreticiSecimler.Contains(secim.Kod, StringComparer.OrdinalIgnoreCase))
                continue;

            HedefSecim hs = new() { Kod = secim.Kod };

            foreach (ApiSonuc sonuc in secim.Sonuclar)
            {
                // Turkiye geneli (0) uretilmiyor - API illerden topluyor.
                if (sonuc.Plaka == 0) continue;
                if (sonuc.GecerliOy <= 0) continue;

                long gecersiz = (long)Math.Round(sonuc.GecerliOy * Ayarlar.GecersizYuzde / 100.0);
                long kullanilan = sonuc.GecerliOy + gecersiz;

                double katilimOran = sonuc.Katilim > 0 ? sonuc.Katilim / 10000.0 : 0.85;
                int secmen = (int)Math.Round(kullanilan / katilimOran);

                int toplamSandik = Math.Max(1,
                    (int)Math.Round((double)secmen / Ayarlar.SandikBasinaSecmen));

                double acilanOran = sonuc.AcilanSandik > 0 ? sonuc.AcilanSandik / 10000.0 : 1.0;

                HedefIl il = new()
                {
                    Plaka        = sonuc.Plaka,
                    Bolge        = bolgeler.TryGetValue(sonuc.Plaka, out string? b) ? b : "",
                    ToplamSandik = toplamSandik,
                    HedefAcilan  = Math.Max(1, (int)Math.Round(toplamSandik * acilanOran)),
                    SecmenSayisi = secmen,
                    GecerliOy    = sonuc.GecerliOy,
                    GecersizOy   = gecersiz
                };

                foreach (ApiOy oy in sonuc.Oylar)
                    if (oy.Oy > 0)
                        il.Oylar.Add(new HedefOy { Kod = oy.Kod, Oy = oy.Oy });

                if (il.Oylar.Count > 0) hs.Iller.Add(il);
            }

            if (hs.Iller.Count > 0) hedef.Secimler.Add(hs);
        }

        return hedef;
    }

    // ---------------------------------------------------- API cevap sekli

    private class ApiVeri
    {
        [JsonPropertyName("iller")]    public List<ApiIl> Iller { get; set; } = new();
        [JsonPropertyName("secimler")] public List<ApiSecim> Secimler { get; set; } = new();
    }

    private class ApiIl
    {
        [JsonPropertyName("plaka")] public int Plaka { get; set; }
        [JsonPropertyName("bolge")] public string? Bolge { get; set; }
    }

    private class ApiSecim
    {
        [JsonPropertyName("kod")]      public string Kod { get; set; } = "";
        [JsonPropertyName("sonuclar")] public List<ApiSonuc> Sonuclar { get; set; } = new();
    }

    private class ApiSonuc
    {
        [JsonPropertyName("plaka")]        public int Plaka { get; set; }
        [JsonPropertyName("acilanSandik")] public int AcilanSandik { get; set; }
        [JsonPropertyName("katilim")]      public int Katilim { get; set; }
        [JsonPropertyName("gecerliOy")]    public long GecerliOy { get; set; }
        [JsonPropertyName("oylar")]        public List<ApiOy> Oylar { get; set; } = new();
    }

    private class ApiOy
    {
        [JsonPropertyName("kod")] public string Kod { get; set; } = "";
        [JsonPropertyName("oy")]  public long Oy { get; set; }
    }
}

public class HedefSecim
{
    public string Kod { get; set; } = "";
    public List<HedefIl> Iller { get; set; } = new();
}

public class HedefIl
{
    public int Plaka { get; set; }
    public string Bolge { get; set; } = "";

    public int ToplamSandik { get; set; }

    /// <summary> Gece bitiminde acilmis olacak sandik sayisi (hepsi degil). </summary>
    public int HedefAcilan { get; set; }

    public int SecmenSayisi { get; set; }
    public long GecerliOy { get; set; }
    public long GecersizOy { get; set; }

    public List<HedefOy> Oylar { get; set; } = new();
}

public class HedefOy
{
    public string Kod { get; set; } = "";
    public long Oy { get; set; }
}
