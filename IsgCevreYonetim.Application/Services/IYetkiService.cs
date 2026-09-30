using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Shared.DTOs;

namespace IsgCevreYonetim.Application.Services
{
    public interface IYetkiService
    {
        // === YETKİ ===
        Task<IEnumerable<Yetki>> GetAllYetkilerAsync();
        Task<Yetki?> GetYetkiByIdAsync(int id);
        Task<Yetki> CreateYetkiAsync(Yetki yetki);
        Task UpdateYetkiAsync(Yetki yetki);
        Task DeleteYetkiAsync(int id);
        Task<bool> YetkiExistsAsync(string ad);
        Task<PaginatedResult<Yetki>> GetYetkilerAsync(ReferenceFilterDto filter);  // ⭐ EKLENDİ

        // === SAYFA ===
        Task<IEnumerable<Sayfa>> GetAllSayfalarAsync();
        Task<Sayfa?> GetSayfaByIdAsync(int id);
        Task<Sayfa> CreateSayfaAsync(Sayfa sayfa);
        Task UpdateSayfaAsync(Sayfa sayfa);
        Task DeleteSayfaAsync(int id);
        Task<PaginatedResult<Sayfa>> GetSayfalarAsync(ReferenceFilterDto filter);  // ⭐ EKLENDİ

        // === YETKİLENDİRME (Yetki + Şube + Sayfa) ===
        Task<IEnumerable<YetkiSayfa>> GetYetkiSayfalarByYetkiAndBranchIdAsync(int yetkiId, int branchId);
        Task<bool> YetkilendirAsync(int yetkiId, int branchId, int sayfaId, bool ekle, bool guncelle, bool sil, bool goster);
        Task<bool> YetkilendirTopluAsync(int yetkiId, int branchId, IEnumerable<YetkiSayfa> permissions, bool lokasyonSecebilir);
        Task<bool> YetkiSubeAyarlaAsync(int yetkiId, int branchId, bool lokasyonSecebilir);
        Task<bool> LokasyonSecebilirAsync(int yetkiId, int branchId);
        Task<bool> KullaniciLokasyonSecilebilirMiAsync(int personelId);
        Task<bool> KullaniciYetkiliMiAsync(int personelId, string sayfaUrl, string islem, int? branchId = null);
        Task<IEnumerable<Sayfa>> GetYetkiliSayfalarAsync(int personelId, int? branchId = null);
    }
}