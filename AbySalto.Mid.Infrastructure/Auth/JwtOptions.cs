using System.ComponentModel.DataAnnotations;

namespace AbySalto.Mid.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Required(ErrorMessage = "Jwt:Key is not set. Set it with dotnet user-secrets (see README.md, Local setup).")]
    [MinLength(32, ErrorMessage = "Jwt:Key must be at least 32 characters. Set it with dotnet user-secrets (see README.md, Local setup).")]
    public string Key { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int ExpiryMinutes { get; set; } = 60;
}
