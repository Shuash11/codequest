// What the app needs from picked answers. (Abstraction)

using CodeQuest.Models;

namespace CodeQuest.Data;

public interface ISubmittedAnswerRepository
{
    IReadOnlyList<SubmittedAnswer> GetByAttempt(int attemptId);

    IReadOnlyList<SubmittedAnswer> GetByQuestion(int questionId);

    SubmittedAnswer? GetByAttemptAndQuestion(int attemptId, int questionId);

    int Add(SubmittedAnswer submittedAnswer);

    bool Delete(int attemptId, int questionId);

    void DeleteByAttempt(int attemptId);
}
