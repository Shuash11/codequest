// The AI topic contract. (Abstraction)

namespace CodeQuest.Services;

public interface ITopicGenerationService
{
    TopicGenerationOutcome GenerateTopic(TopicGenerationRequest request);
}
