using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IsgCevreYonetim.Domain.Entities;
using IsgCevreYonetim.Infrastructure.Data;
using IsgCevreYonetim.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsgCevreYonetim.Web.Controllers;

[Authorize]
public sealed class MesajPushController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly MessagePushKeys _keys;
    public MesajPushController(ApplicationDbContext db, MessagePushKeys keys) { _db = db; _keys = keys; }

    [HttpGet]
    public IActionResult PublicKey() => Json(new { publicKey = _keys.PublicKey });

    public sealed class SubscribeInput
    {
        public string? Endpoint { get; set; }
        public KeysInput? Keys { get; set; }
    }
    public sealed class KeysInput { public string? P256dh { get; set; } public string? Auth { get; set; } }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeInput input)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var personelId)) return Unauthorized();
        if (!Uri.TryCreate(input.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https" ||
            input.Endpoint!.Length > 2048 || !AllowedPushHost(endpoint.Host) ||
            string.IsNullOrWhiteSpace(input.Keys?.P256dh) || input.Keys.P256dh.Length > 256 ||
            string.IsNullOrWhiteSpace(input.Keys.Auth) || input.Keys.Auth.Length > 256) return BadRequest();
        var endpointHash = Hash(input.Endpoint);
        var row = await _db.MesajPushAbonelikleri.SingleOrDefaultAsync(x => x.EndpointHash == endpointHash);
        if (row == null) { row = new MesajPushAboneligi { Endpoint = input.Endpoint, EndpointHash = endpointHash }; _db.MesajPushAbonelikleri.Add(row); }
        row.PersonelId = personelId;
        row.P256dh = input.Keys.P256dh;
        row.Auth = input.Keys.Auth;
        row.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Unsubscribe([FromBody] SubscribeInput input)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var personelId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(input.Endpoint)) return BadRequest();
        await _db.MesajPushAbonelikleri.Where(x => x.PersonelId == personelId && x.EndpointHash == Hash(input.Endpoint))
            .ExecuteDeleteAsync();
        return NoContent();
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool AllowedPushHost(string host) => new[] {
        "googleapis.com", "mozilla.com", "push.apple.com", "windows.com", "microsoft.com"
    }.Any(domain => host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase));
}
