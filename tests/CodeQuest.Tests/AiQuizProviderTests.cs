// =============================================================================
// CodeQuest — Phase P5: AI Provider Chain & Offline Quiz Services
// Tests for: the wire client (Api/ChatCompletionsClient.cs), the generation
// tier (Services/AiQuizProvider.cs), the chain orchestrator
// (Services/QuizGenerationService.cs), the configuration reader
// (Services/AppConfig.cs) and the offline tier (Services/OfflineQuizProvider.cs)
// — all against contracts/ai-quiz-api.md.
// No network: every AI test answers through a stub HttpMessageHandler, so the
// suite runs hermetically in CI. The offline tests run against a FRESH
// temporary SQLite database built from database/schema.sql + database/seed.sql
// (the RepositoryTests pattern of P4).
// Look at: one behavior per case, hand-verifiable against the contract:
//   valid draft, wire envelope, and every error class 1-6 of contract 7
//   (including the R-3 a/c dispositions), the chain semantics of contract 3
//   (order, skips, one attempt, typed terminal failure) and the offline tier
//   rules of contract 3.4. The stub keys ("test-key-…") are placeholders —
//   no real key material appears anywhere in this file.
// =============================================================================

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CodeQuest.Api;
using CodeQuest.Data;
using CodeQuest.Data.Repositories;
using CodeQuest.Models;
using CodeQuest.Services;
using Microsoft.Data.Sqlite;

namespace CodeQuest.Tests;

/// <summary>
/// Phase P5 tests for the AI chain: every case substitutes the network with a
/// stub handler and proves one rule of the wire contract.
/// </summary>
public class AiQuizProviderTests : IDisposable
{
    private const string NvidiaHost = "integrate.api.nvidia.com";
    private const string OpenRouterHost = "openrouter.ai";

    /// <summary>The temp folder the P4 tests already use for scratch files.</summary>
    private static readonly string TempDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Temp", "opencode");

    private readonly List<string> _tempFiles = new();

    /// <summary>
    /// Creates the scratch folder before any test writes into it: CI runners
    /// are fresh machines, so the folder must be self-provisioning — the same
    /// ensure the sibling test classes already perform.
    /// </summary>
    public AiQuizProviderTests()
    {
        Directory.CreateDirectory(TempDirectory);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (string path in _tempFiles)
        {
            File.Delete(path);
        }
    }

    // -------------------------------------------------------------------------
    // (a) Valid response -> draft quiz served by its tier (contract 6.3, 9)
    // -------------------------------------------------------------------------
    [Fact]
    public void ValidResponse_ProducesDraftQuizServedByItsTier()
    {
        StubHttpMessageHandler handler = Stub(_ => HttpResponse(200, Envelope(QuizJson(ThreeQuestions()))));
        AiQuizProvider provider = ProviderFor(NvidiaConfig(), handler);

        QuizGenerationOutcome outcome = provider.GenerateQuiz(NewRequest());

        Assert.True(outcome.IsSuccess);
        Assert.Equal("nvidia", outcome.ServedByTier);
        Assert.Empty(outcome.ProviderReasons);

        Quiz quiz = outcome.QuizDraft!;
        Assert.Equal(QuizStatus.Draft, quiz.Status);
        Assert.Equal("C# Basics Check", quiz.Title);
        Assert.Equal(Difficulty.Easy, quiz.Difficulty);
        Assert.Equal(1, quiz.TopicId);
        Assert.Equal(3, quiz.Questions.Count);
        Assert.Equal(new[] { 1, 2, 3 }, quiz.Questions.Select(question => question.Position).ToArray());

        // Every question: a multiple choice question with exactly four options,
        // exactly one correct option at the echoed index, and an explanation.
        Assert.All(quiz.Questions, question => Assert.IsType<MultipleChoiceQuestion>(question));
        Assert.All(quiz.Questions, question => Assert.False(string.IsNullOrWhiteSpace(question.Explanation)));

        Question first = quiz.Questions[0];
        Assert.Equal(4, first.Choices.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, first.Choices.Select(choice => choice.Position).ToArray());
        Assert.Equal("a1", first.Choices[0].ChoiceText);
        Assert.Equal(1, first.Choices.Count(choice => choice.IsCorrect));
        Assert.True(first.IsCorrectChoice(first.Choices[0]));

        Question second = quiz.Questions[1];
        Assert.Equal(1, second.Choices.Count(choice => choice.IsCorrect));
        Assert.True(second.IsCorrectChoice(second.Choices[1]));

        Question third = quiz.Questions[2];
        Assert.Equal(1, third.Choices.Count(choice => choice.IsCorrect));
        Assert.True(third.IsCorrectChoice(third.Choices[3]));
    }

