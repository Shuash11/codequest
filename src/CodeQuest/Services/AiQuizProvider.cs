// =============================================================================
// CodeQuest — Phase P5: AI Provider Chain (generation tier)
// Contract: contracts/ai-quiz-api.md — ONE wire tier of the provider chain,
//           bound to ONE configured entry (its "name" is the tier id, contract
//           section 2). The chain itself — order, skips, first success wins —
//           is the QuizGenerationService; this class performs the single
//           attempt of its tier (contract 3.3: one attempt, no in-tier retry).
// Look at: GenerateQuiz() — build the prompt of contract 4.1, send it through
//           the one wire client, parse the answer with the strict pipeline of
//           contract 6 and map it to a draft Quiz. Every failure — HTTP,
//           quota, malformed output, timeout — ends as a TYPED failure reason
//           ("<tier>:<class>:<detail>", contract 2 and 7.7); no exception
//           crosses to the caller (contract 7.8, invariant 1).
// Strictness: the parser of contract 6 accepts nothing else — code fences are
//           stripped (models wrap JSON despite instructions, 6.2.2), the whole
//           remaining text must be one JSON object (6.2.3), every field rule
//           of 6.1 is enforced, and the first violation names its field.
// Draft:    the produced Quiz carries Status = Draft (contract 9) — the model
//           never supplies a status, and the factory of the P3 seam refuses
//           anything else.
// =============================================================================

using System.Text.Json;
using CodeQuest.Api;
using CodeQuest.Models;

namespace CodeQuest.Services;

/// <summary>
/// One AI wire tier of the provider chain. It authors a new quiz by asking the
/// configured model for the strict quiz JSON of contract 6.
/// </summary>
public sealed class AiQuizProvider : IQuizProvider
{
    /// <summary>The wire role of the prompt that pins the output schema (contract 4.1).</summary>
    private const string SystemRole = "system";

    /// <summary>The wire role of the prompt that carries the generation parameters (contract 4.1).</summary>
    private const string UserRole = "user";

    private readonly AiProviderConfig _provider;
    private readonly Func<AiProviderConfig, ChatCompletionsClient> _clientFactory;

    /// <summary>Creates the generation tier for one configured provider entry.</summary>
    /// <param name="provider">The configured entry (name, base URL, key, model, switch).</param>
    public AiQuizProvider(AiProviderConfig provider)
        : this(provider, clientFactory: null)
    {
    }

