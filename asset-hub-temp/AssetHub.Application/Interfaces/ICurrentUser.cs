using System;

namespace AssetHub.Application.Interfaces;

public interface ICurrentUser
{
    Guid? Id { get; }
}
