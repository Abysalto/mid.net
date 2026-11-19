using AbySalto.Mid.Infrastructure.Auth;

namespace AbySalto.Mid.Application.Tests.Auth;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Verify_returns_true_for_the_original_password()
    {
        var hash = _hasher.Hash("Secret123!");

        Assert.True(_hasher.Verify("Secret123!", hash));
    }

    [Fact]
    public void Verify_returns_false_for_a_wrong_password()
    {
        var hash = _hasher.Hash("Secret123!");

        Assert.False(_hasher.Verify("secret123!", hash));
    }

    [Fact]
    public void Hash_uses_a_random_salt()
    {
        Assert.NotEqual(_hasher.Hash("Secret123!"), _hasher.Hash("Secret123!"));
    }

    [Fact]
    public void Same_password_gets_a_different_salt_and_a_different_key_each_time()
    {
        var first = _hasher.Hash("Secret123!").Split('.');
        var second = _hasher.Hash("Secret123!").Split('.');

        Assert.NotEqual(first[2], second[2]);
        Assert.NotEqual(first[3], second[3]);
        Assert.Equal(first[1], second[1]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("v2.210000.AAAA.AAAA")]
    [InlineData("v1.abc.AAAA.AAAA")]
    [InlineData("v1.210000.%%%.AAAA")]
    public void Verify_returns_false_for_malformed_hashes(string hash)
    {
        Assert.False(_hasher.Verify("anything", hash));
    }

    [Theory]
    [InlineData("v1.210000.cSlqgOHzpht35gaeoKGq1g==.nH6tZYLtdSOXQfjOBp30up5ShlkuFSdsuBV1ZZEnYKI=")]
    [InlineData("v1.210000.h+ubt5nUENuWrh+GGfMezA==.S+e4L0ng3X1nrK97Ug1WLie6MV1G0mUQO+wcWpHlbn4=")]
    public void Seeded_demo_hashes_verify_with_the_demo_password(string seededHash)
    {
        Assert.True(_hasher.Verify("Demo123!", seededHash));
    }
}