    /// <summary>
    /// Creates the generation tier with an injected client factory — the seam
    /// the tests use to substitute the network with a stub handler.
    /// </summary>
    public AiQuizProvider(
        AiProviderConfig provider,
        Func<AiProviderConfig, ChatCompletionsClient>? clientFactory)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _provider = provider;
        _clientFactory = clientFactory ?? DefaultClientFactory;
    }

    /// <summary>The tier identifier of this provider — its configured name (contract 2).</summary>
    public string TierId => _provider.Name;

    /// <summary>
    /// Performs this tier's single generation attempt (contract 3.3). On success
    /// the outcome names this tier and carries the draft quiz; on any failure it
    /// carries the one typed reason of this tier.
    /// </summary>
    public QuizGenerationOutcome GenerateQuiz(QuizGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        using ChatCompletionsClient client = _clientFactory(_provider);
        ChatCompletionResult result = client.Complete(BuildMessages(request));

        if (!result.IsSuccess)
        {
            return QuizGenerationOutcome.Failure(
                new[] { FormatReason(result.ErrorClass!, result.ErrorDetail) });
        }

        StrictQuizDraft? draft = StrictQuizJsonParser.Parse(
            result.Content!,
            request.Topic.Name,
            request.Difficulty.ToString(),
            request.QuestionCount,
            out string? failureCause);

        if (draft is null)
        {
            // Class 4 — the model output did not survive the strict pipeline
            // (contract 7.4); the chain moves on to the next tier.
            return QuizGenerationOutcome.Failure(
                new[] { $"{_provider.Name}:{ChatCompletionErrorClasses.BadOutput}:{failureCause}" });
        }

        return QuizGenerationOutcome.Success(_provider.Name, MapToQuiz(draft, request));
    }

    /// <summary>
    /// A generation-only tier serves no stored quizzes — the retrieval path of
    /// the application reports the seeded-database tier (contract 3.4).
    /// </summary>
    public IReadOnlyList<Quiz> GetTakeableQuizzes() => Array.Empty<Quiz>();

    /// <summary>The default client of this tier: the one wire client, parameterized.</summary>
    private static ChatCompletionsClient DefaultClientFactory(AiProviderConfig provider)
        => new(provider.Name, provider.BaseUrl, provider.ApiKey, provider.Model);

    /// <summary>
    /// Builds the two messages of the request envelope (contract 4.1): the
    /// system message that pins the strict quiz JSON, then the user message
    /// with the generation parameters (topic name, topic category, difficulty,
    /// question count).
    /// </summary>
    private IReadOnlyList<ChatMessage> BuildMessages(QuizGenerationRequest request)
    {
        string systemPrompt =
            $"""
            You generate multiple-choice quizzes for a study application.
            Answer with ONLY one JSON object - no markdown code fences, no prose before or after it.
            The JSON object must have exactly these fields:
            "title": a concise quiz title of at most 120 characters.
            "topic": the requested topic name, echoed back exactly.
            "difficulty": the requested difficulty, echoed back exactly ("Easy", "Medium" or "Hard").
            "questions": an array of exactly {request.QuestionCount} question objects, in display order.
            Every question object must have exactly these fields:
            "text": the question text.
            "choices": an array of exactly 4 distinct answer option strings.
            "correctIndex": the 0-based index of the single correct option, one of 0, 1, 2, 3.
            "explanation": one or two sentences on why the correct option is right; never empty.
            Do not add any other field at the root or inside a question.
            """;

        string userPrompt =
            $"Generate a {request.Difficulty} quiz about the topic \"{request.Topic.Name}\" "
            + $"(category: {request.Topic.Category}) with exactly {request.QuestionCount} questions.";

        return new[]
        {
            new ChatMessage(SystemRole, systemPrompt),
            new ChatMessage(UserRole, userPrompt),
        };
    }

    /// <summary>Formats this tier's reason string ("<tier>:<class>[:<detail>]", contract 2).</summary>
    private string FormatReason(string errorClass, string? errorDetail)
        => string.IsNullOrEmpty(errorDetail)
            ? $"{_provider.Name}:{errorClass}"
            : $"{_provider.Name}:{errorClass}:{errorDetail}";

    /// <summary>
    /// Maps the validated draft onto the domain model (contract 6.3): the
    /// echoed topic/difficulty are already verified, the positions are 1-based
    /// array order, and exactly one choice per question carries the answer key.
    /// </summary>
    private static Quiz MapToQuiz(StrictQuizDraft draft, QuizGenerationRequest request)
    {
        Quiz quiz = new()
        {
            TopicId = request.Topic.TopicId,
            Title = draft.Title,
            Difficulty = request.Difficulty,
            Status = QuizStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        for (int questionIndex = 0; questionIndex < draft.Questions.Count; questionIndex++)
        {
            StrictQuizQuestion parsedQuestion = draft.Questions[questionIndex];

            MultipleChoiceQuestion question = new()
            {
                QuestionText = parsedQuestion.Text,
                Explanation = parsedQuestion.Explanation,
                Position = questionIndex + 1,
            };

            for (int choiceIndex = 0; choiceIndex < parsedQuestion.Choices.Count; choiceIndex++)
            {
                question.AddChoice(new Choice
                {
                    ChoiceText = parsedQuestion.Choices[choiceIndex],
                    IsCorrect = choiceIndex == parsedQuestion.CorrectIndex,
                    Position = choiceIndex + 1,
                });
            }

            quiz.AddQuestion(question);
        }

        return quiz;
    }
}

/// <summary>
/// The strict-JSON pipeline of contract 6.2: strip code fences, parse the
/// whole text, enforce every field rule of 6.1. Internal — it exists only for
/// the provider above and is exercised through it in the tests.
/// </summary>
internal static class StrictQuizJsonParser
{
    /// <summary>The longest title the contract allows (contract 6.1).</summary>
    private const int MaxTitleLength = 120;

