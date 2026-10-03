using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>In-memory <see cref="ICompanyRepository"/>: one configured company, or null.</summary>
public sealed class FakeCompanyRepository : ICompanyRepository
{
    public Company? Company { get; set; }

    public Task<Company?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default)
        => Task.FromResult(Company);
}

