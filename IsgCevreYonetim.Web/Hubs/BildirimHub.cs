using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace IsgCevreYonetim.Web.Hubs;

[Authorize]
public class BildirimHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var personelId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(personelId))
            await Groups.AddToGroupAsync(Context.ConnectionId, Grup(personelId));
        await base.OnConnectedAsync();
    }

    public static string Grup(int personelId) => Grup(personelId.ToString());
    private static string Grup(string personelId) => $"personel:{personelId}";
}
