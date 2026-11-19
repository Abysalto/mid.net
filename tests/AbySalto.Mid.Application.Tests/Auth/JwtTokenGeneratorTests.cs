using AbySalto.Mid.Application.Common.Interfaces;
using AbySalto.Mid.Domain.Entities;
using AbySalto.Mid.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;

namespace AbySalto.Mid.Application.Tests.Auth;

public class JwtTokenGeneratorTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        Key = "test-signing-key-that-is-at-least-32-chars",
        ExpiryMinutes = 60,
    };

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    private AccessToken Generate() =>
        new JwtTokenGenerator(Microsoft.Extensions.Options.Options.Create(Options), _time)
            .Generate(new User("demo@abysalto.local", "demo", "hash", _time.GetUtcNow().UtcDateTime));

    [Fact]
    public void Token_expires_after_configured_minutes()
    {
        Assert.Equal(_time.GetUtcNow().AddMinutes(60), Generate().ExpiresAt);
    }

    [Fact]
    public async Task Token_validates_with_the_shared_validation_parameters_and_carries_the_user_claims()
    {
        var token = Generate().Token;
        var parameters = JwtValidation.CreateParameters(Options);
        parameters.ValidateLifetime = false;

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal("demo@abysalto.local", result.Claims[JwtRegisteredClaimNames.Email]);
        Assert.Equal("demo", result.Claims[JwtRegisteredClaimNames.UniqueName]);
        Assert.True(Guid.TryParse(result.Claims[JwtRegisteredClaimNames.Sub].ToString(), out var sub));
        Assert.NotEqual(Guid.Empty, sub);
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var token = Generate().Token;
        var otherKey = new JwtOptions { Issuer = Options.Issuer, Audience = Options.Audience, Key = new string('x', 40) };
        var parameters = JwtValidation.CreateParameters(otherKey);
        parameters.ValidateLifetime = false;

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);

        Assert.False(result.IsValid);
    }
}
