// What the app needs from player accounts. (Abstraction)

using CodeQuest.Models;

namespace CodeQuest.Data;

public interface IUserRepository
{
    IReadOnlyList<UserAccount> GetAll();

    UserAccount? GetById(int userId);

    UserAccount? GetByUsername(string username);

    int Add(UserAccount user);

    void Update(UserAccount user);

    bool Delete(int userId);
}
