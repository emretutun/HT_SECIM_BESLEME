namespace HT_SECIM_BESLEME.Model;

/// <summary>
/// Ajanstan gelen bir ANLIK GORUNTU: o an bilinen her seyin tamami.
///
/// Fark degil tam goruntu tasiniyor. Fark gonderimi kulağa daha verimli
/// geliyor ama tek bir kayip paket butun geceyi bozar ve kimse fark
/// etmez. Tam goruntude her dosya kendi basina dogru.
///
/// Bu sinif AJANSIN BICIMINDEN BAGIMSIZ. XML, CSV, JSON - hangisi
/// gelirse gelsin uyarlayici onu buraya cevirir; dogrulama ve gonderme
/// kodu ajansin bicimini hic gormez. Ajans bicimini degistirdiginde
/// (degistiriyorlar) yalnizca uyarlayici degisir.
/// </summary>
public class Anlik
{
    /// <summary> Ajans adi: ANKA, AA, PROVA... </summary>
    public string Kaynak { get; set; } = "";

    /// <summary> Dosyanin ajansta uretildigi an. </summary>
    public DateTime Uretim { get; set; }

    /// <summary>
    /// Ardisik dosya numarasi.
    /// Atlanan ve sira disi gelen dosyalari yakalamak icin; numarasi
    /// bir oncekinden kucuk gelen dosya ESKI dosyadir, reddedilir.
    /// </summary>
    public long Sira { get; set; }

    /// <summary> Dosyanin diskteki adi; gunluk ve arsiv icin. </summary>
    public string DosyaAdi { get; set; } = "";

    public List<AnlikSecim> Secimler { get; } = new();

    /// <summary> Butun secimlerdeki toplam il satiri sayisi. </summary>
    public int IlSayisi => Secimler.Sum(s => s.Iller.Count);

    public override string ToString()
        => $"{Kaynak} #{Sira} {Uretim:HH:mm:ss} / {Secimler.Count} secim / {IlSayisi} il";
}

/// <summary>
/// Tek bir secimin (CB_2027, MV_2027) il il sonuclari.
/// Bir dosyada birden fazla secim olabilir; C ekrani zaten CB ve MV'yi
/// ayni anda gosteriyor.
/// </summary>
public class AnlikSecim
{
    public string Kod { get; set; } = "";

    public List<AnlikIl> Iller { get; } = new();
}

/// <summary>
/// Bir ilin ham sayilari.
///
/// YALNIZCA HAM SAYI tasiniyor - yuzde yok, Turkiye geneli yok,
/// milletvekili sayisi yok. Bunlarin hepsini API hesapliyor.
/// Tek yerde hesaplanmasi, ekranin altiyla ustunun birbiriyle
/// celismesini imkansiz kiliyor.
/// </summary>
public class AnlikIl
{
    public int Plaka { get; set; }

    public int ToplamSandik { get; set; }
    public int AcilanSandik { get; set; }

    public int SecmenSayisi { get; set; }
    public long GecerliOy { get; set; }
    public long GecersizOy { get; set; }

    public List<AnlikOy> Oylar { get; } = new();

    /// <summary> Partilerin / adaylarin aldigi oylarin toplami. </summary>
    public long OyToplami => Oylar.Sum(o => o.Sayi);
}

/// <summary> Bir parti (MV) ya da adayin (CB) o ildeki oyu. </summary>
public class AnlikOy
{
    public string Kod { get; set; } = "";
    public long Sayi { get; set; }
}
