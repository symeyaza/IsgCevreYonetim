namespace IsgCevreYonetim.Web.Services
{
    public sealed class UserScopeInfo
    {
        public int PersonelId { get; init; }

        // Kullanıcının kalıcı/home organizasyon bilgileri.
        public int CompanyId { get; init; }
        public int HomeBranchId { get; init; }

        // Dashboard'ta seçilmiş operasyonel kapsam.
        // Normal kullanıcıda ActiveCompanyId == CompanyId olur.
        public int ActiveCompanyId { get; init; }
        public int ActiveBranchId { get; init; }

        public int YetkiId { get; init; }
        public bool CanSelectCompany { get; init; }
        public bool CanSelectBranch { get; init; }
        public bool IsSystemAdmin { get; init; }

        public string CompanyName { get; init; } = string.Empty;
        public string ActiveCompanyName { get; init; } = string.Empty;
        public string HomeBranchName { get; init; } = string.Empty;
        public string ActiveBranchName { get; init; } = string.Empty;
    }

    public interface IUserScopeService
    {
        Task<UserScopeInfo?> GetAsync();
        Task<bool> IsBranchInCurrentCompanyAsync(int branchId);
        Task<bool> IsActiveBranchAsync(int branchId);
        Task<bool> TrySetActiveCompanyAsync(int companyId);
        Task<bool> TrySetActiveBranchAsync(int branchId);
        void ClearSelectedCompany();
        void ClearSelectedBranch();
    }
}
