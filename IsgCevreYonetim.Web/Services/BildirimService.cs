using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Infrastructure.Data;
using IsgCevreYonetim.Web.Hubs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace IsgCevreYonetim.Web.Services;

public sealed record BildirimIstek(int PersonelId, string Baslik, string Mesaj, string Url, string Tur, string ReferansAnahtari);

public interface IBildirimService
{
    Task OnayBekliyorAsync(int personelId, string baslik, string mesaj, string url, string tur, string referansAnahtari);
    Task OnayBekliyorTopluAsync(IReadOnlyCollection<BildirimIstek> istekler);
    Task TamamlandiAsync(string referansAnahtari);
    Task ReferansGrubunuSilAsync(string referansOnEki);
    Task GecersizTehlikeliIsBildirimleriniTemizleAsync(int personelId);
}

public class BildirimService : IBildirimService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<BildirimHub> _hub;
    private readonly IMemoryCache _cache;

    public BildirimService(ApplicationDbContext db, IHubContext<BildirimHub> hub, IMemoryCache cache)
    {
        _db = db;
        _hub = hub;
        _cache = cache;
    }

    public async Task OnayBekliyorAsync(int personelId, string baslik, string mesaj, string url, string tur, string referansAnahtari)
    {
        await OnayBekliyorTopluAsync(new[] { new BildirimIstek(personelId, baslik, mesaj, url, tur, referansAnahtari) });
    }

    public async Task OnayBekliyorTopluAsync(IReadOnlyCollection<BildirimIstek> istekler)
    {
        if (istekler.Count == 0) return;
        var tekilIstekler = istekler.GroupBy(x => x.ReferansAnahtari).Select(x => x.Last()).ToList();
        var anahtarlar = tekilIstekler.Select(x => x.ReferansAnahtari).ToList();
        var mevcutlar = await _db.Bildirimler.IgnoreQueryFilters()
            .Where(x => anahtarlar.Contains(x.ReferansAnahtari)).ToListAsync();
        var mevcutByKey = mevcutlar.ToDictionary(x => x.ReferansAnahtari);
        var eskiPersonelIds = new HashSet<int>();
        var yeniPersonelIds = new HashSet<int>();
        var now = DateTime.Now;

        foreach (var istek in tekilIstekler)
        {
            if (!mevcutByKey.TryGetValue(istek.ReferansAnahtari, out var bildirim))
            {
                bildirim = new Bildirim { ReferansAnahtari = istek.ReferansAnahtari, CreatedDate = now };
                _db.Bildirimler.Add(bildirim);
            }
            else if (bildirim.PersonelId != istek.PersonelId)
            {
                eskiPersonelIds.Add(bildirim.PersonelId);
            }

            bildirim.PersonelId = istek.PersonelId;
            bildirim.Baslik = istek.Baslik;
            bildirim.Mesaj = istek.Mesaj;
            bildirim.Url = istek.Url;
            bildirim.Tur = istek.Tur;
            bildirim.OkunduMu = false;
            bildirim.OkunmaTarihi = null;
            bildirim.IsDeleted = false;
            bildirim.IsActive = true;
            bildirim.UpdatedDate = now;
            yeniPersonelIds.Add(istek.PersonelId);
        }
        await _db.SaveChangesAsync();

        foreach (var id in eskiPersonelIds)
            await _hub.Clients.Group(BildirimHub.Grup(id)).SendAsync("BildirimlerDegisti");
        foreach (var id in yeniPersonelIds)
            await _hub.Clients.Group(BildirimHub.Grup(id)).SendAsync("BildirimGeldi");
    }

    public async Task TamamlandiAsync(string referansAnahtari)
    {
        var bildirim = await _db.Bildirimler.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.ReferansAnahtari == referansAnahtari);
        if (bildirim == null) return;
        var personelId = bildirim.PersonelId;
        _db.Bildirimler.Remove(bildirim);
        await _db.SaveChangesAsync();
        await _hub.Clients.Group(BildirimHub.Grup(personelId)).SendAsync("BildirimlerDegisti");
    }

    public async Task ReferansGrubunuSilAsync(string referansOnEki)
    {
        var bildirimler = await _db.Bildirimler.IgnoreQueryFilters()
            .Where(x => x.ReferansAnahtari.StartsWith(referansOnEki)).ToListAsync();
        if (bildirimler.Count == 0) return;
        var personelIds = bildirimler.Select(x => x.PersonelId).Distinct().ToList();
        _db.Bildirimler.RemoveRange(bildirimler);
        await _db.SaveChangesAsync();
        foreach (var personelId in personelIds)
            await _hub.Clients.Group(BildirimHub.Grup(personelId)).SendAsync("BildirimlerDegisti");
    }

    public async Task GecersizTehlikeliIsBildirimleriniTemizleAsync(int personelId)
    {
        // Eski sistem onayı bildirimleri artık geçersiz; diğer bildirim türleri korunur.
        var eskiBildirimler = await _db.Bildirimler.IgnoreQueryFilters()
            .Where(x => x.PersonelId == personelId && x.ReferansAnahtari.StartsWith("tehlikeli-is:"))
            .ToListAsync();
        if (eskiBildirimler.Count == 0) return;
        _db.Bildirimler.RemoveRange(eskiBildirimler);
        await _db.SaveChangesAsync();
        await _hub.Clients.Group(BildirimHub.Grup(personelId)).SendAsync("BildirimlerDegisti");
    }
}
