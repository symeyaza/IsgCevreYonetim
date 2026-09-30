using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Shared.DTOs;

namespace IsgCevreYonetim.Application.Services
{
    public interface IReferenceService
    {
        // İl
        Task<IEnumerable<Il>> GetAllIllerAsync();
        Task<Il?> GetIlByIdAsync(int id);
        Task<Il> CreateIlAsync(Il il);
        Task UpdateIlAsync(Il il);
        Task DeleteIlAsync(int id);
        Task<bool> IlExistsAsync(string kod, string ad);

        // İlçe
        Task<IEnumerable<Ilce>> GetAllIlcelerAsync();
        Task<IEnumerable<Ilce>> GetIlcelerByIlIdAsync(int ilId);
        Task<Ilce?> GetIlceByIdAsync(int id);
        Task<Ilce> CreateIlceAsync(Ilce ilce);
        Task UpdateIlceAsync(Ilce ilce);
        Task DeleteIlceAsync(int id);
        Task<bool> IlceExistsAsync(int ilId, string ad);

        // Cinsiyet
        Task<IEnumerable<Cinsiyet>> GetAllCinsiyetlerAsync();
        Task<Cinsiyet?> GetCinsiyetByIdAsync(int id);
        Task<Cinsiyet> CreateCinsiyetAsync(Cinsiyet cinsiyet);
        Task UpdateCinsiyetAsync(Cinsiyet cinsiyet);
        Task DeleteCinsiyetAsync(int id);
        Task<bool> CinsiyetExistsAsync(string ad);

        // Görev
        Task<IEnumerable<Gorev>> GetAllGorevlerAsync();
        Task<Gorev?> GetGorevByIdAsync(int id);
        Task<Gorev> CreateGorevAsync(Gorev gorev);
        Task UpdateGorevAsync(Gorev gorev);
        Task DeleteGorevAsync(int id);
        Task<bool> GorevExistsAsync(string ad);

        // Grup
        Task<IEnumerable<Grup>> GetAllGruplarAsync();
        Task<Grup?> GetGrupByIdAsync(int id);
        Task<Grup> CreateGrupAsync(Grup grup);
        Task UpdateGrupAsync(Grup grup);
        Task DeleteGrupAsync(int id);
        Task<bool> GrupExistsAsync(string ad);

        // Sayfalama Metotları
        Task<PaginatedResult<Il>> GetIllerAsync(ReferenceFilterDto filter);
        Task<PaginatedResult<Ilce>> GetIlcelerAsync(ReferenceFilterDto filter);
        Task<PaginatedResult<Gorev>> GetGorevlerAsync(ReferenceFilterDto filter);
        Task<PaginatedResult<Grup>> GetGruplarAsync(ReferenceFilterDto filter);
        Task<PaginatedResult<Cinsiyet>> GetCinsiyetlerAsync(ReferenceFilterDto filter);
    }
}