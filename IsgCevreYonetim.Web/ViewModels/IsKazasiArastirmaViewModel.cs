using IsgCevreYonetim.Domain.Entities;

namespace IsgCevreYonetim.Web.ViewModels
{
    public class IsKazasiArastirmaViewModel
    {
        public IsKazasi IsKazasi { get; set; } = new();
        public List<IsKazasiArastirmaKategori> Kategoriler { get; set; } = new();
        public HashSet<int> SeciliMaddeIdleri { get; set; } = new();
        public IsKazasiArastirma? Arastirma { get; set; }
    }
}
