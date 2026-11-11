// A player's profile. (Encapsulation)

namespace CodeQuest.Models;

public sealed class UserAccount
{
    private int _userId;
    private string _username = string.Empty;
    private string _passwordHash = string.Empty;
    private string _displayName = string.Empty;
    private DateTimeOffset _createdAt;

    public int UserId
    {
        get => _userId;
        set => _userId = value;
    }

    public string Username
    {
        get => _username;
        set => _username = value;
    }

    public string PasswordHash
    {
        get => _passwordHash;
        set => _passwordHash = value;
    }

    public string DisplayName
    {
        get => _displayName;
        set => _displayName = value;
    }

    public DateTimeOffset CreatedAt
    {
        get => _createdAt;
        set => _createdAt = value;
    }
}
