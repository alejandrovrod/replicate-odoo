using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;

namespace AssetHub.Application.Tenants.Commands;

public record UploadTenantMediaCommand(string FileName, string ContentType, long SizeBytes, Stream ContentStream) : IRequest<string>;

public class UploadTenantMediaCommandHandler : IRequestHandler<UploadTenantMediaCommand, string>
{
    private readonly ITenantResolver _tenantResolver;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg"
    };

    private const long MaxSizeBytes = 5 * 1024 * 1024; // 5 MB

    public UploadTenantMediaCommandHandler(ITenantResolver tenantResolver)
    {
        _tenantResolver = tenantResolver;
    }

    public async Task<string> Handle(UploadTenantMediaCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId()
            ?? throw new UnauthorizedAccessException("Tenant is required.");

        ValidateFileName(request.FileName);
        ValidateExtension(request.FileName);
        ValidateSize(request.SizeBytes);

        // MVP local file storage
        var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "tenants", tenantId.ToString());
        if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

        var safeFileName = Guid.NewGuid().ToString("N") + Path.GetExtension(request.FileName);
        var filePath = Path.Combine(uploadDir, safeFileName);

        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await request.ContentStream.CopyToAsync(fileStream, cancellationToken);
        }

        var blobUri = $"/uploads/tenants/{tenantId}/{safeFileName}";

        return blobUri;
    }

    private static void ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("El nombre del archivo es obligatorio.");
        }

        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("El nombre del archivo contiene caracteres no permitidos.");
        }
    }

    private static void ValidateExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName);

        if (!AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException(
                $"Extensión no permitida '{extension}'. Solo se aceptan imágenes: {string.Join(", ", AllowedExtensions.OrderBy(e => e))}.");
        }
    }

    private static void ValidateSize(long sizeBytes)
    {
        if (sizeBytes <= 0)
        {
            throw new ArgumentException("El archivo está vacío.");
        }

        if (sizeBytes > MaxSizeBytes)
        {
            throw new ArgumentException(
                $"El archivo supera el tamaño máximo de {MaxSizeBytes / (1024 * 1024)} MB.");
        }
    }
}
