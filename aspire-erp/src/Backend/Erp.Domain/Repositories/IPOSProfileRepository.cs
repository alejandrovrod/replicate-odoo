using System;
using System.Threading;
using System.Threading.Tasks;
using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

public interface IPOSProfileRepository
{
    Task<POSProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
