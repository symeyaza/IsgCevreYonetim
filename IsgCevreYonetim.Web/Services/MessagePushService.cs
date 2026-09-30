using System.Text.Json;
using System.Threading.Channels;
using IsgCevreYonetim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace IsgCevreYonetim.Web.Services;

public sealed class MessagePushKeys
{
    public string PublicKey { get; }
    public string PrivateKey { get; }
    public string Subject { get; }

    public MessagePushKeys(IWebHostEnvironment env, IConfiguration config)
    {
        Subject = config["ISG_PUSH_SUBJECT"] ?? "https://isgpaneli.com.tr";
        var publicKey = config["ISG_PUSH_PUBLIC_KEY"];
        var privateKey = config["ISG_PUSH_PRIVATE_KEY"];
        if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(privateKey))
        {
            var path = Path.Combine(env.ContentRootPath, "App_Data", "MessagePushKeys.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (!File.Exists(path))
            {
                var generated = VapidHelper.GenerateVapidKeys();
                try
                {
                    using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    JsonSerializer.Serialize(stream, new Keys(generated.PublicKey, generated.PrivateKey));
                }
                catch (IOException) when (File.Exists(path)) { }
            }
            var saved = JsonSerializer.Deserialize<Keys>(File.ReadAllText(path))
                ?? throw new InvalidOperationException("Web Push anahtarları okunamadı.");
            publicKey = saved.PublicKey;
            privateKey = saved.PrivateKey;
        }
        PublicKey = publicKey;
        PrivateKey = privateKey;
    }
    private sealed record Keys(string PublicKey, string PrivateKey);
}

public sealed record MessagePushItem(int PersonelId, string Title, string Url, string Tag);
public sealed class MessagePushQueue
{
    private readonly Channel<MessagePushItem> _channel = Channel.CreateUnbounded<MessagePushItem>(
        new UnboundedChannelOptions { SingleReader = true });
    public void Enqueue(MessagePushItem item) => _channel.Writer.TryWrite(item);
    public IAsyncEnumerable<MessagePushItem> ReadAllAsync(CancellationToken token) =>
        _channel.Reader.ReadAllAsync(token);
}

public sealed class MessagePushWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MessagePushQueue _queue;
    private readonly MessagePushKeys _keys;
    private readonly ILogger<MessagePushWorker> _logger;
    public MessagePushWorker(IServiceScopeFactory scopeFactory, MessagePushQueue queue,
        MessagePushKeys keys, ILogger<MessagePushWorker> logger)
    { _scopeFactory = scopeFactory; _queue = queue; _keys = keys; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var item in _queue.ReadAllAsync(stoppingToken))
            {
                try { await DeliverAsync(item, stoppingToken); }
                catch (Exception ex) { _logger.LogWarning(ex, "Mesaj push bildirimi gönderilemedi."); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task DeliverAsync(MessagePushItem item, CancellationToken token)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var subscriptions = await db.MesajPushAbonelikleri.AsNoTracking()
            .Where(x => x.PersonelId == item.PersonelId).ToListAsync(token);
        var details = new VapidDetails(_keys.Subject, _keys.PublicKey, _keys.PrivateKey);
        var payload = JsonSerializer.Serialize(new { title = item.Title, body = "Yeni mesajınız var", url = item.Url, tag = item.Tag });
        foreach (var sub in subscriptions)
        {
            try
            {
                var client = new WebPushClient();
                var options = new WebPushOptions { VapidDetails = details, ContentEncoding = ContentEncoding.Aes128gcm, Urgency = Urgency.High };
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                await client.SendNotificationAsync(new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth), payload, options, timeout.Token);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Push aboneliğine gönderim başarısız: {Id}", sub.Id); }
        }
    }
}
