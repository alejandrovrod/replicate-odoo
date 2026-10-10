using System;
using System.Collections.Generic;
using AssetHub.Domain.Security;

namespace AssetHub.Application.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(ApplicationUser user, IList<string> roles, IList<string> permissions);
    string GenerateRefreshToken();
}
