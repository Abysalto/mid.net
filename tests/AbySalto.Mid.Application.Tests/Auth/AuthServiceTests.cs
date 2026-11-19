using AbySalto.Mid.Application.Auth;
using AbySalto.Mid.Application.Auth.Dtos;
using AbySalto.Mid.Application.Common.Exceptions;
using AbySalto.Mid.Application.Common.Interfaces;
using AbySalto.Mid.Domain.Entities;
using AbySalto.Mid.Infrastructure.Auth;
using Microsoft.Extensions.Time.Testing;

namespace AbySalto.Mid.Application.Tests.Auth;

public class AuthServiceTests
{
    private readonly TestDbContext _db = TestDbContext.Create();
    private readonly IJwtTokenGenerator _tokens = Substitute.For<IJwtTokenGenerator>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _tokens.Generate(Arg.Any<User>()).Returns(new AccessToken("token", _time.GetUtcNow().AddHours(1)));
        _sut = new AuthService(_db, new Pbkdf2PasswordHasher(), _tokens, _currentUser, _time);
    }

    [Fact]
    public async Task Register_stores_a_hashed_password_and_returns_a_token()
    {
        var response = await _sut.RegisterAsync(new RegisterRequest("New@Example.com", "newbie", "Passw0rd!"));

        var stored = Assert.Single(_db.Users);
        Assert.Equal("new@example.com", stored.Email);
        Assert.NotEqual("Passw0rd!", stored.PasswordHash);
        Assert.Equal("token", response.AccessToken);
        Assert.Equal(stored.Email, response.User.Email);
    }

    [Fact]
    public async Task Users_with_the_same_password_get_different_hashes_and_both_can_log_in()
    {
        await _sut.RegisterAsync(new RegisterRequest("alice@example.com", "alice", "SamePass1!"));
        await _sut.RegisterAsync(new RegisterRequest("bob@example.com", "bob", "SamePass1!"));

        var hashes = _db.Users.Select(u => u.PasswordHash).ToList();
        Assert.Equal(2, hashes.Distinct().Count());
        Assert.NotEqual(hashes[0].Split('.')[2], hashes[1].Split('.')[2]);

        Assert.Equal("alice@example.com", (await _sut.LoginAsync(new LoginRequest("alice@example.com", "SamePass1!"))).User.Email);
        Assert.Equal("bob@example.com", (await _sut.LoginAsync(new LoginRequest("bob@example.com", "SamePass1!"))).User.Email);
    }

    [Fact]
    public async Task Register_rejects_an_email_that_differs_only_in_case()
    {
        await _sut.RegisterAsync(new RegisterRequest("user@example.com", "first", "Passw0rd!"));

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.RegisterAsync(new RegisterRequest("USER@example.com", "second", "Passw0rd!")));
    }

    [Theory]
    [InlineData("not-an-email", "valid", "Passw0rd!")]
    [InlineData("a@b.com", "ab", "Passw0rd!")]          // username too short
    [InlineData("a@b.com", "valid", "short1")]          // password too short
    [InlineData("a@b.com", "valid", "lettersonly")]     // no digit
    [InlineData("a@b.com", "valid", "1234567890")]      // no letter
    public async Task Register_validates_input(string email, string username, string password)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.RegisterAsync(new RegisterRequest(email, username, password)));
    }

    [Fact]
    public async Task Login_succeeds_with_the_registered_password_regardless_of_email_case()
    {
        await _sut.RegisterAsync(new RegisterRequest("user@example.com", "user", "Passw0rd!"));

        var response = await _sut.LoginAsync(new LoginRequest(" User@Example.com ", "Passw0rd!"));

        Assert.Equal("user@example.com", response.User.Email);
    }

    [Fact]
    public async Task Login_fails_the_same_way_for_wrong_password_and_unknown_email()
    {
        await _sut.RegisterAsync(new RegisterRequest("user@example.com", "user", "Passw0rd!"));

        var wrongPassword = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            _sut.LoginAsync(new LoginRequest("user@example.com", "WrongPass1")));
        var unknownEmail = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            _sut.LoginAsync(new LoginRequest("nobody@example.com", "Passw0rd!")));

        Assert.Equal(wrongPassword.Message, unknownEmail.Message);
    }

    [Fact]
    public async Task GetCurrentUser_returns_the_user_from_the_token()
    {
        await _sut.RegisterAsync(new RegisterRequest("me@example.com", "myself", "Passw0rd!"));
        _currentUser.UserId.Returns(_db.Users.Single().Id);

        var me = await _sut.GetCurrentUserAsync();

        Assert.Equal("me@example.com", me.Email);
    }

    [Fact]
    public async Task GetCurrentUser_without_an_authenticated_user_fails()
    {
        _currentUser.UserId.Returns((Guid?)null);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => _sut.GetCurrentUserAsync());
    }
}
