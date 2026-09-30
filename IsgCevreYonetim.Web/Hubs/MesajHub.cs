using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace IsgCevreYonetim.Web.Hubs;

[Authorize]
public sealed class MesajHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (int.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            await Groups.AddToGroupAsync(Context.ConnectionId, Grup(id));
        else Context.Abort();
        await base.OnConnectedAsync();
    }

    public static string Grup(int personelId) => $"mesaj:{personelId}";
}
