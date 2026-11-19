using AbySalto.Mid.Domain.Entities;

namespace AbySalto.Mid.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    AccessToken Generate(User user);
}

public record AccessToken(string Token, DateTimeOffset ExpiresAt);