    // -------------------------------------------------------------------------
    // (a) The wire envelope itself matches contract 4.1: model from config,
    //     temperature 0.2, max_tokens 4096, stream false, system-first messages,
    //     Bearer auth — the key never leaves the Authorization header.
    // -------------------------------------------------------------------------
    [Fact]
    public void WireRequest_MatchesTheContractEnvelope()
    {
        StubHttpMessageHandler handler = Stub(_ => HttpResponse(200, Envelope(QuizJson(ThreeQuestions()))));
        AiQuizProvider provider = ProviderFor(NvidiaConfig(), handler);

        provider.GenerateQuiz(NewRequest());

        CapturedRequest request = Assert.Single(handler.Captured);
        Assert.Equal($"https://{NvidiaHost}/v1/chat/completions", request.Uri.ToString());
        Assert.Equal("Bearer", request.AuthorizationScheme);
        Assert.Equal("test-key-nvidia", request.AuthorizationParameter);

        string body = request.Body;
        Assert.Contains("\"model\":\"meta/llama-3.3-70b-instruct\"", body);
        Assert.Contains("\"temperature\":0.2", body);
        Assert.Contains("\"max_tokens\":4096", body);
        Assert.Contains("\"stream\":false", body);

        // Exactly two messages, system first, then the user prompt (contract 4.1).
        int systemRole = body.IndexOf("\"role\":\"system\"", StringComparison.Ordinal);
        int userRole = body.IndexOf("\"role\":\"user\"", StringComparison.Ordinal);
        Assert.True(systemRole >= 0, "the request must carry a system message");
        Assert.True(userRole > systemRole, "the user message must follow the system message");
        Assert.Contains("exactly 3 question objects", body, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------
    // (b) Malformed JSON -> class 4 (contract 7.4)
    // -------------------------------------------------------------------------
    [Fact]
    public void MalformedJson_IsClass4BadOutput()
    {
        StubHttpMessageHandler handler = Stub(_ => HttpResponse(200, Envelope("this is not json at all")));
        AiQuizProvider provider = ProviderFor(NvidiaConfig(), handler);

        QuizGenerationOutcome outcome = provider.GenerateQuiz(NewRequest());

        AssertFailure(outcome, "nvidia:bad-output:json-parse-failed");
    }

    // -------------------------------------------------------------------------
    // (c) HTTP 500 -> class 1 (contract 7.1)
    // -------------------------------------------------------------------------
    [Fact]
    public void Http500_IsClass1HttpError()
    {
        StubHttpMessageHandler handler = Stub(_ => HttpResponse(500, "{\"error\":\"invocation failed\"}"));
        AiQuizProvider provider = ProviderFor(NvidiaConfig(), handler);

        QuizGenerationOutcome outcome = provider.GenerateQuiz(NewRequest());

        AssertFailure(outcome, "nvidia:http-error:500");
    }

    // -------------------------------------------------------------------------
    // (d) HTTP 401/403 -> class 2 (contract 7.2; R-3/S2: keyed on the status)
    // -------------------------------------------------------------------------
    [Theory]
    [InlineData(401, "nvidia:auth-failed:401")]
    [InlineData(403, "nvidia:auth-failed:403")]
    public void AuthStatuses_AreClass2AuthFailed(int status, string expectedReason)
    {
        StubHttpMessageHandler handler = Stub(_ => HttpResponse(status, "{\"error\":\"bad key\"}"));
        AiQuizProvider provider = ProviderFor(NvidiaConfig(), handler);

        QuizGenerationOutcome outcome = provider.GenerateQuiz(NewRequest());

        AssertFailure(outcome, expectedReason);
    }

    // -------------------------------------------------------------------------
    // (d2) OpenRouter budget-403 -> class 3 quota (contract 5.2 / 7.3)
    // -------------------------------------------------------------------------
    [Fact]
    public void OpenRouterBudget403_IsClass3Quota()
    {
        string budgetBody =
            """
            {"error": {"code": 403, "message": "key limit exceeded", "metadata": {"limit_source": "openrouter_key_limit"}}}
            """;

        StubHttpMessageHandler handler = Stub(_ => HttpResponse(403, budgetBody));
        AiQuizProvider provider = ProviderFor(OpenRouterConfig(), handler);

        QuizGenerationOutcome outcome = provider.GenerateQuiz(NewRequest());

        AssertFailure(outcome, "openrouter:quota:403;limit_source=openrouter_key_limit");
    }

    // -------------------------------------------------------------------------
    // (d3) OpenRouter 403 WITHOUT the budget marker -> class 2 (contract 5.2)
    // -------------------------------------------------------------------------
    [Fact]
    public void OpenRouter403WithoutBudgetMarker_IsClass2AuthFailed()
    {
        StubHttpMessageHandler handler = Stub(_ => HttpResponse(403, "{\"error\": {\"code\": 403, \"message\": \"invalid key\"}}"));
        AiQuizProvider provider = ProviderFor(OpenRouterConfig(), handler);

        QuizGenerationOutcome outcome = provider.GenerateQuiz(NewRequest());

        AssertFailure(outcome, "openrouter:auth-failed:403");
    }

    // -------------------------------------------------------------------------
    // (e) HTTP 429 with Retry-After -> class 3, retry-after recorded, never
    //     slept on inside the same user action (contract 7.3)
    // -------------------------------------------------------------------------
    [Fact]
    public void Quota429WithRetryAfter_IsClass3Quota()
    {
        StubHttpMessageHandler handler = Stub(_ =>
        {
            HttpResponseMessage response = HttpResponse(429, "{\"error\":\"rate limited\"}");
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(12));
            return response;
        });

        AiQuizProvider provider = ProviderFor(NvidiaConfig(), handler);

        QuizGenerationOutcome outcome = provider.GenerateQuiz(NewRequest());

        AssertFailure(outcome, "nvidia:quota:429;retry-after=12s");
    }

    // -------------------------------------------------------------------------
    // (f) Transport failure (DNS / refused / TLS) -> class 5 with the
    //     network-unreachable detail — never an unhandled exception (R-3 a)
    // -------------------------------------------------------------------------
    [Fact]
    public void TransportFailure_MapsToTimeoutClassWithNetworkDetail()
    {
        StubHttpMessageHandler handler = Stub(_ => throw new HttpRequestException("No such host is known."));
        AiQuizProvider provider = ProviderFor(NvidiaConfig(), handler);

        QuizGenerationOutcome outcome = provider.GenerateQuiz(NewRequest());

        Assert.False(outcome.IsSuccess);
        Assert.Null(outcome.ServedByTier);
        Assert.Null(outcome.QuizDraft);
        string reason = Assert.Single(outcome.ProviderReasons);
        Assert.StartsWith("nvidia:timeout:network-unreachable", reason, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------
    // (f2) No response within the timeout -> class 5 (contract 7.5) — proven
    //      with a 50 ms injected timeout instead of the 60 s constant.
    // -------------------------------------------------------------------------
    [Fact]
    public void ElapsedTimeout_MapsToClass5Timeout()
    {
        AiQuizProvider provider = new(
            NvidiaConfig(),
            config => new ChatCompletionsClient(
                config.Name, config.BaseUrl, config.ApiKey, config.Model,
                new WaitingHandler(), TimeSpan.FromMilliseconds(50)));

        QuizGenerationOutcome outcome = provider.GenerateQuiz(NewRequest());

        AssertFailure(outcome, "nvidia:timeout:1s");
    }

    // -------------------------------------------------------------------------
    // (g) HTTP 200 with zero choices -> distinct malformed cause, failover
    //     class (R-3 c, contract 7.4)
    // -------------------------------------------------------------------------
    [Fact]
    public void Http200WithEmptyChoices_IsMalformedBadOutput()
    {
        StubHttpMessageHandler handler = Stub(_ => HttpResponse(200, "{\"choices\": []}"));
        AiQuizProvider provider = ProviderFor(NvidiaConfig(), handler);

        QuizGenerationOutcome outcome = provider.GenerateQuiz(NewRequest());

        AssertFailure(outcome, "nvidia:bad-output:empty-choices");
    }

    // -------------------------------------------------------------------------
    // (l) Code-fenced JSON still parses (contract 6.2.2)
    // -------------------------------------------------------------------------
    [Fact]
    public void CodeFencedJson_StillParses()
    {
        string fenced = "```json\n" + QuizJson(ThreeQuestions()) + "\n```";
        StubHttpMessageHandler handler = Stub(_ => HttpResponse(200, Envelope(fenced)));
        AiQuizProvider provider = ProviderFor(NvidiaConfig(), handler);

        QuizGenerationOutcome outcome = provider.GenerateQuiz(NewRequest());

        Assert.True(outcome.IsSuccess);
        Assert.Equal(3, outcome.QuizDraft!.Questions.Count);
    }

    // -------------------------------------------------------------------------
    // Strict validation of contract 6.1 — each violation names its field
    // -------------------------------------------------------------------------
    [Fact]
    public void StrictParser_RejectsCorrectIndexOutOfRange()
    {
        string[] questions = ThreeQuestions();
        questions[1] = QuestionJson("Q2?", FourChoices("2"), correctIndex: 5, "e2.");

        StubHttpMessageHandler handler = Stub(_ => HttpResponse(200, Envelope(QuizJson(questions))));
        AiQuizProvider provider = ProviderFor(NvidiaConfig(), handler);

        QuizGenerationOutcome outcome = provider.GenerateQuiz(NewRequest());

        AssertFailure(outcome, "nvidia:bad-output:questions[1].correctIndex=5");
    }

    [Fact]
    public void StrictParser_RejectsDuplicateChoices()
    {
        string[] questions = ThreeQuestions();
        questions[0] = QuestionJson("Q1?", new[] { "a1", "a1", "c1", "d1" }, correctIndex: 0, "e1.");

        StubHttpMessageHandler handler = Stub(_ => HttpResponse(200, Envelope(QuizJson(questions))));
        AiQuizProvider provider = ProviderFor(NvidiaConfig(), handler);

        QuizGenerationOutcome outcome = provider.GenerateQuiz(NewRequest());

        AssertFailure(outcome, "nvidia:bad-output:questions[0].choices[1]-duplicate");
    }

    [Fact]
    public void StrictParser_RejectsTopicEchoMismatch()
    {
        string[] questions = ThreeQuestions();

        StubHttpMessageHandler handler = Stub(_ => HttpResponse(200, Envelope(QuizJson(questions, topic: "Python"))));
        AiQuizProvider provider = ProviderFor(NvidiaConfig(), handler);

        QuizGenerationOutcome outcome = provider.GenerateQuiz(NewRequest());

        AssertFailure(outcome, "nvidia:bad-output:topic-echo-mismatch");
    }

    [Fact]
    public void StrictParser_RejectsDifficultyEchoMismatch()
    {
        string[] questions = ThreeQuestions();

        StubHttpMessageHandler handler = Stub(_ => HttpResponse(200, Envelope(QuizJson(questions, difficulty: "Medium"))));
        AiQuizProvider provider = ProviderFor(NvidiaConfig(), handler);

        QuizGenerationOutcome outcome = provider.GenerateQuiz(NewRequest());

        AssertFailure(outcome, "nvidia:bad-output:difficulty-echo-mismatch");
    }

    [Fact]
    public void StrictParser_RejectsQuestionCountMismatch()
    {
        string[] questions = ThreeQuestions()[..2];

        StubHttpMessageHandler handler = Stub(_ => HttpResponse(200, Envelope(QuizJson(questions))));
        AiQuizProvider provider = ProviderFor(NvidiaConfig(), handler);

        QuizGenerationOutcome outcome = provider.GenerateQuiz(NewRequest());

        AssertFailure(outcome, "nvidia:bad-output:questions-count=2-requested=3");
    }

    [Fact]
    public void StrictParser_RejectsChoiceCountMismatch()
    {
        string[] questions = ThreeQuestions();
        questions[0] = QuestionJson("Q1?", new[] { "a1", "b1", "c1" }, correctIndex: 0, "e1.");

        StubHttpMessageHandler handler = Stub(_ => HttpResponse(200, Envelope(QuizJson(questions))));
        AiQuizProvider provider = ProviderFor(NvidiaConfig(), handler);

        QuizGenerationOutcome outcome = provider.GenerateQuiz(NewRequest());

        AssertFailure(outcome, "nvidia:bad-output:questions[0].choices-count=3");
    }

    [Fact]
    public void StrictParser_RejectsTooLongTitle()
    {
        string[] questions = ThreeQuestions();
        string longTitle = new('x', 121);

        StubHttpMessageHandler handler = Stub(_ => HttpResponse(200, Envelope(QuizJson(questions, title: longTitle))));
        AiQuizProvider provider = ProviderFor(NvidiaConfig(), handler);

        QuizGenerationOutcome outcome = provider.GenerateQuiz(NewRequest());

        AssertFailure(outcome, "nvidia:bad-output:title-longer-than-120");
    }

    // -------------------------------------------------------------------------
    // (r) OpenRouter attribution headers — sent for the OpenRouter profile,
    //     never for NVIDIA (contract 5.2)
    // -------------------------------------------------------------------------
    [Fact]
    public void OpenRouterProfile_SendsAttributionHeaders()
    {
        StubHttpMessageHandler handler = Stub(_ => HttpResponse(200, Envelope(QuizJson(ThreeQuestions()))));
        AiQuizProvider openRouter = ProviderFor(OpenRouterConfig(), handler);
        AiQuizProvider nvidia = ProviderFor(NvidiaConfig(), handler);

        openRouter.GenerateQuiz(NewRequest());
        nvidia.GenerateQuiz(NewRequest());

        Assert.Equal(2, handler.Captured.Count);
        Assert.True(handler.Captured[0].HasRefererHeader, "the OpenRouter request carries the attribution headers");
        Assert.True(handler.Captured[0].HasTitleHeader, "the OpenRouter request carries the attribution headers");
        Assert.False(handler.Captured[1].HasRefererHeader, "the NVIDIA request carries no attribution headers");
        Assert.False(handler.Captured[1].HasTitleHeader, "the NVIDIA request carries no attribution headers");
    }

    // -------------------------------------------------------------------------
    // (h) Chain failover: first tier 429 -> second tier serves (contract 3.3)
    // -------------------------------------------------------------------------
    [Fact]
    public void Chain_FailsOverFromQuotaToSecondProvider()
    {
        StubHttpMessageHandler handler = Stub(uri => uri.Host == NvidiaHost
            ? HttpResponse(429, "{\"error\":\"quota\"}")
            : HttpResponse(200, Envelope(QuizJson(ThreeQuestions()))));

        AppConfig config = LoadConfig(TwoProviderConfig);
        QuizGenerationService service = new(config, provider => ProviderFor(provider, handler));

        QuizGenerationOutcome outcome = service.GenerateQuiz(NewTopic(), Difficulty.Easy, 3);

        Assert.True(outcome.IsSuccess);
        Assert.Equal("openrouter", outcome.ServedByTier);
        Assert.Equal(QuizStatus.Draft, outcome.QuizDraft!.Status);
        Assert.Empty(outcome.ProviderReasons);

        // One attempt per tier (contract 3.3 / invariant 2).
        Assert.Equal(1, handler.CallsTo(NvidiaHost));
        Assert.Equal(1, handler.CallsTo(OpenRouterHost));
    }

    // -------------------------------------------------------------------------
    // (i) Empty-key provider is skipped without an HTTP attempt (contract 3.2)
    // -------------------------------------------------------------------------
    [Fact]
    public void Chain_SkipsEmptyKeyProviderWithoutHttpAttempt()
    {
        StubHttpMessageHandler handler = Stub(_ => HttpResponse(200, Envelope(QuizJson(ThreeQuestions()))));

        string configJson =
            """
            {
              "aiProviders": [
                { "name": "nvidia", "baseUrl": "https://integrate.api.nvidia.com/v1", "apiKey": "", "model": "meta/llama-3.3-70b-instruct", "enabled": true },
                { "name": "openrouter", "baseUrl": "https://openrouter.ai/api/v1", "apiKey": "test-key-openrouter", "model": "nvidia/nemotron-3.5-lightning:free", "enabled": true }
              ]
            }
            """;

        AppConfig config = LoadConfig(configJson);
        QuizGenerationService service = new(config, provider => ProviderFor(provider, handler));

        QuizGenerationOutcome outcome = service.GenerateQuiz(NewTopic(), Difficulty.Easy, 3);

        Assert.True(outcome.IsSuccess);
        Assert.Equal("openrouter", outcome.ServedByTier);
        Assert.Equal(0, handler.CallsTo(NvidiaHost));
        Assert.Equal(1, handler.CallsTo(OpenRouterHost));
    }

    // -------------------------------------------------------------------------
    // (i2) Every entry skipped -> typed failure with the skip reasons
    //      (contract 3.2 / 7.6); here: disabled first entry, empty key second.
    // -------------------------------------------------------------------------
    [Fact]
    public void Chain_AllProvidersSkipped_TypedFailureWithSkipReasons()
    {
        StubHttpMessageHandler handler = Stub(_ => throw new InvalidOperationException("no HTTP attempt may happen"));

        string configJson =
            """
            {
              "aiProviders": [
                { "name": "nvidia", "baseUrl": "https://integrate.api.nvidia.com/v1", "apiKey": "test-key-nvidia", "model": "meta/llama-3.3-70b-instruct", "enabled": false },
                { "name": "openrouter", "baseUrl": "https://openrouter.ai/api/v1", "apiKey": "   ", "model": "nvidia/nemotron-3.5-lightning:free", "enabled": true }
              ]
            }
            """;

        AppConfig config = LoadConfig(configJson);
        QuizGenerationService service = new(config, provider => ProviderFor(provider, handler));

        QuizGenerationOutcome outcome = service.GenerateQuiz(NewTopic(), Difficulty.Easy, 3);

        Assert.False(outcome.IsSuccess);
        Assert.Null(outcome.ServedByTier);
        Assert.Null(outcome.QuizDraft);
        Assert.Equal(
            new[] { "nvidia:not-configured:disabled", "openrouter:not-configured:api-key-empty" },
            outcome.ProviderReasons.ToArray());
    }

    // -------------------------------------------------------------------------
    // (i3) An entry without base URL or model is skipped as invalid-config
    //      (contract 3.2, third row)
    // -------------------------------------------------------------------------
    [Fact]
    public void Chain_EntryWithoutModel_IsSkippedAsInvalidConfig()
    {
        StubHttpMessageHandler handler = Stub(_ => throw new InvalidOperationException("no HTTP attempt may happen"));

        string configJson =
            """
            {
              "aiProviders": [
                { "name": "nvidia", "baseUrl": "https://integrate.api.nvidia.com/v1", "apiKey": "test-key-nvidia", "model": "", "enabled": true }
              ]
            }
            """;

        AppConfig config = LoadConfig(configJson);
        QuizGenerationService service = new(config, provider => ProviderFor(provider, handler));

        QuizGenerationOutcome outcome = service.GenerateQuiz(NewTopic(), Difficulty.Easy, 3);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(new[] { "nvidia:not-configured:invalid-config" }, outcome.ProviderReasons.ToArray());
    }

    // -------------------------------------------------------------------------
    // (j) No providers configured -> class-6 terminal typed failure, not an
    //     exception (contract 7.6) — proven with a pre-provider-decision file
    //     layout (an unrelated settings key, no provider list at all).
    // -------------------------------------------------------------------------
    [Fact]
    public void Chain_NoProvidersConfigured_TerminalTypedFailure()
    {
        StubHttpMessageHandler handler = Stub(_ => throw new InvalidOperationException("no HTTP attempt may happen"));

        AppConfig config = LoadConfig("{ \"legacyPlaceholderSetting\": \"unused\" }");
        QuizGenerationService service = new(config, provider => ProviderFor(provider, handler));

        QuizGenerationOutcome outcome = service.GenerateQuiz(NewTopic(), Difficulty.Easy, 3);

        Assert.False(outcome.IsSuccess);
        Assert.Null(outcome.ServedByTier);
        Assert.Null(outcome.QuizDraft);
        Assert.Equal(new[] { QuizGenerationService.EmptyProviderListReason }, outcome.ProviderReasons.ToArray());
        Assert.Empty(handler.Captured);
    }

    // -------------------------------------------------------------------------
    // All tiers fail -> terminal typed failure, one reason per tier in config
    // order (contract 3.3)
    // -------------------------------------------------------------------------
    [Fact]
    public void Chain_AllProvidersFail_TypedFailureWithReasonsInConfigOrder()
    {
        StubHttpMessageHandler handler = Stub(uri => uri.Host == NvidiaHost
            ? HttpResponse(500, "{\"error\":\"down\"}")
            : HttpResponse(401, "{\"error\":\"bad key\"}"));

        AppConfig config = LoadConfig(TwoProviderConfig);
        QuizGenerationService service = new(config, provider => ProviderFor(provider, handler));

        QuizGenerationOutcome outcome = service.GenerateQuiz(NewTopic(), Difficulty.Easy, 3);

        Assert.False(outcome.IsSuccess);
        Assert.Null(outcome.ServedByTier);
        Assert.Equal(
            new[] { "nvidia:http-error:500", "openrouter:auth-failed:401" },
            outcome.ProviderReasons.ToArray());
    }

    // -------------------------------------------------------------------------
    // R-3 b: an absent "enabled" flag defaults to ENABLED (contract 8 / 3.2)
    // -------------------------------------------------------------------------
    [Fact]
    public void Config_AbsentEnabledFlag_DefaultsToEnabled()
    {
        StubHttpMessageHandler handler = Stub(_ => HttpResponse(200, Envelope(QuizJson(ThreeQuestions()))));

        string configJson =
            """
            {
              "aiProviders": [
                { "name": "nvidia", "baseUrl": "https://integrate.api.nvidia.com/v1", "apiKey": "test-key-nvidia", "model": "meta/llama-3.3-70b-instruct" }
              ]
            }
            """;

        AppConfig config = LoadConfig(configJson);
        QuizGenerationService service = new(config, provider => ProviderFor(provider, handler));

        QuizGenerationOutcome outcome = service.GenerateQuiz(NewTopic(), Difficulty.Easy, 3);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(1, handler.CallsTo(NvidiaHost));
    }

    // -------------------------------------------------------------------------
    // Shared builders and stubs of the AI tests
    // -------------------------------------------------------------------------

    /// <summary>The two-provider chain config, nvidia first (contract 3.1).</summary>
    private const string TwoProviderConfig =
        """
        {
          "aiProviders": [
            { "name": "nvidia", "baseUrl": "https://integrate.api.nvidia.com/v1", "apiKey": "test-key-nvidia", "model": "meta/llama-3.3-70b-instruct", "enabled": true },
            { "name": "openrouter", "baseUrl": "https://openrouter.ai/api/v1", "apiKey": "test-key-openrouter", "model": "nvidia/nemotron-3.5-lightning:free", "enabled": true }
          ]
        }
        """;

    private static AiProviderConfig NvidiaConfig() => new(
        "nvidia", $"https://{NvidiaHost}/v1", "test-key-nvidia", "meta/llama-3.3-70b-instruct", enabled: true);

    private static AiProviderConfig OpenRouterConfig() => new(
        "openrouter", $"https://{OpenRouterHost}/api/v1", "test-key-openrouter", "nvidia/nemotron-3.5-lightning:free", enabled: true);

    private static Topic NewTopic() => new()
    {
        TopicId = 1,
        Name = "C#",
        Category = "Programming",
        Description = "C# fundamentals",
        CreatedAt = new DateTimeOffset(2026, 11, 4, 8, 0, 0, TimeSpan.Zero),
    };

    private static QuizGenerationRequest NewRequest() => new(NewTopic(), Difficulty.Easy, 3);

    private static AiQuizProvider ProviderFor(AiProviderConfig config, StubHttpMessageHandler handler)
        => new(config, cfg => new ChatCompletionsClient(
            cfg.Name, cfg.BaseUrl, cfg.ApiKey, cfg.Model, handler, timeout: null));

    private static StubHttpMessageHandler Stub(Func<Uri, HttpResponseMessage> responder) => new(responder);

    private static HttpResponseMessage HttpResponse(int status, string body)
        => new((HttpStatusCode)status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    /// <summary>Wraps model output in the chat-completions response envelope (contract 4.2).</summary>
    private static string Envelope(string content, string finishReason = "stop")
        => "{\"choices\": [{\"message\": {\"role\": \"assistant\", \"content\": "
           + JsonSerializer.Serialize(content)
           + "}, \"finish_reason\": \"" + finishReason + "\"}]}";

    private static string[] ThreeQuestions() => new[]
    {
        QuestionJson("Q1?", FourChoices("1"), correctIndex: 0, "e1."),
        QuestionJson("Q2?", FourChoices("2"), correctIndex: 1, "e2."),
        QuestionJson("Q3?", FourChoices("3"), correctIndex: 3, "e3."),
    };

    private static string[] FourChoices(string suffix) => new[]
    { "a" + suffix, "b" + suffix, "c" + suffix, "d" + suffix };

    private static string QuestionJson(string text, string[] choices, int correctIndex, string explanation)
        => "{ \"text\": \"" + text + "\", \"choices\": ["
           + string.Join(", ", choices.Select(choice => "\"" + choice + "\""))
           + "], \"correctIndex\": " + correctIndex + ", \"explanation\": \"" + explanation + "\" }";

    private static string QuizJson(string[] questions, string topic = "C#", string difficulty = "Easy", string? title = null)
        => "{ \"title\": \"" + (title ?? "C# Basics Check") + "\", \"topic\": \"" + topic
           + "\", \"difficulty\": \"" + difficulty + "\", \"questions\": ["
           + string.Join(", ", questions) + "] }";

    private static void AssertFailure(QuizGenerationOutcome outcome, string expectedReason)
    {
        Assert.False(outcome.IsSuccess);
        Assert.Null(outcome.ServedByTier);
        Assert.Null(outcome.QuizDraft);
        Assert.Equal(new[] { expectedReason }, outcome.ProviderReasons.ToArray());
    }

    /// <summary>Writes a config file to the scratch folder and reads it through the real loader.</summary>
    private AppConfig LoadConfig(string json)
    {
        string path = Path.Combine(TempDirectory, $"codequest-config-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        _tempFiles.Add(path);

        return AppConfig.LoadFromFile(path);
    }

    /// <summary>
    /// The stub of the network: it answers every request through a responder
    /// and captures the outgoing requests (endpoint, auth header, attribution
    /// headers, body) while they are still alive.
    /// </summary>
    internal sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<Uri, HttpResponseMessage> _responder;

        /// <summary>Every request the handler saw, in order.</summary>
        public List<CapturedRequest> Captured { get; } = new();

        public StubHttpMessageHandler(Func<Uri, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public int CallsTo(string host)
            => Captured.Count(request => request.Uri.Host == host);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content is null)
            {
                Captured.Add(Capture(request, string.Empty));
            }
            else
            {
                Captured.Add(Capture(request, request.Content.ReadAsStringAsync().GetAwaiter().GetResult()));
            }

            return Task.FromResult(_responder(request.RequestUri!));
        }

        private static CapturedRequest Capture(HttpRequestMessage request, string body)
        {
            string? authorization = request.Headers.Authorization?.ToString();
            string scheme = string.Empty;
            string parameter = string.Empty;

            if (!string.IsNullOrEmpty(authorization))
            {
                int space = authorization.IndexOf(' ');
                scheme = space < 0 ? authorization : authorization[..space];
                parameter = space < 0 ? string.Empty : authorization[(space + 1)..];
            }

            return new CapturedRequest(
                request.RequestUri!,
                scheme,
                parameter,
                request.Headers.Contains("HTTP-Referer"),
                request.Headers.Contains("X-Title"),
                body);
        }
    }

    /// <summary>
    /// A handler that never answers: it blocks until the client's timeout
    /// cancels the request — the same cancellation a real 60 s wait produces,
    /// compressed to the injected timeout of the test.
    /// </summary>
    internal sealed class WaitingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.WaitHandle.WaitOne();

            return Task.FromCanceled<HttpResponseMessage>(cancellationToken);
        }
    }

    /// <summary>One outgoing request captured by the stub, for the wire assertions.</summary>
    internal sealed record CapturedRequest(
        Uri Uri,
        string AuthorizationScheme,
        string AuthorizationParameter,
        bool HasRefererHeader,
        bool HasTitleHeader,
        string Body);
}

/// <summary>
/// Phase P5 tests for the offline tier (contract 3.4): the approved quizzes of
/// the seeded database are the takeable ones, and the tier never authors a
/// quiz. Every case runs against its own fresh copy of the seeded demo
/// database (schema.sql + seed.sql), like the P4 repository tests.
/// </summary>
public class OfflineQuizProviderTests : IDisposable
{
    private readonly string _databasePath;
    private readonly Db _db;
    private readonly QuizRepository _quizzes;
    private readonly OfflineQuizProvider _offline;

