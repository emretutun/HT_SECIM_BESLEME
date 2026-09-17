using HT_SECIM_BESLEME.Cekirdek;
using HT_SECIM_BESLEME.Servis;

// ------------------------------------------------------------------
//  HT SECIM - BESLEME SERVISI
//
//  Ajansin biraktigi anlik goruntu dosyalarini izler, dogrular ve
//  API'ye gonderir. Web sunucusu olmasinin tek sebebi /durum saglik
//  ucu; asil is arka plandaki KlasorIzleyici'de doner.
//
//  Servis olmeden ayakta kalmaya oncelik veriyor: bozuk bir dosya ya
//  da bir an erisilemeyen ag surucusu yuzunden durmamali. Yayin
//  gecesi kimse basinda olmayabilir.
// ------------------------------------------------------------------

Console.OutputEncoding = System.Text.Encoding.UTF8;

Kayit.Bilgi("SERVIS BASLIYOR");
Ayarlar.Yukle();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Adres burada sabit: launchSettings.json yalnizca Visual Studio'dan
// calistirirken okunuyor. Servis olarak ya da exe'den calistiginda
// varsayilan 5000'e dusuyordu ve saglik ucu baska adreste kaliyordu.
// ASPNETCORE_URLS ile yine ezilebilir.
builder.WebHost.UseUrls("http://localhost:5050");

// Klasor izleyici arka planda surekli calisir.
builder.Services.AddHostedService<KlasorIzleyici>();

// Prova uretici. Ayar kapaliysa kendi kendine cikiyor; burada kosullu
// kayit yapmiyoruz ki ayar dosyasi degistiginde yalnizca yeniden
// baslatmak yetsin.
builder.Services.AddHostedService<UreticiServisi>();

WebApplication app = builder.Build();

// Saglik ucu.
//
// Sessiz basarisizligi gorunur kilmak icin: ajans 40 dakikadir dosya
// gondermiyorsa ekran eski veriyi guvenle gostermeye devam eder ve
// kimse fark etmez. Yayin tarafi bu uca bakip alarm verebilir.
app.MapGet("/durum", () => Results.Json(new
{
    servis    = "HT_SECIM_BESLEME",
    saglik    = Durum.Saglik,
    zaman     = DateTime.Now,
    baslangic = Durum.Baslangic,

    sonDosya = new
    {
        ad       = Durum.SonDosyaAdi,
        zaman    = Durum.SonDosyaZamani,
        yasSaniye = Durum.Yas,
        sira     = Durum.SonSira,
        kabul    = Durum.SonKabul,
        aciklama = Durum.SonAciklama,
        ilSayisi = Durum.SonIlSayisi
    },

    surum = Durum.SonSurum,

    sayaclar = new
    {
        kabul      = Durum.ToplamKabul,
        red        = Durum.ToplamRed,
        ustUsteRed = Durum.UstUsteRed
    },

    ayar = new
    {
        gelen   = Ayarlar.Gelen,
        api     = Ayarlar.ApiAdres,
        uretici = Ayarlar.Uretici
    }
}));

app.Lifetime.ApplicationStopping.Register(() => Kayit.Bilgi("SERVIS DURUYOR"));

app.Run();
