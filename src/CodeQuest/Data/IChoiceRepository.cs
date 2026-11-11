// What the app needs from answer options. (Abstraction)

using CodeQuest.Models;

namespace CodeQuest.Data;

public interface IChoiceRepository
{
    IReadOnlyList<Choice> GetByQuestion(int questionId);

    Choice? GetById(int choiceId);

    int Add(Choice choice);

    void Update(Choice choice);

    bool Delete(int choiceId);
}
