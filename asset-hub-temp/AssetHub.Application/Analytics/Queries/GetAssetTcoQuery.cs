using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using AssetHub.Domain.Analytics;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AssetHub.Application.Analytics.Queries;

public class AssetTcoDto
{
    public string Scope { get; set; } = "global";
    public Guid? AssetId { get; set; }
    public decimal TotalCost { get; set; }
    public string Currency { get; set; } = "MXN";
    public Dictionary<string, decimal> CostByType { get; set; } = new();
    public int EntryCount { get; set; }
    public decimal? AnnualizedCost { get; set; }
    public int AgeDays { get; set; }
    public string AnnualizationStatus { get; set; } = "not_applicable"; // sufficient_data | projected | insufficient_data | not_applicable
    public string? AnnualizationMessage { get; set; }
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

public class GetAssetTcoQuery : IRequest<AssetTcoDto?>
{
    public Guid? AssetId { get; set; }
    public bool IncludeSubtree { get; set; }
}

/// <summary>
/// TCO = sum of CostEntry.Amount for the asset (or the whole tenant when AssetId is null).
/// Returns null when the requested asset does not belong to the current tenant (SCN-R-002).
/// Annualization: assets younger than 30 days report "insufficient_data";
/// between 30 and 364 days the value is "projected" (365-day extrapolation).
/// Soft-deleted entries are excluded by the global query filter.
/// </summary>
public class GetAssetTcoQueryHandler : IRequestHandler<GetAssetTcoQuery, AssetTcoDto?>
{
    private readonly ITenantDbContext _db;
    private readonly ITenantResolver _tenantResolver;
    private readonly IMemoryCache _cache;

    public GetAssetTcoQueryHandler(ITenantDbContext db, ITenantResolver tenantResolver, IMemoryCache cache)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _cache = cache;
    }

    public async Task<AssetTcoDto?> Handle(GetAssetTcoQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();
        if (tenantId == null)
            throw new InvalidOperationException("Tenant context required");

        var cacheKey = $"tco_{tenantId.Value}_{request.AssetId?.ToString() ?? "global"}_{request.IncludeSubtree}";

        if (_cache.TryGetValue(cacheKey, out AssetTcoDto? cached) && cached != null)
        {
            return cached;
        }

        var result = await ComputeTco(request, cancellationToken);

        if (result != null)
        {
            _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
            });
        }

        return result;
    }

    private async Task<AssetTcoDto?> ComputeTco(GetAssetTcoQuery request, CancellationToken cancellationToken)
    {
        var dto = new AssetTcoDto
        {
            Scope = request.AssetId.HasValue ? "asset" : "global",
            AssetId = request.AssetId
        };

        var query = _db.CostEntries.AsQueryable();

        List<Guid> assetIds;
        DateTime? assetStart = null;

        if (request.AssetId.HasValue)
        {
            assetIds = new List<Guid> { request.AssetId.Value };

            if (request.IncludeSubtree)
            {
                var subtreeIds = await _db.AssetHierarchies
                    .Where(h => h.AncestorId == request.AssetId.Value)
                    .Select(h => h.DescendantId)
                    .ToListAsync(cancellationToken);
                assetIds.AddRange(subtreeIds);
            }

            var asset = await _db.Assets
                .Where(a => a.Id == request.AssetId.Value)
                .Select(a => new { a.CommissionedAt, a.InstalledAt, a.CreatedAt })
                .FirstOrDefaultAsync(cancellationToken);

            if (asset == null)
            {
                // SCN-R-002: cross-tenant or missing asset -> controller maps to 404.
                return null;
            }

            assetStart = asset.CommissionedAt ?? asset.InstalledAt ?? asset.CreatedAt;
            query = query.Where(c => c.AssetId != null && assetIds.Contains(c.AssetId.Value));
        }
        else
        {
            assetIds = new List<Guid>();
        }

        var entries = await query
            .Select(c => new { c.CostType, c.Amount, c.Currency, c.OccurredAt })
            .ToListAsync(cancellationToken);

        dto.EntryCount = entries.Count;
        dto.TotalCost = entries.Sum(e => e.Amount);
        dto.Currency = entries.FirstOrDefault()?.Currency ?? "MXN";
        dto.CostByType = entries
            .GroupBy(e => e.CostType)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

        if (request.AssetId.HasValue)
        {
            var now = DateTime.UtcNow;
            var ageDays = assetStart.HasValue ? (int)Math.Floor((now - assetStart.Value).TotalDays) : 0;
            dto.AgeDays = ageDays;

            if (ageDays <= 0)
            {
                dto.AnnualizationStatus = "insufficient_data";
                dto.AnnualizationMessage = "Datos insuficientes: el activo no registra antigüedad válida.";
            }
            else if (ageDays < 30)
            {
                dto.AnnualizationStatus = "insufficient_data";
                dto.AnnualizationMessage = "Datos insuficientes: el activo tiene menos de 30 días de antigüedad.";
            }
            else if (ageDays < 365)
            {
                dto.AnnualizedCost = Math.Round(dto.TotalCost / ageDays * 365m, 2);
                dto.AnnualizationStatus = "projected";
                dto.AnnualizationMessage = "Valor anualizado proyectado con base en la antigüedad del activo.";
            }
            else
            {
                dto.AnnualizedCost = Math.Round(dto.TotalCost / ageDays * 365m, 2);
                dto.AnnualizationStatus = "sufficient_data";
            }
        }
        else
        {
            dto.AnnualizationStatus = "not_applicable";
        }

        return dto;
    }
}
