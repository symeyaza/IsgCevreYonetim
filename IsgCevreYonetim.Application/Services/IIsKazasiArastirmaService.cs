using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Shared.DTOs;

namespace IsgCevreYonetim.Application.Services
{
    public interface IIsKazasiArastirmaService
    {
        Task<List<IsKazasiArastirmaKategori>> GetKategorilerAsync(bool includeInactive = true);
        Task<IsKazasiArastirmaKategori?> GetKategoriByIdAsync(int id);
        Task CreateKategoriAsync(IsKazasiArastirmaKategori kategori);
        Task UpdateKategoriAsync(IsKazasiArastirmaKategori kategori);
        Task DeleteKategoriAsync(int id);

        Task<List<IsKazasiArastirmaMadde>> GetMaddelerAsync(int? kategoriId = null, bool includeInactive = true);
        Task<PaginatedResult<IsKazasiArastirmaMadde>> GetMaddelerPagedAsync(
            int? kategoriId = null,
            int pageNumber = 1,
            int pageSize = 10,
            bool includeInactive = true);
        Task<IsKazasiArastirmaMadde?> GetMaddeByIdAsync(int id);
        Task CreateMaddeAsync(IsKazasiArastirmaMadde madde);
        Task UpdateMaddeAsync(IsKazasiArastirmaMadde madde);
        Task DeleteMaddeAsync(int id);

        Task<IsKazasi?> GetIsKazasiAsync(int isKazasiId, int branchId);
        Task<List<IsKazasiArastirmaKategori>> GetAktifKategorilerVeMaddelerAsync();
        Task<HashSet<int>> GetSeciliMaddeIdleriAsync(int isKazasiId, int branchId);
        Task<List<IsKazasiArastirmaMadde>> GetSeciliMaddelerAsync(int isKazasiId, int branchId);
        Task<IsKazasiArastirma?> GetArastirmaBilgisiAsync(int isKazasiId, int branchId);
        Task SaveArastirmaAsync(int isKazasiId, int branchId, int arastiranPersonelId, IEnumerable<int>? seciliMaddeIdleri);
    }
}
