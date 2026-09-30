using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Shared.DTOs;
using Microsoft.AspNetCore.Http;

namespace IsgCevreYonetim.Application.Services
{
    public interface IIsKazasiService
    {
        Task<IsKazasi?> GetByIdAsync(int id, int? branchId = null);
        Task<IEnumerable<IsKazasi>> GetAllAsync();
        Task<IsKazasi> CreateAsync(IsKazasi isKazasi, List<IFormFile>? dosyalar, List<int>? sahitPersonelIds, int branchId);
        Task UpdateAsync(IsKazasi isKazasi, List<IFormFile>? dosyalar, List<int>? sahitPersonelIds, int branchId);
        Task DeleteAsync(int id, int branchId);

        Task<PaginatedResult<IsKazasi>> GetIsKazalariAsync(IsKazasiFilterDto filter);
        Task<IEnumerable<PersonelAutocompleteDto>> SearchPersonelAsync(string searchTerm, int branchId);
        Task<PersonelAutocompleteDto?> GetPersonelByIdAsync(int id, int branchId);
        Task<IEnumerable<IsKazasiDosya>> GetDosyalarAsync(int isKazasiId);
        Task<bool> DeleteDosyaAsync(int dosyaId, int branchId);

        Task<IsKazasiRaporDto> GetRaporAsync(
            int? departmentId = null,
            int? branchId = null,
            int? onemliKazaGun = 3,
            DateTime? baslangicTarihi = null,
            DateTime? bitisTarihi = null);
    }
}