    /// <summary>How many choices a question must have (contract 6.1).</summary>
    private const int RequiredChoiceCount = 4;

    /// <summary>The highest valid correctIndex (contract 6.1).</summary>
    private const int MaxCorrectIndex = 3;

    /// <summary>
    /// Parses the model output into a validated draft, or reports the first
    /// violated rule. <paramref name="expectedTopicName"/> and
    /// <paramref name="expectedDifficulty"/> are the requested values the
    /// model must echo (contract 6.1).
    /// </summary>
    public static StrictQuizDraft? Parse(
        string content,
        string expectedTopicName,
        string expectedDifficulty,
        int expectedQuestionCount,
        out string? failureCause)
    {
        string json = StripCodeFences(content);

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            // 6.2.3 — the whole text must be one JSON object; no substring
            // salvage, no response healing.
            failureCause = "json-parse-failed";
            return null;
        }

        using (document)
        {
            return Validate(
                document.RootElement,
                expectedTopicName,
                expectedDifficulty,
                expectedQuestionCount,
                out failureCause);
        }
    }

    /// <summary>
    /// Contract 6.2.2 — if the text begins with a markdown code fence, the
    /// opening fence line and a trailing fence line are stripped.
    /// </summary>
    internal static string StripCodeFences(string content)
    {
        string trimmed = content.Trim();

        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        int firstLineBreak = trimmed.IndexOf('\n');

        if (firstLineBreak < 0)
        {
            // "```json {…} ```" on a single line cannot be split by lines; the
            // whole-string parse of the next step decides.
            return trimmed;
        }

        trimmed = trimmed[(firstLineBreak + 1)..].TrimStart();

        if (trimmed.EndsWith("```", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^3].TrimEnd();
        }

        return trimmed;
    }

    /// <summary>Enforces every field rule of contract 6.1; the first violation stops.</summary>
    private static StrictQuizDraft? Validate(
        JsonElement root,
        string expectedTopicName,
        string expectedDifficulty,
        int expectedQuestionCount,
        out string? failureCause)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            failureCause = "root-not-object";
            return null;
        }

        if (!TryReadTrimmedString(root, "title", out string? title))
        {
            failureCause = FieldCause("title");
            return null;
        }

        if (title!.Length > MaxTitleLength)
        {
            failureCause = $"title-longer-than-{MaxTitleLength}";
            return null;
        }

        if (!TryReadTrimmedString(root, "topic", out string? topic))
        {
            failureCause = FieldCause("topic");
            return null;
        }

        if (!string.Equals(topic, expectedTopicName.Trim(), StringComparison.Ordinal))
        {
            // The echo is a generation-consistency check (contract 6.3).
            failureCause = "topic-echo-mismatch";
            return null;
        }

        if (!TryReadTrimmedString(root, "difficulty", out string? difficulty))
        {
            failureCause = FieldCause("difficulty");
            return null;
        }

        if (!string.Equals(difficulty, expectedDifficulty, StringComparison.Ordinal))
        {
            failureCause = "difficulty-echo-mismatch";
            return null;
        }

        if (!root.TryGetProperty("questions", out JsonElement questions)
            || questions.ValueKind != JsonValueKind.Array)
        {
            failureCause = FieldCause("questions");
            return null;
        }

        if (questions.GetArrayLength() != expectedQuestionCount)
        {
            failureCause = $"questions-count={questions.GetArrayLength()}-requested={expectedQuestionCount}";
            return null;
        }

        List<StrictQuizQuestion> parsedQuestions = new(questions.GetArrayLength());

        for (int index = 0; index < questions.GetArrayLength(); index++)
        {
            if (TryValidateQuestion(questions[index], index, out StrictQuizQuestion? question, out failureCause))
            {
                parsedQuestions.Add(question!);
                continue;
            }

            return null;
        }

        failureCause = null;
        return new StrictQuizDraft(title!, parsedQuestions);
    }

    /// <summary>Validates one question object (contract 6.1, the question rules).</summary>
    private static bool TryValidateQuestion(
        JsonElement element,
        int index,
        out StrictQuizQuestion? question,
        out string? failureCause)
    {
        question = null;
        string field = $"questions[{index}]";

        if (element.ValueKind != JsonValueKind.Object)
        {
            failureCause = $"{field}-not-object";
            return false;
        }

        if (!TryReadTrimmedString(element, "text", out string? text))
        {
            failureCause = FieldCause($"{field}.text");
            return false;
        }

        if (!element.TryGetProperty("choices", out JsonElement choices)
            || choices.ValueKind != JsonValueKind.Array)
        {
            failureCause = FieldCause($"{field}.choices");
            return false;
        }

        if (choices.GetArrayLength() != RequiredChoiceCount)
        {
            failureCause = $"{field}.choices-count={choices.GetArrayLength()}";
            return false;
        }

        List<string> parsedChoices = new(RequiredChoiceCount);

        for (int choiceIndex = 0; choiceIndex < RequiredChoiceCount; choiceIndex++)
        {
            JsonElement choice = choices[choiceIndex];

            if (choice.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(choice.GetString()))
            {
                failureCause = FieldCause($"{field}.choices[{choiceIndex}]");
                return false;
            }

            string choiceText = choice.GetString()!.Trim();

            if (parsedChoices.Contains(choiceText, StringComparer.Ordinal))
            {
                // The schema stores choices UNIQUE per question (contract 6.1).
                failureCause = $"{field}.choices[{choiceIndex}]-duplicate";
                return false;
            }

            parsedChoices.Add(choiceText);
        }

        if (!element.TryGetProperty("correctIndex", out JsonElement correctIndex)
            || correctIndex.ValueKind != JsonValueKind.Number
            || !correctIndex.TryGetInt32(out int parsedCorrectIndex))
        {
            failureCause = FieldCause($"{field}.correctIndex");
            return false;
        }

        if (parsedCorrectIndex < 0 || parsedCorrectIndex > MaxCorrectIndex)
        {
            failureCause = $"{field}.correctIndex={parsedCorrectIndex}";
            return false;
        }

        if (!TryReadTrimmedString(element, "explanation", out string? explanation))
        {
            failureCause = FieldCause($"{field}.explanation");
            return false;
        }

        question = new StrictQuizQuestion(text!, parsedChoices, parsedCorrectIndex, explanation!);
        failureCause = null;
        return true;
    }

    /// <summary>Reads one required, trimmed, non-empty string property.</summary>
    private static bool TryReadTrimmedString(JsonElement element, string propertyName, out string? value)
    {
        value = null;

        if (!element.TryGetProperty(propertyName, out JsonElement item)
            || item.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? text = item.GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        value = text.Trim();
        return true;
    }

    /// <summary>The cause text of a missing, mistyped or empty required field.</summary>
    private static string FieldCause(string field) => $"{field}-missing-or-empty";
}

