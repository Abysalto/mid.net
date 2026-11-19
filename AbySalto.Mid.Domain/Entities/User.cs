namespace AbySalto.Mid.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string Username { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private User() { }

    public User(string email, string username, string passwordHash, DateTime createdAt)
    {
        Id = Guid.CreateVersion7();
        Email = NormalizeEmail(email);
        Username = username.Trim();
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
