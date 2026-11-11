// What the app needs from the random-mix builder. (Abstraction)

namespace CodeQuest.Services;

public interface IRandomMixService
{
    /// <summary>The smallest number of drawable questions a mix needs.</summary>
    int MinimumQuestions { get; }

    /// <summary>How many questions the approved quizzes can offer a mix.</summary>
    int CountDrawableQuestions();

    /// <summary>
    /// Builds one fresh mix quiz (random questions from every approved quiz,
    /// shuffled answers) and returns its id, ready to take.
    /// </summary>
    int CreateMix();
}
