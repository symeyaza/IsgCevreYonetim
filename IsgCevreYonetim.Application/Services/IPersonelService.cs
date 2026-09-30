using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Shared.DTOs;
using Microsoft.AspNetCore.Http;

namespace IsgCevreYonetim.Application.Services
{
    public interface IPersonelService
    {
        Task<Personel> GetByIdAsync(int id);
        Task<Personel> GetByEmailAsync(string email);
        Task<Personel> GetBySicilNoAsync(string sicilNo);
        Task<IEnumerable<Personel>> GetAllAsync();
        Task<IEnumerable<Personel>> GetByCompanyIdAsync(int companyId);
        Task<IEnumerable<Personel>> GetByBranchIdAsync(int branchId);
        Task<bool> ValidatePasswordAsync(Personel personel, string password);
        Task<bool> ChangePasswordAsync(int personelId, string oldPassword, string newPassword);
        Task<bool> ResetPasswordAsync(int personelId, string newPassword);
        Task<bool> IsEmailExistAsync(string email, int? excludeId = null);
        Task<bool> IsSicilNoExistAsync(string sicilNo, int? excludeId = null);
        Task<(bool EmailExists, bool SicilNoExists)> CheckUniqueFieldsAsync(string email, string sicilNo, int? excludeId = null);
        Task<bool> ValidateOrganizationSelectionAsync(int companyId, int branchId, int? departmentId, int? unitId);
        Task<Personel> CreateAsync(Personel personel, string password);
        Task UpdateAsync(Personel personel, string? newPassword = null);
        Task DeleteAsync(int id);
        Task<bool> LoginAsync(string emailOrSicil, string password, string ipAddress, string userAgent);
        Task LogoutAsync(int personelId);
        Task<bool> IsAccountLockedAsync(Personel personel);
        Task UnlockAccountAsync(int personelId);
        Task UpdateLastLoginAsync(int personelId);
        Task IncrementFailedLoginAttemptsAsync(int personelId);
        Task ResetFailedLoginAttemptsAsync(int personelId);

        // Profil Resmi
        Task<string> SaveProfileImageAsync(int personelId, IFormFile file);
        Task<bool> DeleteProfileImageAsync(int personelId);
        Task<Personel> GetPersonelWithDetailsAsync(int id);

        // ⭐ YENİ: Sayfalama metodu
        Task<PaginatedResult<Personel>> GetPersonellerAsync(PersonelFilterDto filter);
    }
}