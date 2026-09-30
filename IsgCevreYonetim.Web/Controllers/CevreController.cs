using System.ComponentModel.DataAnnotations;
using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Infrastructure.Data;
using IsgCevreYonetim.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace IsgCevreYonetim.Web.Controllers;

[Authorize]
public class CevreController : Controller
{
 private readonly ApplicationDbContext _db;
 private readonly IUserScopeService _scope;
 private readonly IWebHostEnvironment _environment;
 public CevreController(ApplicationDbContext db,IUserScopeService scope,IWebHostEnvironment environment) { _db=db;_scope=scope;_environment=environment; }
 private async Task<int> Branch() => (await _scope.GetAsync())?.ActiveBranchId ?? 0;
 public IActionResult Index() => View();
 private async Task<IActionResult> Catalog<T>(string title,string prefix,IQueryable<T> query,int page,int pageSize) where T : class
 {
  pageSize = pageSize is 5 or 10 or 25 or 50 or 100 ? pageSize : 10;
  var count = await query.CountAsync();
  page = Math.Clamp(page,1,Math.Max(1,(int)Math.Ceiling((double)count/pageSize)));
  ViewBag.Title=title;ViewBag.Prefix=prefix;
  ViewBag.Pagination = new IsgCevreYonetim.Shared.DTOs.PaginatedResult<object> { TotalCount=count, PageNumber=page, PageSize=pageSize };
  return View("Catalog",(await query.Skip((page-1)*pageSize).Take(pageSize).ToListAsync()).Cast<object>().ToList());
 }
 private IActionResult CatalogForm(string title,string prefix,object value) { ViewBag.Title=title;ViewBag.Prefix=prefix;return View("CatalogForm",value); }
 private IActionResult CatalogDetail(string title,string prefix,object value) { ViewBag.Title=title;ViewBag.Prefix=prefix;return View("CatalogDetail",value); }
 public async Task<IActionResult> Turler(string? searchTerm=null,int page=1,int pageSize=10) { var b=await Branch(); ViewBag.SearchTerm=searchTerm; await Lookups(); var query=_db.AtikTurleri.AsNoTracking().Where(x=>x.BranchId==b); if(!string.IsNullOrWhiteSpace(searchTerm)) query=query.Where(x=>x.AtikTurAdi.Contains(searchTerm)); return await Catalog("Atık Türleri","Tur",query.OrderBy(x=>x.AtikTurAdi).ThenBy(x=>x.Id),page,pageSize); }
 [HttpGet] public async Task<IActionResult> TurEkle() { await Lookups(); return CatalogForm("Atık Türleri","Tur",new AtikTuru()); }
 [HttpGet] public async Task<IActionResult> TurDuzenle(int id) { var b=await Branch(); var x=await _db.AtikTurleri.FirstOrDefaultAsync(x=>x.Id==id && x.BranchId==b); if(x==null)return NotFound(); await Lookups(); return CatalogForm("Atık Türleri","Tur",x); }
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> TurEkle(AtikTuru model) => await SaveTur(model,false);
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> TurDuzenle(AtikTuru model) => await SaveTur(model,true);
 private async Task<IActionResult> SaveTur(AtikTuru model,bool edit) {
  var b=await Branch(); if(b==0)return Forbid();
  var row=edit?await _db.AtikTurleri.FirstOrDefaultAsync(x=>x.Id==model.Id && x.BranchId==b):null;
  if(edit && row==null)return NotFound();
  if(!ModelState.IsValid) { await Lookups(); return CatalogForm("Atık Türleri","Tur",model); }
  row ??=new AtikTuru { BranchId=b,CreatedDate=DateTime.Now };
  row.AtikTurAdi=model.AtikTurAdi.Trim();
  if(edit)row.UpdatedDate=DateTime.Now;else _db.AtikTurleri.Add(row);
  try { await _db.SaveChangesAsync(); } catch(DbUpdateException) { ModelState.AddModelError("","Bu kayıt zaten mevcut veya ilişkili kayıt geçersiz.");await Lookups();return CatalogForm("Atık Türleri","Tur",model); }
  return RedirectToAction(nameof(Turler));
 }
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> TurSil(int id) { var b=await Branch();var x=await _db.AtikTurleri.FirstOrDefaultAsync(x=>x.Id==id && x.BranchId==b);if(x==null)return NotFound();
 if(await _db.Atiklar.AnyAsync(y=>y.AtikTuruId==id && y.BranchId==b)) { TempData["ToastrError"]="Bu kayıt kullanıldığı için silinemez."; return RedirectToAction(nameof(Turler)); } _db.Remove(x);await _db.SaveChangesAsync();return RedirectToAction(nameof(Turler)); }
 public async Task<IActionResult> FirmaTurleri(string? searchTerm=null,int page=1,int pageSize=10) { var b=await Branch(); ViewBag.SearchTerm=searchTerm; await Lookups(); var query=_db.AtikFirmaTurleri.AsNoTracking().Where(x=>x.BranchId==b); if(!string.IsNullOrWhiteSpace(searchTerm)) query=query.Where(x=>x.AtikFirmaTuruAdi.Contains(searchTerm)); return await Catalog("Atık Firma Türleri","FirmaTur",query.OrderBy(x=>x.AtikFirmaTuruAdi).ThenBy(x=>x.Id),page,pageSize); }
 [HttpGet] public async Task<IActionResult> FirmaTurEkle() { await Lookups(); return CatalogForm("Atık Firma Türleri","FirmaTur",new AtikFirmaTuru()); }
 [HttpGet] public async Task<IActionResult> FirmaTurDuzenle(int id) { var b=await Branch(); var x=await _db.AtikFirmaTurleri.FirstOrDefaultAsync(x=>x.Id==id && x.BranchId==b); if(x==null)return NotFound(); await Lookups(); return CatalogForm("Atık Firma Türleri","FirmaTur",x); }
 [HttpGet] public async Task<IActionResult> FirmaTurDetay(int id) { var b=await Branch(); var x=await _db.AtikFirmaTurleri.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id && x.BranchId==b); if(x==null)return NotFound(); await Lookups(); return CatalogDetail("Atık Firma Türleri","FirmaTur",x); }
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> FirmaTurEkle(AtikFirmaTuru model) => await SaveFirmaTur(model,false);
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> FirmaTurDuzenle(AtikFirmaTuru model) => await SaveFirmaTur(model,true);
 private async Task<IActionResult> SaveFirmaTur(AtikFirmaTuru model,bool edit) {
  var b=await Branch(); if(b==0)return Forbid();
  var row=edit?await _db.AtikFirmaTurleri.FirstOrDefaultAsync(x=>x.Id==model.Id && x.BranchId==b):null;
  if(edit && row==null)return NotFound();
  if(!ModelState.IsValid) { await Lookups(); return CatalogForm("Atık Firma Türleri","FirmaTur",model); }
  row ??=new AtikFirmaTuru { BranchId=b,CreatedDate=DateTime.Now };
  row.AtikFirmaTuruAdi=model.AtikFirmaTuruAdi.Trim();
  if(edit)row.UpdatedDate=DateTime.Now;else _db.AtikFirmaTurleri.Add(row);
  try { await _db.SaveChangesAsync(); } catch(DbUpdateException) { ModelState.AddModelError("","Bu kayıt zaten mevcut veya ilişkili kayıt geçersiz.");await Lookups();return CatalogForm("Atık Firma Türleri","FirmaTur",model); }
  return RedirectToAction(nameof(FirmaTurleri));
 }
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> FirmaTurSil(int id) { var b=await Branch();var x=await _db.AtikFirmaTurleri.FirstOrDefaultAsync(x=>x.Id==id && x.BranchId==b);if(x==null)return NotFound();
 if(await _db.AtikFirmalari.AnyAsync(y=>y.AtikFirmaTuruId==id && y.BranchId==b)) { TempData["ToastrError"]="Bu kayıt kullanıldığı için silinemez."; return RedirectToAction(nameof(FirmaTurleri)); } _db.Remove(x);await _db.SaveChangesAsync();return RedirectToAction(nameof(FirmaTurleri)); }
 public async Task<IActionResult> Firmalar(string? searchTerm=null,int page=1,int pageSize=10) { var b=await Branch(); ViewBag.SearchTerm=searchTerm; await Lookups(); var query=_db.AtikFirmalari.AsNoTracking().Where(x=>x.BranchId==b); if(!string.IsNullOrWhiteSpace(searchTerm)) query=query.Where(x=>x.AtikFirmaAdi.Contains(searchTerm) || x.LisansNo != null && x.LisansNo.Contains(searchTerm)); return await Catalog("Atık Firmaları","Firma",query.OrderBy(x=>x.AtikFirmaAdi).ThenBy(x=>x.Id),page,pageSize); }
 [HttpGet] public async Task<IActionResult> FirmaEkle() { await Lookups(); return CatalogForm("Atık Firmaları","Firma",new AtikFirma()); }
 [HttpGet] public async Task<IActionResult> FirmaDuzenle(int id) { var b=await Branch(); var x=await _db.AtikFirmalari.FirstOrDefaultAsync(x=>x.Id==id && x.BranchId==b); if(x==null)return NotFound(); await Lookups(); return CatalogForm("Atık Firmaları","Firma",x); }
 [HttpGet] public async Task<IActionResult> FirmaDetay(int id) { var b=await Branch(); var x=await _db.AtikFirmalari.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id && x.BranchId==b); if(x==null)return NotFound(); await Lookups(); return CatalogDetail("Atık Firmaları","Firma",x); }
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> FirmaEkle(AtikFirma model) => await SaveFirma(model,false);
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> FirmaDuzenle(AtikFirma model) => await SaveFirma(model,true);
 private async Task<IActionResult> SaveFirma(AtikFirma model,bool edit) {
  var b=await Branch(); if(b==0)return Forbid();
  var row=edit?await _db.AtikFirmalari.FirstOrDefaultAsync(x=>x.Id==model.Id && x.BranchId==b):null;
  if(edit && row==null)return NotFound();
  if(!await _db.AtikFirmaTurleri.AnyAsync(x=>x.Id==model.AtikFirmaTuruId && x.BranchId==b)) ModelState.AddModelError(nameof(model.AtikFirmaTuruId),"Geçerli firma türü seçin.");
  model.AtikFirmaAdi = (model.AtikFirmaAdi ?? "").Trim();
  if(model.AtikFirmaAdi.Length > 0 && await _db.AtikFirmalari.AnyAsync(x=>x.Id!=model.Id && x.BranchId==b && x.AtikFirmaTuruId==model.AtikFirmaTuruId && x.AtikFirmaAdi==model.AtikFirmaAdi))
      ModelState.AddModelError(nameof(model.AtikFirmaAdi),"Bu firma aynı firma türünde zaten kayıtlı.");
  if(!ModelState.IsValid) { await Lookups(); return CatalogForm("Atık Firmaları","Firma",model); }
  row ??=new AtikFirma { BranchId=b,CreatedDate=DateTime.Now };
  row.AtikFirmaAdi=model.AtikFirmaAdi.Trim();
  row.LisansNo=model.LisansNo?.Trim();
  row.AtikFirmaTuruId=model.AtikFirmaTuruId;
  if(edit)row.UpdatedDate=DateTime.Now;else _db.AtikFirmalari.Add(row);
  try { await _db.SaveChangesAsync(); } catch(DbUpdateException) { ModelState.AddModelError("","Bu kayıt zaten mevcut veya ilişkili kayıt geçersiz.");await Lookups();return CatalogForm("Atık Firmaları","Firma",model); }
  return RedirectToAction(nameof(Firmalar));
 }
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> FirmaSil(int id) { var b=await Branch();var x=await _db.AtikFirmalari.FirstOrDefaultAsync(x=>x.Id==id && x.BranchId==b);if(x==null)return NotFound();
 if(await _db.AtikTakipleri.AnyAsync(y=>(y.TasiyiciFirmaId==id || y.AliciFirmaId==id) && y.BranchId==b)) { TempData["ToastrError"]="Bu kayıt kullanıldığı için silinemez."; return RedirectToAction(nameof(Firmalar)); } _db.Remove(x);await _db.SaveChangesAsync();return RedirectToAction(nameof(Firmalar)); }
 public async Task<IActionResult> Atiklar(string? searchTerm=null,int page=1,int pageSize=10) { var b=await Branch(); ViewBag.SearchTerm=searchTerm; await Lookups(); var query=_db.Atiklar.AsNoTracking().Where(x=>x.BranchId==b); if(!string.IsNullOrWhiteSpace(searchTerm)) query=query.Where(x=>x.AtikAdi.Contains(searchTerm) || x.AtikKodu.Contains(searchTerm)); return await Catalog("Atıklar","Atik",query.OrderBy(x=>x.AtikAdi).ThenBy(x=>x.Id),page,pageSize); }
 [HttpGet] public async Task<IActionResult> AtikEkle() { await Lookups(); return CatalogForm("Atıklar","Atik",new Atik()); }
 [HttpGet] public async Task<IActionResult> AtikDuzenle(int id) { var b=await Branch(); var x=await _db.Atiklar.FirstOrDefaultAsync(x=>x.Id==id && x.BranchId==b); if(x==null)return NotFound(); await Lookups(); return CatalogForm("Atıklar","Atik",x); }
 [HttpGet] public async Task<IActionResult> AtikDetay(int id) { var b=await Branch(); var x=await _db.Atiklar.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id && x.BranchId==b); if(x==null)return NotFound(); await Lookups(); return CatalogDetail("Atıklar","Atik",x); }
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> AtikEkle(Atik model) => await SaveAtik(model,false);
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> AtikDuzenle(Atik model) => await SaveAtik(model,true);
 private async Task<IActionResult> SaveAtik(Atik model,bool edit) {
  var b=await Branch(); if(b==0)return Forbid();
  var row=edit?await _db.Atiklar.FirstOrDefaultAsync(x=>x.Id==model.Id && x.BranchId==b):null;
  if(edit && row==null)return NotFound();
  if(!await _db.AtikTurleri.AnyAsync(x=>x.Id==model.AtikTuruId && x.BranchId==b)) ModelState.AddModelError(nameof(model.AtikTuruId),"Geçerli atık türü seçin.");
  if(!ModelState.IsValid) { await Lookups(); return CatalogForm("Atıklar","Atik",model); }
  row ??=new Atik { BranchId=b,CreatedDate=DateTime.Now };
  row.AtikAdi=model.AtikAdi.Trim();
  row.AtikKodu=model.AtikKodu.Trim();
  row.AtikTuruId=model.AtikTuruId;
  if(edit)row.UpdatedDate=DateTime.Now;else _db.Atiklar.Add(row);
  try { await _db.SaveChangesAsync(); } catch(DbUpdateException) { ModelState.AddModelError("","Bu kayıt zaten mevcut veya ilişkili kayıt geçersiz.");await Lookups();return CatalogForm("Atıklar","Atik",model); }
  return RedirectToAction(nameof(Atiklar));
 }
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> AtikSil(int id) { var b=await Branch();var x=await _db.Atiklar.FirstOrDefaultAsync(x=>x.Id==id && x.BranchId==b);if(x==null)return NotFound();
 if(await _db.AtikTakipleri.AnyAsync(y=>y.AtikId==id && y.BranchId==b)) { TempData["ToastrError"]="Bu kayıt kullanıldığı için silinemez."; return RedirectToAction(nameof(Atiklar)); } _db.Remove(x);await _db.SaveChangesAsync();return RedirectToAction(nameof(Atiklar)); }
 private async Task Lookups() { var b=await Branch();ViewBag.Turler=await _db.AtikTurleri.AsNoTracking().Where(x=>x.BranchId==b).OrderBy(x=>x.AtikTurAdi).ToListAsync();ViewBag.FirmaTurleri=await _db.AtikFirmaTurleri.AsNoTracking().Where(x=>x.BranchId==b).OrderBy(x=>x.AtikFirmaTuruAdi).ToListAsync();ViewBag.Firmalar=await _db.AtikFirmalari.AsNoTracking().Where(x=>x.BranchId==b).OrderBy(x=>x.AtikFirmaAdi).ToListAsync(); ViewBag.TasiyiciFirmalar=await _db.AtikFirmalari.AsNoTracking().Where(x=>x.BranchId==b && x.AtikFirmaTuru!=null && x.AtikFirmaTuru.AtikFirmaTuruAdi.StartsWith("Taşıyıcı")).OrderBy(x=>x.AtikFirmaAdi).ToListAsync(); ViewBag.AliciFirmalar=await _db.AtikFirmalari.AsNoTracking().Where(x=>x.BranchId==b && x.AtikFirmaTuru!=null && x.AtikFirmaTuru.AtikFirmaTuruAdi.StartsWith("Alıcı")).OrderBy(x=>x.AtikFirmaAdi).ToListAsync(); ViewBag.Atiklar=await _db.Atiklar.AsNoTracking().Include(x=>x.AtikTuru).Where(x=>x.BranchId==b).OrderBy(x=>x.AtikAdi).ToListAsync(); }
 private IQueryable<AtikTakibi> Track(int branch) => _db.AtikTakipleri.Where(x=>x.BranchId==branch);
 public async Task<IActionResult> Takip(int page=1,string? searchTerm=null,int pageSize=10)
 {
  var b=await Branch(); var q=Track(b).AsNoTracking();
  if(!string.IsNullOrWhiteSpace(searchTerm)) q=q.Where(x=>(x.TasimaMotAtNo!=null && x.TasimaMotAtNo.Contains(searchTerm)) || x.Atik!.AtikAdi.Contains(searchTerm));
  pageSize=pageSize is 5 or 10 or 25 or 50 or 100 ? pageSize : 10;
  var count=await q.CountAsync(); page=Math.Clamp(page,1,Math.Max(1,(int)Math.Ceiling((double)count/pageSize)));
  ViewBag.SearchTerm=searchTerm;
  ViewBag.Pagination=new IsgCevreYonetim.Shared.DTOs.PaginatedResult<object>{TotalCount=count,PageNumber=page,PageSize=pageSize};
  return View(await q.Include(x=>x.Atik).Include(x=>x.TasiyiciFirma).Include(x=>x.AliciFirma).OrderByDescending(x=>x.Tarih).ThenByDescending(x=>x.Id).Skip((page-1)*pageSize).Take(pageSize).ToListAsync());
 }
 [HttpGet] public async Task<IActionResult> TakipEkle() { await Lookups();return View("TakipForm",new AtikTakibi()); }
 [HttpGet] public async Task<IActionResult> TakipDuzenle(int id) { var b=await Branch();var x=await Track(b).Include(x=>x.Dosyalar).FirstOrDefaultAsync(x=>x.Id==id);if(x==null)return NotFound();await Lookups();return View("TakipForm",x); }
 [HttpGet] public async Task<IActionResult> TakipDetay(int id) { var b=await Branch();var x=await Track(b).AsNoTracking().Include(x=>x.Dosyalar).Include(x=>x.Atik).ThenInclude(x=>x!.AtikTuru).Include(x=>x.TasiyiciFirma).Include(x=>x.AliciFirma).FirstOrDefaultAsync(x=>x.Id==id);return x==null?NotFound():View(x); }
 [HttpPost,ValidateAntiForgeryToken,RequestSizeLimit(28000000)] public async Task<IActionResult> TakipEkle(AtikTakibi model,List<IFormFile>? dosyalar) => await SaveTrack(model,dosyalar,false);
 [HttpPost,ValidateAntiForgeryToken,RequestSizeLimit(28000000)] public async Task<IActionResult> TakipDuzenle(AtikTakibi model,List<IFormFile>? dosyalar) => await SaveTrack(model,dosyalar,true);
 private async Task<IActionResult> SaveTrack(AtikTakibi model,List<IFormFile>? files,bool edit) {
  var b=await Branch();if(b==0)return Forbid();var x=edit?await Track(b).FirstOrDefaultAsync(x=>x.Id==model.Id):null;if(edit && x==null)return NotFound();
  ModelState.Remove(nameof(model.Dosyalar));ModelState.Remove(nameof(model.Atik));ModelState.Remove(nameof(model.AliciFirma));ModelState.Remove(nameof(model.TasiyiciFirma));
  if(!await _db.Atiklar.AnyAsync(a=>a.Id==model.AtikId && a.BranchId==b))ModelState.AddModelError(nameof(model.AtikId),"Geçerli atık seçin.");
  if(!await _db.AtikFirmalari.AnyAsync(a=>a.Id==model.TasiyiciFirmaId && a.BranchId==b && a.AtikFirmaTuru!=null && a.AtikFirmaTuru.AtikFirmaTuruAdi.StartsWith("Taşıyıcı")))ModelState.AddModelError(nameof(model.TasiyiciFirmaId),"Taşıyıcı firma türünde bir firma seçin.");
  var alici=await _db.AtikFirmalari.AsNoTracking().FirstOrDefaultAsync(a=>a.Id==model.AliciFirmaId && a.BranchId==b && a.AtikFirmaTuru!=null && a.AtikFirmaTuru.AtikFirmaTuruAdi.StartsWith("Alıcı"));if(alici==null)ModelState.AddModelError(nameof(model.AliciFirmaId),"Alıcı firma türünde bir firma seçin.");
  files ??=new();if(files.Count>10 || files.Sum(f=>f.Length)>25_000_000)ModelState.AddModelError("dosyalar","En fazla 10 dosya ve toplam 25 MB yüklenebilir.");
  foreach(var file in files)if(!ValidFile(file))ModelState.AddModelError("dosyalar","Yalnız resim, PDF, Word, Excel ve video dosyaları (dosya başına 20 MB) kabul edilir.");
  if(!ModelState.IsValid){await Lookups();if(edit)x=await Track(b).Include(y=>y.Dosyalar).FirstAsync(y=>y.Id==model.Id);model.Dosyalar=x?.Dosyalar??new List<AtikTakipDosyasi>();return View("TakipForm",model);}
  x ??=new AtikTakibi{BranchId=b,CreatedDate=DateTime.Now};x.TasimaMotAtNo=model.TasimaMotAtNo?.Trim();x.Tarih=model.Tarih.Date;x.AtikId=model.AtikId;x.Miktar=model.Miktar;x.OdemeSatis=model.OdemeSatis;x.BertarafBedeli=model.OdemeSatis.HasValue ? model.BertarafBedeli : 0m;x.NakliyeBedeli=model.NakliyeBedeli;x.IslemeYontemi=model.IslemeYontemi?.Trim();x.HesaplananTutar=(model.OdemeSatis == true ? 1m : model.OdemeSatis == false ? -1m : 0m)*(model.Miktar/1000m)*x.BertarafBedeli-model.NakliyeBedeli;x.TasiyiciFirmaId=model.TasiyiciFirmaId;x.AliciFirmaId=model.AliciFirmaId;x.AliciLisansNo=alici!.LisansNo; if(edit)x.UpdatedDate=DateTime.Now;else _db.AtikTakipleri.Add(x);await _db.SaveChangesAsync();
  var saved=new List<string>();try { foreach(var f in files) { var name=Guid.NewGuid().ToString("N")+Path.GetExtension(f.FileName).ToLowerInvariant();var dir=Path.Combine(_environment.ContentRootPath,"App_Data","AtikDosyalari");Directory.CreateDirectory(dir);var path=Path.Combine(dir,name);await using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true)) await f.CopyToAsync(stream);saved.Add(path);_db.AtikTakipDosyalari.Add(new AtikTakipDosyasi{AtikTakibiId=x.Id,DosyaAdi=Path.GetFileName(f.FileName),IcerikTuru=ContentType(f.FileName),SaklamaAdi=name,Boyut=f.Length});}await _db.SaveChangesAsync(); }catch {foreach(var path in saved)System.IO.File.Delete(path);TempData["ToastrError"]="Kayıt yapıldı fakat dosya yüklenemedi; dosyaları güncelleme ekranından yeniden yükleyin."; }
  return RedirectToAction(nameof(Takip));
 }
 private static readonly Dictionary<string,string> Types=new(StringComparer.OrdinalIgnoreCase){{".jpg","image/jpeg"},{".jpeg","image/jpeg"},{".png","image/png"},{".gif","image/gif"},{".webp","image/webp"},{".pdf","application/pdf"},{".doc","application/msword"},{".docx","application/vnd.openxmlformats-officedocument.wordprocessingml.document"},{".xls","application/vnd.ms-excel"},{".xlsx","application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"},{".mp4","video/mp4"},{".webm","video/webm"}};
 private static bool ValidFile(IFormFile file)=>file.Length>0 && file.Length<=20_000_000 && Types.ContainsKey(Path.GetExtension(file.FileName)) && file.FileName.Length<=255;
 private static string ContentType(string name)=>Types[Path.GetExtension(name)];
 [HttpGet] public async Task<IActionResult> Dosya(int id,bool onizle=false) { var b=await Branch();var f=await _db.AtikTakipDosyalari.AsNoTracking().Include(x=>x.AtikTakibi).FirstOrDefaultAsync(x=>x.Id==id && x.AtikTakibi!=null && x.AtikTakibi.BranchId==b);if(f==null)return NotFound();var path=Path.Combine(_environment.ContentRootPath,"App_Data","AtikDosyalari",f.SaklamaAdi);if(!System.IO.File.Exists(path))return NotFound(); Response.Headers["X-Content-Type-Options"]="nosniff";Response.Headers["Content-Security-Policy"]="default-src 'none'; sandbox";if(onizle && (f.IcerikTuru.StartsWith("image/") || f.IcerikTuru=="application/pdf" || f.IcerikTuru.StartsWith("video/")))return PhysicalFile(path,f.IcerikTuru,enableRangeProcessing:true);return PhysicalFile(path,f.IcerikTuru,f.DosyaAdi,enableRangeProcessing:true); }
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> DosyaSil(int id) {var b=await Branch();var f=await _db.AtikTakipDosyalari.Include(x=>x.AtikTakibi).FirstOrDefaultAsync(x=>x.Id==id && x.AtikTakibi!=null && x.AtikTakibi.BranchId==b);if(f==null)return NotFound();var record=f.AtikTakibiId;_db.Remove(f);await _db.SaveChangesAsync();var path=Path.Combine(_environment.ContentRootPath,"App_Data","AtikDosyalari",f.SaklamaAdi);if(System.IO.File.Exists(path))System.IO.File.Delete(path);return RedirectToAction(nameof(TakipDuzenle),new{id=record});}
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> TakipSil(int id) {var b=await Branch();var x=await Track(b).Include(y=>y.Dosyalar).FirstOrDefaultAsync(y=>y.Id==id);if(x==null)return NotFound();var names=x.Dosyalar.Select(y=>y.SaklamaAdi).ToArray();_db.RemoveRange(x.Dosyalar);_db.Remove(x);await _db.SaveChangesAsync();foreach(var name in names){var path=Path.Combine(_environment.ContentRootPath,"App_Data","AtikDosyalari",name);if(System.IO.File.Exists(path))System.IO.File.Delete(path);}return RedirectToAction(nameof(Takip));}
}
