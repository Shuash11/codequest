// What the app needs from quiz attempts. (Abstraction)

using CodeQuest.Models;

namespace CodeQuest.Data;

public interface IAttemptRepository
{
    IReadOnlyList<QuizAttempt> GetByUser(int userId);

    IReadOnlyList<QuizAttempt> GetByQuiz(int quizId);

    IReadOnlyList<QuizAttempt> GetByUserAndQuiz(int userId, int quizId);

    QuizAttempt? GetById(int attemptId);

    int Add(QuizAttempt attempt);

    void Update(QuizAttempt attempt);

    bool Delete(int attemptId);
}
