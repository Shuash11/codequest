// What the app needs from topics. (Abstraction)

using CodeQuest.Models;

namespace CodeQuest.Data;

public interface ITopicRepository
{
    IReadOnlyList<Topic> GetAll();

    IReadOnlyList<Topic> GetByCategory(string category);

    IReadOnlyList<Topic> Search(string searchText);

    Topic? GetById(int topicId);

    int Add(Topic topic);

    void Update(Topic topic);

    bool Delete(int topicId);

    bool ExistsByName(string name, int? ignoreTopicId = null);
}
