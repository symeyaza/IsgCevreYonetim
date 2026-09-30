using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using IsgCevreYonetim.Infrastructure.Data;
using System.Security.Claims;

namespace IsgCevreYonetim.Web.Hubs;

/// <summary>Authenticated clients receive a lightweight event to refresh the online list.</summary>
[Authorize]
public sealed class OnlineUsersHub : Hub
{
    private readonly OnlineConnectionTracker _connections;
    private readonly ApplicationDbContext _db;

    public OnlineUsersHub(OnlineConnectionTracker connections, ApplicationDbContext db)
    {
        _connections = connections;
        _db = db;
    }

    public override async Task OnConnectedAsync()
    {
        var key = Context.User?.FindFirstValue("OnlineSessionKey");
        if (string.IsNullOrEmpty(key)) { Context.Abort(); return; }
        _connections.Add(key, Context.ConnectionId);
        await _db.OnlineKullaniciOturumlari.Where(x => x.SessionKey == key)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastSeenUtc, DateTime.UtcNow));
        await Clients.All.SendAsync("OnlineUsersChanged");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var key = Context.User?.FindFirstValue("OnlineSessionKey");
        if (!string.IsNullOrEmpty(key) && _connections.RemoveLast(key, Context.ConnectionId))
        {
            await _db.OnlineKullaniciOturumlari.Where(x => x.SessionKey == key).ExecuteDeleteAsync();
            await Clients.All.SendAsync("OnlineUsersChanged");
        }
        await base.OnDisconnectedAsync(exception);
    }
}
