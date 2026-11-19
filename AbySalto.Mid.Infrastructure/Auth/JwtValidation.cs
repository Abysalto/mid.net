using Microsoft.IdentityModel.Tokens;

namespace AbySalto.Mid.Infrastructure.Auth;

public static class JwtValidation
{
    public static TokenValidationParameters CreateParameters(JwtOptions options) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = options.Issuer,
        ValidateAudience = true,
        ValidAudience = options.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = JwtTokenGenerator.CreateSigningKey(options.Key),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = "unique_name",
    };
}
