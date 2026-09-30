using IsgCevreYonetim.Application.Services;
using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Infrastructure.Data;
using IsgCevreYonetim.Shared.DTOs;
using IsgCevreYonetim.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsgCevreYonetim.Web.Controllers
{
    /// <summary>
    /// Şirket/Şube master yönetimi ayrı kalır; Departman ve Birim işlemleri ise
    /// Dashboard'ta seçili aktif şubeye (ActiveBranchId) kesin olarak bağlıdır.
    /// Böylece listeleme, ekleme, düzenleme, silme ve bağımlı dropdown isteklerinde
    /// URL/form üzerinden başka bir şubeye geçiş yapılamaz.
    /// </summary>
    public class OrganizationController : Controller
    {
        private readonly IOrganizationService _organizationService;
        private readonly IReferenceService _referenceService;
        private readonly IUserScopeService _userScopeService;
        private readonly ApplicationDbContext _context;

        public OrganizationController(
            IOrganizationService organizationService,
            IReferenceService referenceService,
            IUserScopeService userScopeService,
            ApplicationDbContext context)
        {
            _organizationService = organizationService;
            _referenceService = referenceService;
            _userScopeService = userScopeService;
            _context = context;
        }

        // ==================== ŞİRKET ====================
        public async Task<IActionResult> Companies(
            string? searchTerm = null, bool? isActive = null, int page = 1,
            int pageSize = 10, string? sortBy = null, bool sortDescending = false)
        {
            if (await RequireScopeAsync() == null) return Forbid();

            var result = await _organizationService.GetCompaniesAsync(new CompanyFilterDto
            {
                CompanyId = null,
                SearchTerm = searchTerm,
                IsActive = isActive,
                PageNumber = page,
                PageSize = Math.Clamp(pageSize, 1, 100),
                SortBy = sortBy,
                SortDescending = sortDescending
            });

            return View(result);
        }

        [HttpGet]
        public async Task<IActionResult> CompanyEkle()
        {
            if (await RequireScopeAsync() == null) return Forbid();
            return View(new Company());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CompanyEkle(Company company)
        {
            if (await RequireScopeAsync() == null) return Forbid();

            if (string.IsNullOrWhiteSpace(company.CompanyAdi))
            {
                TempData["ToastrError"] = "Şirket adı zorunludur!";
                return View(company);
            }

            if (!string.IsNullOrWhiteSpace(company.CompanyKodu) &&
                await _organizationService.CompanyExistsAsync(company.CompanyKodu))
            {
                TempData["ToastrError"] = "Bu kodda bir şirket zaten mevcut!";
                return View(company);
            }

            await _organizationService.CreateCompanyAsync(company);
            TempData["ToastrSuccess"] = "Şirket başarıyla eklendi!";
            return RedirectToAction(nameof(Companies));
        }

        [HttpGet]
        public async Task<IActionResult> CompanyDuzenle(int id)
        {
            if (await RequireScopeAsync() == null) return Forbid();
            var company = await _organizationService.GetCompanyByIdAsync(id);
            if (company == null)
            {
                TempData["ToastrError"] = "Şirket bulunamadı!";
                return RedirectToAction(nameof(Companies));
            }
            return View(company);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CompanyDuzenle(Company company)
        {
            var scope = await RequireScopeAsync();
            if (scope == null) return Forbid();

            var existing = await _organizationService.GetCompanyByIdAsync(company.Id);
            if (existing == null)
            {
                TempData["ToastrError"] = "Şirket bulunamadı!";
                return RedirectToAction(nameof(Companies));
            }

            if (string.IsNullOrWhiteSpace(company.CompanyAdi))
            {
                TempData["ToastrError"] = "Şirket adı zorunludur!";
                return View(company);
            }

            // Kullanıcının bağlı olduğu şirketin pasifleştirilmesi oturumu scope'suz bırakır.
            if (company.Id == scope.CompanyId && !company.IsActive)
            {
                company.IsActive = true;
                TempData["ToastrWarning"] = "Aktif oturumun bağlı olduğu şirket pasife alınamaz.";
                return View(company);
            }

            await _organizationService.UpdateCompanyAsync(company);
            TempData["ToastrSuccess"] = "Şirket başarıyla güncellendi!";
            return RedirectToAction(nameof(Companies));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CompanySil(int id)
        {
            var scope = await RequireScopeAsync();
            if (scope == null) return Forbid();
            if (id == scope.CompanyId)
            {
                TempData["ToastrWarning"] = "Aktif oturumun bağlı olduğu şirket silinemez.";
                return RedirectToAction(nameof(Companies));
            }

            try
            {
                await _organizationService.DeleteCompanyAsync(id);
                TempData["ToastrSuccess"] = "Şirket başarıyla silindi!";
            }
            catch (Exception ex) { TempData["ToastrError"] = ex.Message; }
            return RedirectToAction(nameof(Companies));
        }

        // ==================== ŞUBE ====================
        public async Task<IActionResult> Branches(
            int? companyId = null, string? searchTerm = null, bool? isActive = null,
            int page = 1, int pageSize = 10, string? sortBy = null, bool sortDescending = false)
        {
            if (await RequireScopeAsync() == null) return Forbid();

            if (companyId.HasValue && await _organizationService.GetCompanyByIdAsync(companyId.Value) == null)
                companyId = null;

            var result = await _organizationService.GetBranchesAsync(new BranchFilterDto
            {
                CompanyId = companyId,
                BranchId = null,
                SearchTerm = searchTerm,
                IsActive = isActive,
                PageNumber = page,
                PageSize = Math.Clamp(pageSize, 1, 100),
                SortBy = sortBy,
                SortDescending = sortDescending
            });

            ViewBag.CompanyId = companyId;
            ViewBag.Companies = await _organizationService.GetAllCompaniesAsync();
            return View(result);
        }

        [HttpGet]
        public async Task<IActionResult> BranchEkle(int? companyId = null)
        {
            var scope = await RequireScopeAsync();
            if (scope == null) return Forbid();

            await LoadAllCompanyDataAsync();

            // Şube oluşturma ORGANİZASYON MASTER işlemidir. Dashboard'taki aktif şube
            // bu işlemi sınırlandırmaz. Süper Admin için yalnızca kolaylık olması amacıyla
            // Dashboard'ta seçili şirket varsayılan seçilir; isterse başka şirket seçebilir.
            if (companyId.HasValue && await _organizationService.GetCompanyByIdAsync(companyId.Value) == null)
                companyId = null;

            if (!companyId.HasValue && scope.IsSystemAdmin && scope.ActiveCompanyId > 0)
                companyId = scope.ActiveCompanyId;

            return View(new Branch { CompanyId = companyId ?? 0 });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> BranchEkle(Branch branch)
        {
            var scope = await RequireScopeAsync();
            if (scope == null) return Forbid();

            // BranchKodu boş bırakılabilir; servis kayıt sırasında otomatik üretir.
            // Non-nullable string nedeniyle MVC'nin eklediği örtük Required hatasını kaldır.
            ModelState.Remove(nameof(branch.BranchKodu));
            // Navigation property formdan post edilmez. CompanyId seçimi yeterlidir; aksi halde
            // nullable olmayan Company navigation alanı MVC tarafından örtük Required sayılır.
            ModelState.Remove(nameof(branch.Company));
            ModelState.Remove(nameof(branch.Departments));
            ModelState.Remove(nameof(branch.Personeller));

            // DİKKAT: Burada branch.CompanyId / yeni şube herhangi bir ActiveBranchId ile
            // karşılaştırılmaz. Süper Admin mevcut bir şubeye bağlı olmadan, seçtiği herhangi
            // bir şirkete yeni şube tanımlayabilir. Dashboard scope'u yalnız operasyonel
            // modüllerde (Personel, İş Kazası, Araştırma vb.) kullanılır.
            var targetCompany = await _organizationService.GetCompanyByIdAsync(branch.CompanyId);
            if (targetCompany == null)
                ModelState.AddModelError(nameof(branch.CompanyId), "Geçerli bir şirket seçiniz.");
            if (string.IsNullOrWhiteSpace(branch.BranchAdi))
                ModelState.AddModelError(nameof(branch.BranchAdi), "Şube adı zorunludur.");
            if (!string.IsNullOrWhiteSpace(branch.BranchKodu) && await _organizationService.BranchExistsAsync(branch.BranchKodu))
                ModelState.AddModelError(nameof(branch.BranchKodu), "Bu kodda bir şube zaten mevcut.");

            if (!ModelState.IsValid)
            {
                await LoadAllCompanyDataAsync();
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();
                TempData["ToastrError"] = errors.Count > 0
                    ? string.Join(" ", errors)
                    : "Şube bilgilerini kontrol ediniz.";
                return View(branch);
            }

            var createdBranch = await _organizationService.CreateBranchAsync(branch);

            // Süper Admin yeni/boş bir şirkette ilk şubeyi oluşturduysa Dashboard kapsamını
            // doğrudan bu şubeye geçir. Böylece yeni şube üzerinde hemen işlem yapılabilir.
            if (scope.IsSystemAdmin && scope.ActiveCompanyId == createdBranch.CompanyId && scope.ActiveBranchId == 0)
            {
                await _userScopeService.TrySetActiveBranchAsync(createdBranch.Id);
            }

            TempData["ToastrSuccess"] = "Şube başarıyla eklendi!";
            return RedirectToAction(nameof(Branches), new { companyId = branch.CompanyId });
        }

        [HttpGet]
        public async Task<IActionResult> BranchDuzenle(int id)
        {
            if (await RequireScopeAsync() == null) return Forbid();
            var branch = await _organizationService.GetBranchByIdAsync(id);
            if (branch == null)
            {
                TempData["ToastrError"] = "Şube bulunamadı!";
                return RedirectToAction(nameof(Branches));
            }

            await LoadAllCompanyDataAsync();
            ViewBag.Ilceler = branch.IlId.HasValue
                ? await _referenceService.GetIlcelerByIlIdAsync(branch.IlId.Value)
                : new List<Ilce>();
            return View(branch);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> BranchDuzenle(Branch branch)
        {
            var scope = await RequireScopeAsync();
            if (scope == null) return Forbid();

            // Entity navigation alanları düzenleme formundan post edilmez.
            ModelState.Remove(nameof(branch.Company));
            ModelState.Remove(nameof(branch.Departments));
            ModelState.Remove(nameof(branch.Personeller));

            var existing = await _organizationService.GetBranchByIdAsync(branch.Id);
            if (existing == null)
            {
                TempData["ToastrError"] = "Şube bulunamadı!";
                return RedirectToAction(nameof(Branches));
            }

            if (await _organizationService.GetCompanyByIdAsync(branch.CompanyId) == null)
                ModelState.AddModelError(nameof(branch.CompanyId), "Geçerli bir şirket seçiniz.");
            if (string.IsNullOrWhiteSpace(branch.BranchAdi))
                ModelState.AddModelError(nameof(branch.BranchAdi), "Şube adı zorunludur.");
            if (!scope.IsSystemAdmin && branch.Id == scope.HomeBranchId && !branch.IsActive)
                ModelState.AddModelError(nameof(branch.IsActive), "Giriş yapan kullanıcının ana şubesi pasife alınamaz.");

            if (!ModelState.IsValid)
            {
                await LoadAllCompanyDataAsync();
                ViewBag.Ilceler = branch.IlId.HasValue
                    ? await _referenceService.GetIlcelerByIlIdAsync(branch.IlId.Value)
                    : new List<Ilce>();
                TempData["ToastrError"] = "Şube bilgilerini kontrol ediniz.";
                return View(branch);
            }

            await _organizationService.UpdateBranchAsync(branch);
            TempData["ToastrSuccess"] = "Şube başarıyla güncellendi!";
            return RedirectToAction(nameof(Branches), new { companyId = branch.CompanyId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> BranchSil(int id)
        {
            var scope = await RequireScopeAsync();
            if (scope == null) return Forbid();
            if (!scope.IsSystemAdmin && id == scope.HomeBranchId)
            {
                TempData["ToastrWarning"] = "Giriş yapan kullanıcının ana şubesi silinemez.";
                return RedirectToAction(nameof(Branches));
            }

            try
            {
                await _organizationService.DeleteBranchAsync(id);
                TempData["ToastrSuccess"] = "Şube başarıyla silindi!";
            }
            catch (Exception ex) { TempData["ToastrError"] = ex.Message; }
            return RedirectToAction(nameof(Branches));
        }

        // ==================== DEPARTMAN ====================
        public async Task<IActionResult> Departments(
            int? branchId = null, string? searchTerm = null, bool? isActive = null,
            int page = 1, int pageSize = 10, string? sortBy = null, bool sortDescending = false)
        {
            var scope = await RequireScopeAsync();
            if (scope == null || scope.ActiveBranchId <= 0) return Forbid();

            // QueryString'den gelen branchId bilinçli olarak kullanılmaz.
            // Departman ekranının tek kapsamı Dashboard'taki aktif şubedir.
            branchId = scope.ActiveBranchId;

            var result = await _organizationService.GetDepartmentsAsync(new DepartmentFilterDto
            {
                BranchId = scope.ActiveBranchId,
                SearchTerm = searchTerm,
                IsActive = isActive,
                PageNumber = page,
                PageSize = Math.Clamp(pageSize, 1, 100),
                SortBy = sortBy,
                SortDescending = sortDescending
            });

            ViewBag.BranchId = scope.ActiveBranchId;
            ViewBag.Branches = new List<Branch>
            {
                new() { Id = scope.ActiveBranchId, CompanyId = scope.ActiveCompanyId, BranchAdi = scope.ActiveBranchName }
            };
            return View(result);
        }

        [HttpGet]
        public async Task<IActionResult> DepartmentEkle(int? branchId = null)
        {
            var scope = await RequireScopeAsync();
            if (scope == null || scope.ActiveBranchId <= 0) return Forbid();
            await LoadActiveBranchDataAsync(scope);
            return View(new Department { BranchId = scope.ActiveBranchId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DepartmentEkle(Department department)
        {
            var scope = await RequireScopeAsync();
            if (scope == null || scope.ActiveBranchId <= 0) return Forbid();

            // Formdan farklı BranchId gönderilse bile aktif şubeye zorla.
            department.BranchId = scope.ActiveBranchId;
            ModelState.Remove(nameof(department.BranchId));
            ModelState.Remove(nameof(department.DepartmentKodu));
            ModelState.Remove(nameof(department.Branch));
            ModelState.Remove(nameof(department.Units));
            ModelState.Remove(nameof(department.Personeller));

            if (string.IsNullOrWhiteSpace(department.DepartmentAdi))
                ModelState.AddModelError(nameof(department.DepartmentAdi), "Departman adı zorunludur.");
            if (!string.IsNullOrWhiteSpace(department.DepartmentKodu) && await _organizationService.DepartmentExistsAsync(department.DepartmentKodu))
                ModelState.AddModelError(nameof(department.DepartmentKodu), "Bu kodda bir departman zaten mevcut.");

            if (!ModelState.IsValid)
            {
                await LoadActiveBranchDataAsync(scope);
                TempData["ToastrError"] = "Departman bilgilerini kontrol ediniz.";
                return View(department);
            }

            await _organizationService.CreateDepartmentAsync(department);
            TempData["ToastrSuccess"] = "Departman başarıyla eklendi!";
            return RedirectToAction(nameof(Departments));
        }

        [HttpGet]
        public async Task<IActionResult> DepartmentDuzenle(int id)
        {
            var scope = await RequireScopeAsync();
            if (scope == null || scope.ActiveBranchId <= 0) return Forbid();
            var department = await _organizationService.GetDepartmentByIdAsync(id);
            if (department == null || department.BranchId != scope.ActiveBranchId)
            {
                TempData["ToastrError"] = "Departman aktif şubede bulunamadı!";
                return RedirectToAction(nameof(Departments));
            }
            await LoadActiveBranchDataAsync(scope);
            return View(department);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DepartmentDuzenle(Department department)
        {
            var scope = await RequireScopeAsync();
            if (scope == null || scope.ActiveBranchId <= 0) return Forbid();

            var existing = await _organizationService.GetDepartmentByIdAsync(department.Id);
            if (existing == null || existing.BranchId != scope.ActiveBranchId)
            {
                TempData["ToastrError"] = "Departman aktif şubede bulunamadı!";
                return RedirectToAction(nameof(Departments));
            }

            department.BranchId = scope.ActiveBranchId;
            ModelState.Remove(nameof(department.BranchId));
            ModelState.Remove(nameof(department.Branch));
            ModelState.Remove(nameof(department.Units));
            ModelState.Remove(nameof(department.Personeller));

            if (string.IsNullOrWhiteSpace(department.DepartmentAdi))
                ModelState.AddModelError(nameof(department.DepartmentAdi), "Departman adı zorunludur.");

            if (!ModelState.IsValid)
            {
                await LoadActiveBranchDataAsync(scope);
                TempData["ToastrError"] = "Departman bilgilerini kontrol ediniz.";
                return View(department);
            }

            await _organizationService.UpdateDepartmentAsync(department);
            TempData["ToastrSuccess"] = "Departman başarıyla güncellendi!";
            return RedirectToAction(nameof(Departments));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DepartmentSil(int id)
        {
            var scope = await RequireScopeAsync();
            if (scope == null || scope.ActiveBranchId <= 0) return Forbid();
            var department = await _organizationService.GetDepartmentByIdAsync(id);
            if (department == null || department.BranchId != scope.ActiveBranchId)
            {
                TempData["ToastrError"] = "Bu departman aktif şubeye ait değil.";
                return RedirectToAction(nameof(Departments));
            }
            try
            {
                await _organizationService.DeleteDepartmentAsync(id);
                TempData["ToastrSuccess"] = "Departman başarıyla silindi!";
            }
            catch (Exception ex) { TempData["ToastrError"] = ex.Message; }
            return RedirectToAction(nameof(Departments));
        }

        // ==================== BİRİM ====================
        public async Task<IActionResult> Units(
            int? departmentId = null, string? searchTerm = null, bool? isActive = null,
            int page = 1, int pageSize = 10, string? sortBy = null, bool sortDescending = false)
        {
            var scope = await RequireScopeAsync();
            if (scope == null || scope.ActiveBranchId <= 0) return Forbid();

            if (departmentId.HasValue)
            {
                var selectedDepartment = await _organizationService.GetDepartmentByIdAsync(departmentId.Value);
                if (selectedDepartment == null || selectedDepartment.BranchId != scope.ActiveBranchId)
                    departmentId = null;
            }

            var result = await _organizationService.GetUnitsAsync(new UnitFilterDto
            {
                BranchId = scope.ActiveBranchId,
                DepartmentId = departmentId,
                SearchTerm = searchTerm,
                IsActive = isActive,
                PageNumber = page,
                PageSize = Math.Clamp(pageSize, 1, 100),
                SortBy = sortBy,
                SortDescending = sortDescending
            });

            ViewBag.DepartmentId = departmentId;
            ViewBag.Departments = await _organizationService.GetDepartmentsByBranchIdAsync(scope.ActiveBranchId);
            return View(result);
        }

        [HttpGet]
        public async Task<IActionResult> UnitEkle(int? departmentId = null)
        {
            var scope = await RequireScopeAsync();
            if (scope == null || scope.ActiveBranchId <= 0) return Forbid();
            await LoadActiveDepartmentDataAsync(scope);
            if (departmentId.HasValue)
            {
                var department = await _organizationService.GetDepartmentByIdAsync(departmentId.Value);
                if (department == null || department.BranchId != scope.ActiveBranchId) departmentId = null;
            }
            return View(new Unit { DepartmentId = departmentId ?? 0 });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UnitEkle(Unit unit)
        {
            var scope = await RequireScopeAsync();
            if (scope == null || scope.ActiveBranchId <= 0) return Forbid();

            ModelState.Remove(nameof(unit.UnitKodu));
            ModelState.Remove(nameof(unit.Department));
            ModelState.Remove(nameof(unit.Personeller));

            var department = await _organizationService.GetDepartmentByIdAsync(unit.DepartmentId);
            if (department == null || department.BranchId != scope.ActiveBranchId)
                ModelState.AddModelError(nameof(unit.DepartmentId), "Aktif şubeye ait geçerli bir departman seçiniz.");
            if (string.IsNullOrWhiteSpace(unit.UnitAdi))
                ModelState.AddModelError(nameof(unit.UnitAdi), "Birim adı zorunludur.");
            if (!string.IsNullOrWhiteSpace(unit.UnitKodu) && await _organizationService.UnitExistsAsync(unit.UnitKodu))
                ModelState.AddModelError(nameof(unit.UnitKodu), "Bu kodda bir birim zaten mevcut.");

            if (!ModelState.IsValid)
            {
                await LoadActiveDepartmentDataAsync(scope);
                TempData["ToastrError"] = "Birim bilgilerini kontrol ediniz.";
                return View(unit);
            }

            await _organizationService.CreateUnitAsync(unit);
            TempData["ToastrSuccess"] = "Birim başarıyla eklendi!";
            return RedirectToAction(nameof(Units));
        }

        [HttpGet]
        public async Task<IActionResult> UnitDuzenle(int id)
        {
            var scope = await RequireScopeAsync();
            if (scope == null || scope.ActiveBranchId <= 0) return Forbid();
            var unit = await _organizationService.GetUnitByIdAsync(id);
            if (unit == null)
            {
                TempData["ToastrError"] = "Birim bulunamadı!";
                return RedirectToAction(nameof(Units));
            }
            var department = await _organizationService.GetDepartmentByIdAsync(unit.DepartmentId);
            if (department == null || department.BranchId != scope.ActiveBranchId) return Forbid();
            await LoadActiveDepartmentDataAsync(scope);
            return View(unit);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UnitDuzenle(Unit unit)
        {
            var scope = await RequireScopeAsync();
            if (scope == null || scope.ActiveBranchId <= 0) return Forbid();

            ModelState.Remove(nameof(unit.Department));
            ModelState.Remove(nameof(unit.Personeller));

            var existing = await _organizationService.GetUnitByIdAsync(unit.Id);
            if (existing == null) return Forbid();
            var existingDepartment = await _organizationService.GetDepartmentByIdAsync(existing.DepartmentId);
            if (existingDepartment == null || existingDepartment.BranchId != scope.ActiveBranchId) return Forbid();

            var targetDepartment = await _organizationService.GetDepartmentByIdAsync(unit.DepartmentId);
            if (targetDepartment == null || targetDepartment.BranchId != scope.ActiveBranchId)
                ModelState.AddModelError(nameof(unit.DepartmentId), "Aktif şubeye ait geçerli bir departman seçiniz.");
            if (string.IsNullOrWhiteSpace(unit.UnitAdi))
                ModelState.AddModelError(nameof(unit.UnitAdi), "Birim adı zorunludur.");

            if (!ModelState.IsValid)
            {
                await LoadActiveDepartmentDataAsync(scope);
                TempData["ToastrError"] = "Birim bilgilerini kontrol ediniz.";
                return View(unit);
            }

            await _organizationService.UpdateUnitAsync(unit);
            TempData["ToastrSuccess"] = "Birim başarıyla güncellendi!";
            return RedirectToAction(nameof(Units));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UnitSil(int id)
        {
            var scope = await RequireScopeAsync();
            if (scope == null || scope.ActiveBranchId <= 0)
                return Json(new { success = false, message = "Aktif şube bilgisi bulunamadı." });

            var unit = await _organizationService.GetUnitByIdAsync(id);
            var department = unit == null ? null : await _organizationService.GetDepartmentByIdAsync(unit.DepartmentId);
            if (unit == null || department == null || department.BranchId != scope.ActiveBranchId)
                return Json(new { success = false, message = "Bu birim aktif şubeye ait değil." });

            try
            {
                await _organizationService.DeleteUnitAsync(id);
                return Json(new { success = true, message = "Birim başarıyla silindi!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =====================================================
        // OPERASYONEL BAĞIMLI DROPDOWN API'LERİ
        // Personel/İş Kazası gibi Dashboard-scope kullanan ekranlar içindir.
        // Organizasyon master CRUD formları bu uçlara bağlı değildir.
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> BranchesByCompany(int companyId)
        {
            var scope = await RequireScopeAsync();
            if (scope == null) return Forbid();

            // Dropdown endpoint'leri yalnız ihtiyaç duyulan iki kolonu tek SQL ile döndürür.
            // Önceden önce Company/Branch doğrulaması, sonra liste sorgusu olmak üzere
            // iki ayrı DB round-trip oluşuyordu.
            if (!scope.IsSystemAdmin && companyId != scope.CompanyId)
                return Forbid();

            var query = _context.Branches
                .AsNoTracking()
                .Where(b => b.CompanyId == companyId && !b.IsDeleted && b.IsActive);

            if (!scope.IsSystemAdmin)
                query = query.Where(b => b.Id == scope.ActiveBranchId);

            var items = await query
                .OrderBy(b => b.BranchAdi)
                .Select(b => new { id = b.Id, text = b.BranchAdi })
                .ToListAsync();

            return Json(items);
        }

        [HttpGet]
        public async Task<IActionResult> DepartmentsByBranch(int branchId)
        {
            var scope = await RequireScopeAsync();
            if (scope == null) return Forbid();

            if (branchId != scope.ActiveBranchId)
                return Forbid();

            var query = _context.Departments
                .AsNoTracking()
                .Where(d =>
                    d.BranchId == branchId &&
                    !d.IsDeleted &&
                    d.IsActive);

            // Şube Dashboard seçimidir; şirket bağını da aynı SQL içinde doğrula.
            var activeCompanyId = scope.IsSystemAdmin ? scope.ActiveCompanyId : scope.CompanyId;
            query = query.Where(d => d.Branch.CompanyId == activeCompanyId);

            var items = await query
                .OrderBy(d => d.DepartmentAdi)
                .Select(d => new { id = d.Id, text = d.DepartmentAdi })
                .ToListAsync();

            return Json(items);
        }

        [HttpGet]
        public async Task<IActionResult> UnitsByDepartment(int departmentId)
        {
            var scope = await RequireScopeAsync();
            if (scope == null) return Forbid();

            // Birim dropdown'ı kritik sıcak yol: Department kontrolü + Unit listesi
            // iki SQL yerine tek projection sorgusunda yapılır.
            var query = _context.Units
                .AsNoTracking()
                .Where(u =>
                    u.DepartmentId == departmentId &&
                    !u.IsDeleted &&
                    u.IsActive);

            var activeCompanyId = scope.IsSystemAdmin ? scope.ActiveCompanyId : scope.CompanyId;
            query = query.Where(u =>
                u.Department.BranchId == scope.ActiveBranchId &&
                u.Department.Branch.CompanyId == activeCompanyId);

            var items = await query
                .OrderBy(u => u.UnitAdi)
                .Select(u => new { id = u.Id, text = u.UnitAdi })
                .ToListAsync();

            return Json(items);
        }

        private Task<UserScopeInfo?> RequireScopeAsync() => _userScopeService.GetAsync();

        private async Task LoadAllCompanyDataAsync()
        {
            ViewBag.Companies = await _organizationService.GetAllCompaniesAsync();
            ViewBag.Iller = await _referenceService.GetAllIllerAsync();
        }

        private Task LoadActiveBranchDataAsync(UserScopeInfo scope)
        {
            ViewBag.Branches = new List<Branch>
            {
                new() { Id = scope.ActiveBranchId, CompanyId = scope.ActiveCompanyId, BranchAdi = scope.ActiveBranchName }
            };
            return Task.CompletedTask;
        }

        private async Task LoadActiveDepartmentDataAsync(UserScopeInfo scope)
        {
            ViewBag.Departments = await _organizationService.GetDepartmentsByBranchIdAsync(scope.ActiveBranchId);
        }
    }
}
