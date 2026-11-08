// =============================================================================
// CodeQuest — WP4: AI Topic Generation (service contract)
// The topic-generation seam, behind an interface like every other service of
// the application (plan §5: services all behind interfaces). The consumer —
// the New-topic dialog — never learns which tier answered, and never sees an
// exception: the outcome is the typed result of the whole chain.
// Look at: GenerateTopic — one call, one walk of the configured providers
//          (contract 3 semantics copied from the quiz chain): skips first,
//          then one attempt per tier in config order, first success wins.
// =============================================================================

namespace CodeQuest.Services;

/// <summary>
/// Creates topics with the AI provider chain. Implementations return the
/// typed outcome of the whole chain — a success served by the first tier that
/// produced a valid topic, or a failure with one reason per tier that was
/// tried. No exception is thrown to the caller.
/// </summary>
public interface ITopicGenerationService
{
    /// <summary>
    /// Runs the chain for one topic generation request. The result is always a
    /// typed outcome — never an exception (contract 7.8).
    /// </summary>
    /// <param name="request">The validated description of the topic to create.</param>
    TopicGenerationOutcome GenerateTopic(TopicGenerationRequest request);
}
