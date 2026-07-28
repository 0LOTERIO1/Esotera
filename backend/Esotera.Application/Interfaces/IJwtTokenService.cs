using Esotera.Domain.Entities;

namespace Esotera.Application.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(User user);
}
