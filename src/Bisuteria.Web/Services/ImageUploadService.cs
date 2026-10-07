using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Bisuteria.Web.Services;

/// <summary>
/// Guarda y optimiza imágenes subidas desde el panel de administración:
/// redimensiona a un máximo de 1200 px y comprime en JPEG (calidad 82).
/// </summary>
public interface IImageUploadService
{
    /// <summary>Guarda una imagen optimizada y devuelve su URL pública.</summary>
    Task<string> SaveOptimizedAsync(IFormFile file, string subfolder, CancellationToken ct = default);

    /// <summary>Descarta solamente archivos creados por esta instancia durante la solicitud.</summary>
    void DiscardNewUploads(IEnumerable<string> urls);

    /// <summary>Guarda varias imágenes; devuelve las URLs generadas.</summary>
    Task<List<string>> SaveManyAsync(IEnumerable<IFormFile> files, string subfolder, CancellationToken ct = default);
}

public class ImageUploadService : IImageUploadService
{
    private const int MaxDimension = 1200;
    private const int JpegQuality = 82;
    private const long MaxFileSize = 8 * 1024 * 1024; // 8 MB

    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png", "image/webp" };

    private readonly Dictionary<string, string> _createdFiles = new(StringComparer.Ordinal);
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ImageUploadService> _logger;

    public ImageUploadService(IWebHostEnvironment env, ILogger<ImageUploadService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<List<string>> SaveManyAsync(IEnumerable<IFormFile> files, string subfolder, CancellationToken ct = default)
    {
        var urls = new List<string>();
        try
        {
            foreach (var file in files)
            {
                if (file is null || file.Length == 0) continue;
                urls.Add(await SaveOptimizedAsync(file, subfolder, ct));
            }
        }
        catch
        {
            DiscardNewUploads(urls);
            throw;
        }
        return urls;
    }

    public void DiscardNewUploads(IEnumerable<string> urls)
    {
        foreach (var url in urls)
        {
            if (!_createdFiles.Remove(url, out var absolutePath)) continue;
            try { File.Delete(absolutePath); }
            catch (IOException ex) { _logger.LogWarning(ex, "No se pudo retirar una imagen nueva no confirmada."); }
            catch (UnauthorizedAccessException ex) { _logger.LogWarning(ex, "No se pudo retirar una imagen nueva no confirmada."); }
        }
    }

    public async Task<string> SaveOptimizedAsync(IFormFile file, string subfolder, CancellationToken ct = default)
    {
        if (subfolder is not ("products" or "gallery"))
            throw new InvalidOperationException("La carpeta de imágenes no es válida.");
        if (file.Length <= 0) throw new InvalidOperationException("La imagen está vacía.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException($"Formato no permitido ({extension}). Usa JPG, PNG o WebP.");

        if (!AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase) && !string.IsNullOrEmpty(file.ContentType))
            throw new InvalidOperationException("El archivo no es una imagen válida.");

        if (file.Length > MaxFileSize)
            throw new InvalidOperationException("La imagen supera el tamaño máximo de 8 MB.");

        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var folderRelative = Path.Combine("img", "uploads", subfolder);
        var folderAbsolute = Path.Combine(webRoot, folderRelative);
        Directory.CreateDirectory(folderAbsolute);

        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid().ToString("N")}.jpg";
        var absolutePath = Path.Combine(folderAbsolute, fileName);

        try
        {
            await using var stream = file.OpenReadStream();
            var info = await Image.IdentifyAsync(stream, ct);
            if ((long)info.Width * info.Height > 24_000_000)
                throw new InvalidOperationException("La imagen supera el límite de 24 millones de píxeles.");
            var format = info.Metadata.DecodedImageFormat?.Name;
            if (format is not ("JPEG" or "PNG" or "WEBP"))
                throw new InvalidOperationException("El contenido debe ser una imagen JPG, PNG o WebP.");
            stream.Position = 0;
            using var image = await Image.LoadAsync(new SixLabors.ImageSharp.Formats.DecoderOptions { MaxFrames = 1, SkipMetadata = true }, stream, ct);

            if (image.Width > MaxDimension || image.Height > MaxDimension)
            {
                var scale = Math.Min((double)MaxDimension / image.Width, (double)MaxDimension / image.Height);
                var newWidth = Math.Max(1, (int)Math.Round(image.Width * scale));
                var newHeight = Math.Max(1, (int)Math.Round(image.Height * scale));
                image.Mutate(x => x.Resize(newWidth, newHeight));
            }

            await using var output = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await image.SaveAsJpegAsync(output, new JpegEncoder { Quality = JpegQuality }, ct);
        }
        catch (InvalidOperationException)
        {
            if (File.Exists(absolutePath)) File.Delete(absolutePath);
            throw;
        }
        catch (Exception ex)
        {
            if (File.Exists(absolutePath)) File.Delete(absolutePath);
            _logger.LogError(ex, "Error al procesar la imagen {FileName}", file.FileName);
            throw new InvalidOperationException("No se pudo procesar la imagen. Verifica que sea un archivo válido.");
        }

        var url = "/" + Path.Combine(folderRelative, fileName).Replace('\\', '/');
        _createdFiles[url] = absolutePath;
        return url;
    }
}
