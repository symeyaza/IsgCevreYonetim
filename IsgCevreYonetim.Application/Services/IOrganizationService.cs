using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Shared.DTOs;

namespace IsgCevreYonetim.Application.Services
{
    public interface IOrganizationService
    {
        // Şirket
        Task<IEnumerable<Company>> GetAllCompaniesAsync();
        Task<Company?> GetCompanyByIdAsync(int id);
        Task<Company> CreateCompanyAsync(Company company);
        Task UpdateCompanyAsync(Company company);
        Task DeleteCompanyAsync(int id);
        Task<bool> CompanyExistsAsync(string kod);
        Task<PaginatedResult<Company>> GetCompaniesAsync(CompanyFilterDto filter);

        // Şube
        Task<IEnumerable<Branch>> GetAllBranchesAsync();
        Task<IEnumerable<Branch>> GetBranchesByCompanyIdAsync(int companyId);
        Task<Branch?> GetBranchByIdAsync(int id);
        Task<Branch> CreateBranchAsync(Branch branch);
        Task UpdateBranchAsync(Branch branch);
        Task DeleteBranchAsync(int id);
        Task<bool> BranchExistsAsync(string kod);
        Task<string> GenerateBranchCodeAsync();
        Task<PaginatedResult<Branch>> GetBranchesAsync(BranchFilterDto filter);

        // Departman
        Task<IEnumerable<Department>> GetAllDepartmentsAsync();
        Task<IEnumerable<Department>> GetDepartmentsByBranchIdAsync(int branchId);
        Task<Department?> GetDepartmentByIdAsync(int id);
        Task<Department> CreateDepartmentAsync(Department department);
        Task UpdateDepartmentAsync(Department department);
        Task DeleteDepartmentAsync(int id);
        Task<bool> DepartmentExistsAsync(string kod);
        Task<string> GenerateDepartmentCodeAsync();
        Task<PaginatedResult<Department>> GetDepartmentsAsync(DepartmentFilterDto filter);

        // Birim
        Task<IEnumerable<Unit>> GetAllUnitsAsync();
        Task<IEnumerable<Unit>> GetUnitsByDepartmentIdAsync(int departmentId);
        Task<IEnumerable<Unit>> GetUnitsByBranchIdAsync(int branchId);
        Task<Unit?> GetUnitByIdAsync(int id);
        Task<Unit> CreateUnitAsync(Unit unit);
        Task UpdateUnitAsync(Unit unit);
        Task DeleteUnitAsync(int id);
        Task<bool> UnitExistsAsync(string kod);
        Task<string> GenerateUnitCodeAsync();
        Task<PaginatedResult<Unit>> GetUnitsAsync(UnitFilterDto filter);
    }
}