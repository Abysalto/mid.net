using System.Net.Mail;
using AbySalto.Mid.Application.Auth.Dtos;
using AbySalto.Mid.Application.Common.Exceptions;
using AbySalto.Mid.Application.Common.Interfaces;
using AbySalto.Mid.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AbySalto.Mid.Application.Auth;

internal sealed class AuthService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator tokenGenerator,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IAuthService
{
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;
    public const int MinUsernameLength = 3;
    public const int MaxUsernameLength = 64;

    private static string? s_dummyHash;

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);

        var email = User.NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
            throw new ConflictException("An account with this email already exists.", code: "auth.email_taken");

        var user = new User(email, request.Username, passwordHasher.Hash(request.Password), timeProvider.GetUtcNow().UtcDateTime);
        db.Users.Add(user);

        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex)
        {
            throw new ConflictException("An account with this email already exists.", ex, "auth.email_taken");
        }

        return CreateResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password)
            || request.Password.Length > MaxPasswordLength)
            throw new AuthenticationFailedException();

        var email = User.NormalizeEmail(request.Email);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Verify against a throwaway hash when the email is unknown, so both failures take the same
        // ~PBKDF2 time and response timing doesn't reveal which emails have an account.
        var hash = user?.PasswordHash ?? (s_dummyHash ??= passwordHasher.Hash(Guid.NewGuid().ToString()));
        if (!passwordHasher.Verify(request.Password, hash) || user is null)
            throw new AuthenticationFailedException();

        return CreateResponse(user);
    }

    public async Task<UserDto> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Your account no longer exists. Log in again or register.", "auth.user_not_found");

        return ToDto(user);
    }

    private AuthResponse CreateResponse(User user)
    {
        var token = tokenGenerator.Generate(user);
        return new AuthResponse(token.Token, token.ExpiresAt, ToDto(user));
    }

    private static UserDto ToDto(User user) => new(user.Email, user.Username, user.CreatedAt);

    private static void Validate(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !MailAddress.TryCreate(request.Email.Trim(), out _))
            throw new ValidationException("A valid email address is required.", "auth.invalid_email");

        var username = request.Username?.Trim() ?? string.Empty;
        if (username.Length is < MinUsernameLength or > MaxUsernameLength)
            throw new ValidationException($"Username must be {MinUsernameLength}-{MaxUsernameLength} characters.", "auth.invalid_username",
                new Dictionary<string, object?> { ["minLength"] = MinUsernameLength, ["maxLength"] = MaxUsernameLength });

        var password = request.Password ?? string.Empty;
        if (password.Length is < MinPasswordLength or > MaxPasswordLength
            || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new ValidationException(
                $"Password must be {MinPasswordLength}-{MaxPasswordLength} characters and contain a letter and a digit.",
                "auth.invalid_password",
                new Dictionary<string, object?> { ["minLength"] = MinPasswordLength, ["maxLength"] = MaxPasswordLength });
    }
}
