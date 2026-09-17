using System.Globalization;
using System.Text;
using System.Xml.Linq;
using HT_SECIM_BESLEME.Cekirdek;
using HT_SECIM_BESLEME.Model;

namespace HT_SECIM_BESLEME.Uretici;

/// <summary>
/// Anlik goruntuyu ajans bicimindeki XML dosyasina yazar.
///
/// DOSYA ONCE .tmp OLARAK YAZILIP SONRA ADI DEGISTIRILIYOR. Ajanstan
/// da bunu istiyoruz, kendi ureticimizin de boyle davranmasi sart:
/// aksi halde izleyici yarim yazilmis dosyayi gorur ve provanin
/// kendisi gercekte olmayan bir hata uretir. Rename atomiktir.
/// </summary>
public static class XmlYazici
{
    public static bool Yaz(Anlik anlik, string klasor)
    {
        string ad = $"{Ayarlar.UreticiKaynak.ToLowerInvariant()}_{anlik.Sira:D5}.xml";
        string gecici = Path.Combine(klasor, ad + ".tmp");
        string hedef  = Path.Combine(klasor, ad);

        try
        {
            Directory.CreateDirectory(klasor);

            XDocument belge = new(Kok(anlik));

            using (StreamWriter yazici = new(gecici, false, new UTF8Encoding(false)))
                belge.Save(yazici);

            File.Move(gecici, hedef, overwrite: true);

            anlik.DosyaAdi = ad;
            return true;
        }
        catch (Exception ex)
        {
            Kayit.Hata("URETIM YAZILAMADI", $"{ad} - {ex.Message}");

            try { if (File.Exists(gecici)) File.Delete(gecici); } catch { }

            return false;
        }
    }

    private static XElement Kok(Anlik anlik)
    {
        XElement kok = new("besleme",
            new XAttribute("kaynak", anlik.Kaynak),
            new XAttribute("uretim", anlik.Uretim.ToString("s", CultureInfo.InvariantCulture)),
            new XAttribute("sira", anlik.Sira));

        foreach (AnlikSecim secim in anlik.Secimler)
        {
            XElement secimDugumu = new("secim", new XAttribute("kod", secim.Kod));

            foreach (AnlikIl il in secim.Iller)
            {
                XElement ilDugumu = new("il",
                    new XAttribute("plaka", il.Plaka),
                    new XAttribute("toplamSandik", il.ToplamSandik),
                    new XAttribute("acilanSandik", il.AcilanSandik),
                    new XAttribute("secmenSayisi", il.SecmenSayisi),
                    new XAttribute("gecerliOy", il.GecerliOy),
                    new XAttribute("gecersizOy", il.GecersizOy));

                foreach (AnlikOy oy in il.Oylar)
                    ilDugumu.Add(new XElement("oy",
                        new XAttribute("kod", oy.Kod),
                        new XAttribute("sayi", oy.Sayi)));

                secimDugumu.Add(ilDugumu);
            }

            kok.Add(secimDugumu);
        }

        return kok;
    }
}