/// <summary>A validated draft quiz produced by the strict parser (contract 6.1).</summary>
internal sealed class StrictQuizDraft
{
    /// <summary>Creates the validated draft.</summary>
    public StrictQuizDraft(string title, List<StrictQuizQuestion> questions)
    {
        Title = title;
        Questions = questions;
    }

    /// <summary>The validated title (trimmed, non-empty, at most 120 characters).</summary>
    public string Title { get; }

    /// <summary>The validated questions, in display order.</summary>
    public List<StrictQuizQuestion> Questions { get; }
}

/// <summary>A validated question of a draft (contract 6.1).</summary>
internal sealed class StrictQuizQuestion
{
    /// <summary>Creates the validated question.</summary>
    public StrictQuizQuestion(string text, List<string> choices, int correctIndex, string explanation)
    {
        Text = text;
        Choices = choices;
        CorrectIndex = correctIndex;
        Explanation = explanation;
    }

    /// <summary>The question text (trimmed, non-empty).</summary>
    public string Text { get; }

    /// <summary>The four distinct choice texts (trimmed, non-empty).</summary>
    public List<string> Choices { get; }

    /// <summary>The 0-based index of the single correct choice (0 to 3).</summary>
    public int CorrectIndex { get; }

    /// <summary>Why the correct choice is right (trimmed, non-empty).</summary>
    public string Explanation { get; }
}
