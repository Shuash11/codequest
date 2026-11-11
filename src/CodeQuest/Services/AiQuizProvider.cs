// Asks the AI to make a quiz. (Polymorphism)

using System.Text.Json;
using CodeQuest.Api;
using CodeQuest.Models;

namespace CodeQuest.Services;

public sealed class AiQuizProvider : IQuizProvider
{
    private const string SystemRole = "system";

    private const string UserRole = "user";

    private readonly AiProviderConfig _provider;
    private readonly Func<AiProviderConfig, ChatCompletionsClient> _clientFactory;

    public AiQuizProvider(AiProviderConfig provider)
        : this(provider, clientFactory: null)
    {
    }

    public AiQuizProvider(
        AiProviderConfig provider,
        Func<AiProviderConfig, ChatCompletionsClient>? clientFactory)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _provider = provider;
        _clientFactory = clientFactory ?? DefaultClientFactory;
    }

    public string TierId => _provider.Name;

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
            return QuizGenerationOutcome.Failure(
                new[] { $"{_provider.Name}:{ChatCompletionErrorClasses.BadOutput}:{failureCause}" });
        }

        return QuizGenerationOutcome.Success(_provider.Name, MapToQuiz(draft, request));
    }

    public IReadOnlyList<Quiz> GetTakeableQuizzes() => Array.Empty<Quiz>();

    private static ChatCompletionsClient DefaultClientFactory(AiProviderConfig provider)
        => new(provider.Name, provider.BaseUrl, provider.ApiKey, provider.Model);

    private IReadOnlyList<ChatMessage> BuildMessages(QuizGenerationRequest request)
    {
        string systemPrompt =
            $"""
            You generate multiple-choice quizzes for a study application.
            Answer with ONLY one JSON object - no markdown code fences, no prose before or after it.
            The JSON object must have exactly these fields:
            "title": a concise quiz title of at most 120 characters.
            "topic": the requested topic name, echoed back exactly.
            "difficulty": the requested difficulty, echoed back exactly ("Easy", "Medium", "Hard" or "Insane").
            Insane means expert-level content: trick phrasing, close distractors, and deep language or framework corner cases.
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

    private string FormatReason(string errorClass, string? errorDetail)
        => string.IsNullOrEmpty(errorDetail)
            ? $"{_provider.Name}:{errorClass}"
            : $"{_provider.Name}:{errorClass}:{errorDetail}";

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
                Difficulty = request.Difficulty,
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

internal static class StrictQuizJsonParser
{
    private const int MaxTitleLength = 120;

    private const int RequiredChoiceCount = 4;

    private const int MaxCorrectIndex = 3;

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
            return trimmed;
        }

        trimmed = trimmed[(firstLineBreak + 1)..].TrimStart();

        if (trimmed.EndsWith("```", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^3].TrimEnd();
        }

        return trimmed;
    }

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

    private static string FieldCause(string field) => $"{field}-missing-or-empty";
}

internal sealed class StrictQuizDraft
{
    public StrictQuizDraft(string title, List<StrictQuizQuestion> questions)
    {
        Title = title;
        Questions = questions;
    }

    public string Title { get; }

    public List<StrictQuizQuestion> Questions { get; }
}

internal sealed class StrictQuizQuestion
{
    public StrictQuizQuestion(string text, List<string> choices, int correctIndex, string explanation)
    {
        Text = text;
        Choices = choices;
        CorrectIndex = correctIndex;
        Explanation = explanation;
    }

    public string Text { get; }

    public List<string> Choices { get; }

    public int CorrectIndex { get; }

    public string Explanation { get; }
}
