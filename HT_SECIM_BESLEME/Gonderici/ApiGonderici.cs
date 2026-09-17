using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HT_SECIM_BESLEME.Cekirdek;
using HT_SECIM_BESLEME.Model;

namespace HT_SECIM_BESLEME.Gonderici;

/// <summary>
/// Anlik goruntuyu API'ye gonderir.
///
/// HttpClient TEK ORNEK: her istekte yenisini yaratmak soket tuketir ve
/// gece boyunca binlerce istek atan bir serviste er gec "address already
/// in use" hatasina donusur.
/// </summary>
public sealed class ApiGonderici : IDisposable
{
    private readonly HttpClient istemci;

    private static readonly JsonSerializerOptions Secenekler = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public ApiGonderici()
    {
        istemci = new HttpClient { Timeout = TimeSpan.FromSeconds(Ayarlar.ApiZamanasimi) };
        istemci.DefaultRequestHeaders.Add("X-API-KEY", Ayarlar.ApiAnahtar);
    }

    public sealed class Cevap
    {
        [JsonPropertyName("kabul")]    public bool Kabul { get; set; }
        [JsonPropertyName("surum")]    public int Surum { get; set; }
        [JsonPropertyName("ilSayisi")] public int IlSayisi { get; set; }
        [JsonPropertyName("oySatiri")] public int OySatiri { get; set; }
        [JsonPropertyName("aciklama")] public string Aciklama { get; set; } = "";
        [JsonPropertyName("uyarilar")] public List<string> Uyarilar { get; set; } = new();
    }

    /// <summary>
    /// Gonderir ve cevabi doner.
    ///
    /// API'ye ULASILAMAZSA null doner - bu bir RED DEGIL. Dosya hatali
    /// klasorune atilmamali: suclu dosya degil, baglanti. Cagiran onu
    /// yerinde birakip sonraki turda yeniden deniyor.
    /// </summary>
    public async Task<Cevap?> GonderAsync(Anlik anlik, CancellationToken iptal)
    {
        string adres = Ayarlar.ApiAdres + "/api/v1/besleme";

        object istek = new
        {
            kaynak = anlik.Kaynak,
            uretim = anlik.Uretim,
            sira   = anlik.Sira,
            dosya  = anlik.DosyaAdi,
            secimler = anlik.Secimler.Select(s => new
            {
                kod = s.Kod,
                iller = s.Iller.Select(i => new
                {
                    plaka        = i.Plaka,
                    toplamSandik = i.ToplamSandik,
                    acilanSandik = i.AcilanSandik,
                    secmenSayisi = i.SecmenSayisi,
                    gecersizOy   = i.GecersizOy,
                    gecerliOy    = i.GecerliOy,
                    oylar = i.Oylar.Select(o => new { kod = o.Kod, sayi = o.Sayi })
                })
            })
        };

        try
        {
            using HttpResponseMessage cevap =
                await istemci.PostAsJsonAsync(adres, istek, Secenekler, iptal);

            if (!cevap.IsSuccessStatusCode)
            {
                string govde = await cevap.Content.ReadAsStringAsync(iptal);

                Kayit.Hata("API REDDETTI",
                    $"{(int)cevap.StatusCode} {cevap.ReasonPhrase} - {Kisalt(govde)}");

                // Yetki hatasi dosyayla ilgili degil; dosya yerinde kalsin.
                if (cevap.StatusCode == System.Net.HttpStatusCode.Unauthorized) return null;

                return new Cevap { Kabul = false, Aciklama = $"HTTP {(int)cevap.StatusCode}" };
            }

            return await cevap.Content.ReadFromJsonAsync<Cevap>(Secenekler, iptal);
        }
        catch (Exception ex)
        {
            Kayit.Hata("API'YE ULASILAMADI", $"{adres} - {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gece baslamadan once sayaclari sifirlar.
    ///
    /// Prova sifirdan basliyor ama veritabaninda nihai sonuclar duruyor;
    /// sifirlanmazsa geri gitme korumasi HER dosyayi hakli olarak
    /// reddeder. Gercek secim gecesinde de ayni islem gerekiyor.
    /// </summary>
    public async Task<bool> SifirlaAsync(List<string> secimler, CancellationToken iptal)
    {
        string adres = Ayarlar.ApiAdres + "/api/v1/besleme/sifirla";

        try
        {
            using HttpResponseMessage cevap = await istemci.PostAsJsonAsync(
                adres, new { secimler, onay = "SIFIRLA" }, Secenekler, iptal);

            string govde = await cevap.Content.ReadAsStringAsync(iptal);

            if (!cevap.IsSuccessStatusCode)
            {
                Kayit.Hata("SIFIRLAMA BASARISIZ", $"{(int)cevap.StatusCode} - {Kisalt(govde)}");
                return false;
            }

            Kayit.Uyari("SAYACLAR SIFIRLANDI", Kisalt(govde));
            return true;
        }
        catch (Exception ex)
        {
            Kayit.Hata("SIFIRLAMAYA ULASILAMADI", $"{adres} - {ex.Message}");
            return false;
        }
    }

    private static string Kisalt(string metin)
        => metin.Length <= 200 ? metin : metin[..200] + "...";

    public void Dispose() => istemci.Dispose();
}