    public OfflineQuizProviderTests()
    {
        string scratchDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Temp", "opencode");
        Directory.CreateDirectory(scratchDirectory);

        _databasePath = Path.Combine(scratchDirectory, $"codequest-offline-tests-{Guid.NewGuid():N}.db");
        _db = new Db(_databasePath);

        ExecuteScript(ReadScript("schema.sql"));
        ExecuteScript(ReadScript("seed.sql"));

        _quizzes = new QuizRepository(_db);
        _offline = new OfflineQuizProvider(_quizzes);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        File.Delete(_databasePath);
        File.Delete(_databasePath + "-journal");
        File.Delete(_databasePath + "-wal");
        File.Delete(_databasePath + "-shm");
    }

    // -------------------------------------------------------------------------
    // (k) Retrieval only: the seeded APPROVED quizzes, and nothing else
    //     (contract 3.4 — the drafts 4 and 5 of seed.sql are not takeable)
    // -------------------------------------------------------------------------
    [Fact]
    public void GetTakeableQuizzes_ReturnsTheSeededApprovedQuizzesOnly()
    {
        IReadOnlyList<Quiz> takeable = _offline.GetTakeableQuizzes();

        Assert.Equal(new[] { 1, 2, 3 }, takeable.Select(quiz => quiz.QuizId).ToArray());
        Assert.All(takeable, quiz => Assert.Equal(QuizStatus.Approved, quiz.Status));
        Assert.Equal(OfflineQuizProvider.OfflineTierId, _offline.TierId);
    }

