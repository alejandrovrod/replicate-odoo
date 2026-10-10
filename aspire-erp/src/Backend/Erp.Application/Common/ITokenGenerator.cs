using Erp.Domain.Entities.Security;

namespace Erp.Application.Common;

public interface ITokenGenerator
{
    string GenerateToken(User user, IEnumerable<DocTypePermission> permissions, IEnumerable<string>? roles = null);
}
