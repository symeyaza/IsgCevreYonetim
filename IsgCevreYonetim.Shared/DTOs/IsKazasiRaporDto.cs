// Shared/DTOs/IsKazasiRaporDto.cs
namespace IsgCevreYonetim.Shared.DTOs
{
    public class IsKazasiRaporDto
    {
        public int OnemliKazaGun { get; set; } = 3;
        public int ToplamKaza { get; set; }
        public int ToplamKayipGun { get; set; }
        public int OnemliKazaSayisi { get; set; }
        public int OnemliKazaKayipGun { get; set; }

        // ⭐ Departman-Ay Kaza Matrisi
        public List<DepartmanAyMatrisDto> KazaMatrisi { get; set; } = new();

        // ⭐ Departman-Ay Kayıp Gün Matrisi
        public List<DepartmanAyMatrisDto> KayipGunMatrisi { get; set; } = new();

        // ⭐ Departman-Ay Önemli Kaza Matrisi
        public List<DepartmanAyMatrisDto> OnemliKazaMatrisi { get; set; } = new();

        // ⭐ YENİ: Devreden Kayıp Gün Matrisi
        public List<DepartmanAyMatrisDto> DevredenKayipGunMatrisi { get; set; } = new();

        // Kaza araştırma maddelerinin departman bazlı dağılımı
        public List<ArastirmaKategoriMatrisDto> ArastirmaKategoriMatrisleri { get; set; } = new();

        // Aylık istatistikler (grafikler için)
        public List<AylikRaporDto> AylikRaporlar { get; set; } = new();

        // Departman bazlı istatistikler
        public List<DepartmanRaporDto> DepartmanRaporlari { get; set; } = new();
    }

    public class DepartmanRaporDto
    {
        public int DepartmentId { get; set; }
        public string DepartmentAdi { get; set; } = string.Empty;
        public int ToplamKaza { get; set; }
        public int ToplamKayipGun { get; set; }
        public int OnemliKazaSayisi { get; set; }
        public int OnemliKazaKayipGun { get; set; }
        public List<AyDetayDto> AylikDetaylar { get; set; } = new();
    }

    public class AylikRaporDto
    {
        public int Yil { get; set; }
        public int Ay { get; set; }
        public string AyAdi => new DateTime(Yil, Ay, 1).ToString("MMMM", new System.Globalization.CultureInfo("tr-TR"));
        public string AyKisa => new DateTime(Yil, Ay, 1).ToString("MMM", new System.Globalization.CultureInfo("tr-TR"));
        public int ToplamKaza { get; set; }
        public int ToplamKayipGun { get; set; }
        public int OnemliKazaSayisi { get; set; }
    }

    public class DepartmanAyMatrisDto
    {
        public int DepartmentId { get; set; }
        public string DepartmentAdi { get; set; } = string.Empty;
        public Dictionary<int, AyMatrisData> Aylar { get; set; } = new();
        public int Toplam { get; set; }
    }

    public class AyMatrisData
    {
        public int Deger { get; set; }  // Kaza sayısı, Kayıp gün veya Önemli kaza sayısı
    }

    public class AyDetayDto
    {
        public int Ay { get; set; }
        public string AyAdi { get; set; } = string.Empty;
        public int KazaSayisi { get; set; }
        public int KayipGun { get; set; }
        public int OnemliKazaSayisi { get; set; }
    }

    public class ArastirmaKategoriMatrisDto
    {
        public int KategoriId { get; set; }
        public string KategoriAdi { get; set; } = string.Empty;
        public int Sira { get; set; }
        public List<ArastirmaMaddeBaslikDto> Maddeler { get; set; } = new();
        public List<ArastirmaDepartmanSatirDto> Departmanlar { get; set; } = new();
        public int ToplamKaza { get; set; }
    }

    public class ArastirmaMaddeBaslikDto
    {
        public int MaddeId { get; set; }
        public string Metin { get; set; } = string.Empty;
        public int Sira { get; set; }
        public int ToplamKaza { get; set; }
    }

    public class ArastirmaDepartmanSatirDto
    {
        public int DepartmentId { get; set; }
        public string DepartmentAdi { get; set; } = string.Empty;
        public Dictionary<int, int> MaddeKazaSayilari { get; set; } = new();
        public int ToplamKaza { get; set; }
    }

    // ⭐ YENİ: Devreden Kayıp Gün DTO'su
    public class DevredenKayipGunDto
    {
        public int DepartmentId { get; set; }
        public string DepartmentAdi { get; set; } = string.Empty;
        public int Ay { get; set; }
        public int Yil { get; set; }
        public int DevredenGun { get; set; }  // Bir önceki aydan devreden kayıp gün
        public int CariAyKayipGun { get; set; }  // Cari aydaki kayıp gün
        public int ToplamKayipGun { get; set; }  // Devreden + Cari
    }


    public class IsKazasiExportDto
    {
        public string KazaTarihi { get; set; } = string.Empty;
        public string PersonelAdSoyad { get; set; } = string.Empty;
        public string SicilNo { get; set; } = string.Empty;
        public string Departman { get; set; } = string.Empty;
        public string Sube { get; set; } = string.Empty;
        public string Birim { get; set; } = string.Empty;
        public string Gorev { get; set; } = string.Empty;
        public string Grup { get; set; } = string.Empty;
        public string Aciklama { get; set; } = string.Empty;
        public string Mudahale { get; set; } = string.Empty;
        public string RaporBaslangic { get; set; } = string.Empty;
        public string RaporBitis { get; set; } = string.Empty;
        public string KayipGun { get; set; } = string.Empty;
        public string DosyaSayisi { get; set; } = string.Empty;
    }

}