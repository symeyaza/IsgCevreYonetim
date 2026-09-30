namespace IsgCevreYonetim.Domain.Entities;

public sealed class MesajPushAboneligi
{
    public int Id { get; set; }
    public int PersonelId { get; set; }
    public string Endpoint { get; set; } = "";
    public string EndpointHash { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";
    public DateTime UpdatedUtc { get; set; }
}
