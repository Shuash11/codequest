// What the app needs from quizzes. (Abstraction)

using CodeQuest.Models;

namespace CodeQuest.Data;

public interface IQuizRepository
{
    IReadOnlyList<Quiz> GetAll();

    IReadOnlyList<Quiz> GetByTopic(int topicId);

    IReadOnlyList<Quiz> GetByStatus(QuizStatus status);

    IReadOnlyList<Quiz> GetByTopicAndStatus(int topicId, QuizStatus status);

    Quiz? GetById(int quizId);

    int Add(Quiz quiz);

    void Update(Quiz quiz);

    bool Delete(int quizId);

    bool ExistsByTitle(int topicId, string title, int? ignoreQuizId = null);
}
