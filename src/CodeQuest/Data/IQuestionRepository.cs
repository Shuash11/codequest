// What the app needs from questions. (Abstraction)

using CodeQuest.Models;

namespace CodeQuest.Data;

public interface IQuestionRepository
{
    IReadOnlyList<Question> GetByQuiz(int quizId);

    Question? GetById(int questionId);

    int Add(Question question);

    void Update(Question question);

    bool Delete(int questionId);

    void Reorder(int quizId, IReadOnlyList<int> questionIdsInOrder);
}
