using ImageMagick;
using Microsoft.AspNetCore.Hosting;

namespace Bisuteria.Web.Services;

public sealed class PaymentProofStorage : IPaymentProofStorage
{
    private const int MaxBytes = 5 * 1024 * 1024;
    private const ulong MaxPixels = 40_000_000;
    private readonly string _directory;

    public PaymentProofStorage(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var configured = configuration["PaymentProofs:StoragePath"];
        _directory = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "PaymentProofs")
            : Path.IsPathRooted(configured) ? configured : Path.Combine(environment.ContentRootPath, configured));
        var webRoot = Path.GetFullPath(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (_directory.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El almacenamiento de comprobantes debe permanecer fuera de wwwroot.");
    }

    public async Task<StoredPaymentProof> SaveAsync(IFormFile file, CancellationToken ct = default)
    {
        if (file is null || file.Length is <= 0 or > MaxBytes)
            throw new Bisuteria.Application.Services.BusinessRuleException("El comprobante debe pesar entre 1 byte y 5 MB.");

        await using var input = new MemoryStream();
        await file.CopyToAsync(input, ct);
        var original = input.ToArray();
        byte[] content;
        string contentType;

        if (IsJpeg(original) || IsPng(original) || IsWebP(original))
        {
            try
            {
                var info = new MagickImageInfo(original);
                if (info.Width == 0 || info.Height == 0 || (ulong)info.Width * info.Height > MaxPixels)
                    throw new InvalidDataException("La imagen supera el límite de píxeles.");
                using var image = new MagickImage(original);
                if (image.Format is not (MagickFormat.Jpeg or MagickFormat.Png or MagickFormat.WebP))
                    throw new InvalidDataException("Formato de imagen no permitido.");
                if (image.Width > 2400 || image.Height > 2400)
                    image.Resize(new MagickGeometry(2400, 2400) { IgnoreAspectRatio = false, Greater = true });
                image.Strip();
                image.Format = MagickFormat.Jpeg;
                image.Quality = 82;
                await using var optimized = new MemoryStream();
                await image.WriteAsync(optimized, ct);
                content = optimized.ToArray();
                contentType = "image/jpeg";
            }
            catch (Exception ex) when (ex is MagickException or InvalidDataException or ArgumentException)
            {
                throw new Bisuteria.Application.Services.BusinessRuleException("La imagen del comprobante está dañada o no es válida.");
            }
        }
        else if (IsSafePdf(original))
        {
            content = original;
            contentType = "application/pdf";
        }
        else
        {
            throw new Bisuteria.Application.Services.BusinessRuleException("Usa una imagen JPG, PNG o WebP válida, o un PDF sin contenido activo.");
        }

        Directory.CreateDirectory(_directory);
        var key = Guid.NewGuid().ToString("N");
        var path = GetPath(key);
        try
        {
            await File.WriteAllBytesAsync(path, content, ct);
            return new StoredPaymentProof(key, contentType, content.LongLength);
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var path = GetPath(storageKey);
        Stream result = File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) : Stream.Null;
        return Task.FromResult<Stream?>(result == Stream.Null ? null : result);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var path = GetPath(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string GetPath(string key)
    {
        if (key.Length != 32 || !Guid.TryParseExact(key, "N", out _)) throw new InvalidOperationException("Clave de comprobante inválida.");
        var path = Path.GetFullPath(Path.Combine(_directory, key + ".bin"));
        if (!path.StartsWith(_directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ruta de comprobante inválida.");
        return path;
    }

    private static bool IsJpeg(ReadOnlySpan<byte> data) => data.Length >= 4 && data[0] == 0xff && data[1] == 0xd8 && data[^2] == 0xff && data[^1] == 0xd9;
    private static bool IsPng(ReadOnlySpan<byte> data) => data.Length >= 8 && data[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    private static bool IsWebP(ReadOnlySpan<byte> data) => data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8);

    private static bool IsSafePdf(byte[] data)
    {
        if (data.Length < 512 || !data.AsSpan(0, Math.Min(data.Length, 16)).StartsWith("%PDF-"u8)) return false;
        var content = System.Text.Encoding.Latin1.GetString(data);
        var tail = content[^Math.Min(content.Length, 1024)..];
        if (!tail.Contains("%%EOF", StringComparison.Ordinal)) return false;
        return !System.Text.RegularExpressions.Regex.IsMatch(content,
            @"/(?:JavaScript|JS|EmbeddedFile|Launch|OpenAction|RichMedia|AA|XFA)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }
}
