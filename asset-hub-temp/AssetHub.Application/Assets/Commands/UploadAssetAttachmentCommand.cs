using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Assets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Assets.Commands;

public record UploadAssetAttachmentCommand(Guid AssetId, string FileName, string ContentType, long SizeBytes, Stream ContentStream) : IRequest<Guid>;

public class UploadAssetAttachmentCommandHandler : IRequestHandler<UploadAssetAttachmentCommand, Guid>
{
    private readonly ITenantDbContext _dbContext;

    public UploadAssetAttachmentCommandHandler(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(UploadAssetAttachmentCommand request, CancellationToken cancellationToken)
    {
        var asset = await _dbContext.Assets.FirstOrDefaultAsync(a => a.Id == request.AssetId, cancellationToken);
        if (asset == null) throw new InvalidOperationException("Asset not found");

        // MVP local file storage
        var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", request.AssetId.ToString());
        if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

        var safeFileName = Guid.NewGuid().ToString("N") + Path.GetExtension(request.FileName);
        var filePath = Path.Combine(uploadDir, safeFileName);
        
        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await request.ContentStream.CopyToAsync(fileStream, cancellationToken);
        }

        var blobUri = $"/uploads/{request.AssetId}/{safeFileName}";
        
        string kind = request.ContentType.StartsWith("image/") ? "photo" : "doc";

        var attachment = new AssetAttachment
        {
            AssetId = request.AssetId,
            FileName = request.FileName,
            ContentType = request.ContentType,
            SizeBytes = request.SizeBytes,
            BlobUri = blobUri,
            Kind = kind
        };

        _dbContext.AssetAttachments.Add(attachment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return attachment.Id;
    }
}
