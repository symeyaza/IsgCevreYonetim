namespace IsgCevreYonetim.Web.ViewModels;

public sealed record RaporSecenegi(int Id, string Ad);
public sealed record RaporDagilimi(string Ad, int Sayi);
public sealed record PersonelRaporSatiri(int Id, string SicilNo, string AdSoyad, string Departman, string Birim, string Yetki, DateTime? IseGirisTarihi, bool Aktif);
public sealed class PersonelRaporViewModel
{
    public string Sube { get; set; } = "";
    public int? Yil { get; set; }
    public int? DepartmanId { get; set; }
    public bool? Aktif { get; set; }
    public int Toplam { get; set; }
    public int AktifSayisi { get; set; }
    public int PasifSayisi => Toplam - AktifSayisi;
    public int BuYilBaslayan { get; set; }
    public List<int> Yillar { get; set; } = new();
    public List<RaporSecenegi> Departmanlar { get; set; } = new();
    public List<RaporDagilimi> DepartmanDagilimi { get; set; } = new();
    public int[] AylikGiris { get; set; } = new int[12];
    public List<PersonelRaporSatiri> Satirlar { get; set; } = new();
    public int Sayfa { get; set; }
    public int SayfaSayisi { get; set; }
}
public sealed record TehlikeliIsRaporSatiri(int Id, DateTime Tarih, string Is, string Yer, string Birim, string Durum, int Calisan, int KayitliGun);
public sealed class TehlikeliIsRaporViewModel
{
    public string Sube { get; set; } = "";
    public int Yil { get; set; }
    public int? BirimId { get; set; }
    public int? DurumId { get; set; }
    public int Toplam { get; set; }
    public int Tamamlanan { get; set; }
    public int DevamEden => Toplam - Tamamlanan;
    public int CalisanToplami { get; set; }
    public int GunKontrolSayisi { get; set; }
    public int[] Aylik { get; set; } = new int[12];
    public List<int> Yillar { get; set; } = new();
    public List<RaporSecenegi> Birimler { get; set; } = new();
    public List<RaporSecenegi> Durumlar { get; set; } = new();
    public List<RaporDagilimi> DurumDagilimi { get; set; } = new();
    public List<RaporDagilimi> TehlikeDagilimi { get; set; } = new();
    public List<TehlikeliIsRaporSatiri> Satirlar { get; set; } = new();
    public int Sayfa { get; set; }
    public int SayfaSayisi { get; set; }
}