    // -------------------------------------------------------------------------
    // (k) The offline tier never authors a quiz — the typed refusal (3.4.1)
    // -------------------------------------------------------------------------
    [Fact]
    public void GenerateQuiz_IsAlwaysTheTypedRefusal()
    {
        QuizGenerationOutcome outcome = _offline.GenerateQuiz(
            new QuizGenerationRequest(NewTopic(), Difficulty.Medium, 3));

        Assert.False(outcome.IsSuccess);
        Assert.Null(outcome.ServedByTier);
        Assert.Null(outcome.QuizDraft);
        Assert.Equal(new[] { OfflineQuizProvider.GenerationRefusalReason }, outcome.ProviderReasons.ToArray());
        Assert.StartsWith("offline:", OfflineQuizProvider.GenerationRefusalReason, StringComparison.Ordinal);
    }

    private static Topic NewTopic() => new()
    {
        TopicId = 1,
        Name = "C#",
        Category = "Programming",
        Description = "C# fundamentals",
        CreatedAt = new DateTimeOffset(2026, 11, 4, 8, 0, 0, TimeSpan.Zero),
    };

    /// <summary>Executes one SQL script as one batch through a connection of the data layer.</summary>
    private void ExecuteScript(string script)
    {
        using SqliteConnection connection = _db.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = script;
        command.ExecuteNonQuery();
    }

    /// <summary>Reads one SQL script of the database folder (read-only).</summary>
    private static string ReadScript(string fileName)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "database", fileName);

            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"The SQL script '{fileName}' was not found in a 'database' folder above the test directory.");
    }
}
