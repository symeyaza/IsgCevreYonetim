using System.IO.Compression;
using IsgCevreYonetim.Domain.Entities;

namespace IsgCevreYonetim.Web.Services;

public sealed class MessageAttachmentStorage
{
    private const long MaxDocument = 20L * 1024 * 1024;
    private const long MaxVideo = 25L * 1024 * 1024;
    public const long MaxRequest = 30L * 1024 * 1024;
    private readonly string _directory;
    private static readonly IReadOnlyDictionary<string, string> Types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png",
        [".gif"] = "image/gif", [".webp"] = "image/webp", [".pdf"] = "application/pdf",
        [".doc"] = "application/msword", [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel", [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".mp4"] = "video/mp4", [".mov"] = "video/quicktime", [".webm"] = "video/webm"
    };

    public MessageAttachmentStorage(IWebHostEnvironment environment, IConfiguration config)
    {
        _directory = config["MesajDosyalari:DepoYolu"] is { Length: > 0 } configured
            ? Path.GetFullPath(configured)
            : Path.Combine(environment.ContentRootPath, "App_Data", "MesajEkleri");
    }

    public static string? Validate(IReadOnlyList<IFormFile> files)
    {
        if (files.Count > 5) return "Bir mesajda en fazla 5 dosya gönderilebilir.";
        if (files.Sum(f => f.Length) > 25L * 1024 * 1024) return "Dosyaların toplamı 25 MB sınırını aşamaz.";
        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!Types.ContainsKey(ext)) return "Bu dosya türü desteklenmiyor.";
            if (file.Length <= 0 || file.Length > (ext is ".mp4" or ".mov" or ".webm" ? MaxVideo : MaxDocument))
                return "Dosya boş veya boyut sınırını aşıyor (belge/resim 20 MB, video 25 MB).";
        }
        return null;
    }

    public async Task<MesajEki> SaveAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!Types.TryGetValue(ext, out var mime) || file.Length <= 0 || file.Length >
            (ext is ".mp4" or ".mov" or ".webm" ? MaxVideo : MaxDocument))
            throw new InvalidDataException("Dosya türü veya boyutu geçersiz.");
        await using (var input = file.OpenReadStream())
        {
            var head = new byte[16];
            var count = await input.ReadAsync(head, cancellationToken);
            var valid = ext switch
            {
                ".jpg" or ".jpeg" => count >= 3 && head[0] == 0xff && head[1] == 0xd8 && head[2] == 0xff,
                ".png" => count >= 8 && head.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
                ".gif" => count >= 6 && (head.AsSpan(0, 6).SequenceEqual("GIF87a"u8) || head.AsSpan(0, 6).SequenceEqual("GIF89a"u8)),
                ".webp" => count >= 12 && head.AsSpan(0, 4).SequenceEqual("RIFF"u8) && head.AsSpan(8, 4).SequenceEqual("WEBP"u8),
                ".pdf" => count >= 5 && head.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
                ".doc" or ".xls" => count >= 8 && head.AsSpan(0, 8).SequenceEqual(new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1 }),
                ".docx" or ".xlsx" => count >= 4 && head.AsSpan(0, 4).SequenceEqual(new byte[] { 0x50, 0x4b, 0x03, 0x04 }),
                ".mp4" or ".mov" => count >= 8 && head.AsSpan(4, 4).SequenceEqual("ftyp"u8),
                ".webm" => count >= 4 && head.AsSpan(0, 4).SequenceEqual(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 }),
                _ => false
            };
            if (!valid) throw new InvalidDataException("Dosya içeriği uzantısıyla uyuşmuyor.");
            if (ext is ".docx" or ".xlsx")
            {
                if (!input.CanSeek) throw new InvalidDataException("Office dosyası doğrulanamadı.");
                input.Position = 0;
                try
                {
                    using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
                    var required = ext == ".docx" ? "word/document.xml" : "xl/workbook.xml";
                    if (archive.GetEntry("[Content_Types].xml") == null || archive.GetEntry(required) == null)
                        throw new InvalidDataException("Office dosyası doğrulanamadı.");
                }
                catch (InvalidDataException) { throw; }
                catch (Exception ex) when (ex is IOException or NotSupportedException)
                { throw new InvalidDataException("Office dosyası doğrulanamadı.", ex); }
            }
        }
        Directory.CreateDirectory(_directory);
        var stored = Guid.NewGuid().ToString("N") + ext;
        var path = Path.Combine(_directory, stored);
        try
        {
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            await using var source = file.OpenReadStream();
            var buffer = new byte[81920]; long written = 0; int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                written += read;
                if (written > file.Length || written > (ext is ".mp4" or ".mov" or ".webm" ? MaxVideo : MaxDocument))
                    throw new InvalidDataException("Dosya boyutu sınırı aşıldı.");
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            if (written != file.Length) throw new InvalidDataException("Dosya eksik aktarıldı.");
        }
        catch { Delete(stored); throw; }
        var safeName = Path.GetFileName(file.FileName.Replace('\\', '/')).Trim();
        if (safeName.Length > 180) safeName = safeName[^180..];
        return new MesajEki { DosyaAdi = safeName, DepoAdi = stored, IcerikTuru = mime, Boyut = file.Length };
    }

    public string? Resolve(string stored)
    {
        if (stored != Path.GetFileName(stored) || stored.Contains('/') || stored.Contains('\\')) return null;
        var path = Path.Combine(_directory, stored);
        return System.IO.File.Exists(path) ? path : null;
    }

    public void Delete(string stored)
    {
        var path = Resolve(stored);
        if (path != null) System.IO.File.Delete(path);
    }
}
