using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Application.Assets.Queries;

public record GetAssetAttachmentsQuery(Guid AssetId) : IRequest<List<AssetAttachmentDto>>;

public record AssetAttachmentDto(Guid Id, string FileName, string BlobUri, string ContentType, long SizeBytes, string Kind, DateTime CreatedAt);

public class GetAssetAttachmentsQueryHandler : IRequestHandler<GetAssetAttachmentsQuery, List<AssetAttachmentDto>>
{
    private readonly ITenantDbContext _dbContext;

    public GetAssetAttachmentsQueryHandler(ITenantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<AssetAttachmentDto>> Handle(GetAssetAttachmentsQuery request, CancellationToken cancellationToken)
    {
        var attachments = await _dbContext.AssetAttachments
            .Where(a => a.AssetId == request.AssetId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return attachments.Select(a => new AssetAttachmentDto(
            a.Id,
            a.FileName,
            a.BlobUri,
            a.ContentType,
            a.SizeBytes,
            a.Kind,
            a.CreatedAt
        )).ToList();
    }
}
