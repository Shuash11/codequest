// =============================================================================
// CodeQuest — WP4: AI Topic Generation tests (plan A1, FR1)
// The topic-generation twin of AiQuizProviderTests: every case substitutes the
// network with a stub HttpMessageHandler, so the suite stays hermetic (no
// network in CI), and proves ONE rule per case:
//   * a valid (and a code-fenced) model answer maps to the typed draft;
//   * every strict-parser violation names its field ("bad-output:<cause>") —
//     unknown fields, the 60-char name cap, the category enum, the 10..200
//     description window, non-object roots, malformed JSON;
//   * the wire failure classes carry through as typed reasons (quota);
//   * the chain semantics of contract 3 hold for topics too: skip rules with
//     their typed reasons, first success wins, one reason per tried tier in
//     config order, the terminal empty-provider-list reason, and no exception
//     ever crossing to the caller;
//   * the request guard of FR1 (required, trimmed 5..300).
// The stub keys ("test-key-…") are placeholders — no real key material
// appears anywhere in this file (contract 7.8, invariant 3).
// =============================================================================

using System.Net;
using System.Text;
using System.Text.Json;
using CodeQuest.Api;
using CodeQuest.Services;

namespace CodeQuest.Tests;

/// <summary>
/// WP4 tests for the topic chain: one behavior per case, hand-verifiable
/// against the plan's FR1 rules and the carried-over chain contract.
/// </summary>
public class TopicGenerationTests : IDisposable
{
    /// <summary>The temp folder the sibling test classes use for scratch files.</summary>
    private static readonly string TempDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Temp", "opencode");

    private readonly List<string> _tempFiles = new();

    /// <summary>CI runners are fresh machines: the scratch folder self-provisions.</summary>
    public TopicGenerationTests()
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
    // (a) A valid (and a code-fenced) model answer -> the typed draft
    // -------------------------------------------------------------------------
    [Fact]
    public void ValidResponse_ProducesDraftServedByItsTier()
    {
        AiTopicProvider provider = ProviderFor(EnabledConfig("nvidia"), Stub(_ => HttpResponse(200, Envelope(TopicJson()))));

        TopicGenerationOutcome outcome = provider.GenerateTopic(NewRequest());

        Assert.True(outcome.IsSuccess);
        Assert.Equal("nvidia", outcome.ServedByTier);
        Assert.Empty(outcome.ProviderReasons);

        TopicDraft draft = outcome.Draft!;
        Assert.Equal("SQL Injection Basics", draft.Name);
        Assert.Equal("Cybersecurity", draft.Category);
        Assert.Equal("Covers common SQL injection attacks and the parameterized-query defense.", draft.Description);
    }

    [Fact]
    public void CodeFencedResponse_IsStrippedAndParsed()
    {
        // Models wrap JSON despite instructions (contract 6.2.2) — the topic
        // pipeline reuses the quiz pipeline's one fence stripper.
        string fenced = "```json\n" + TopicJson() + "\n```";
        AiTopicProvider provider = ProviderFor(EnabledConfig("nvidia"), Stub(_ => HttpResponse(200, Envelope(fenced))));

        TopicGenerationOutcome outcome = provider.GenerateTopic(NewRequest());

        Assert.True(outcome.IsSuccess);
        Assert.Equal("SQL Injection Basics", outcome.Draft!.Name);
    }

    // -------------------------------------------------------------------------
    // (b) The strict parser: the first violation names its field
    // -------------------------------------------------------------------------
    [Fact]
    public void NameLongerThan60_IsABadOutputFailure()
    {
        AiTopicProvider provider = ProviderFor(EnabledConfig("nvidia"), Stub(_ => HttpResponse(200, Envelope(TopicJson(name: new string('x', 61))))));

        TopicGenerationOutcome outcome = provider.GenerateTopic(NewRequest());

        AssertFailure(outcome, "nvidia:bad-output:name-longer-than-60");
    }

    [Fact]
    public void CategoryOutsideTheTwoValues_IsABadOutputFailure()
    {
        AiTopicProvider provider = ProviderFor(EnabledConfig("nvidia"), Stub(_ => HttpResponse(200, Envelope(TopicJson(category: "General")))));

        TopicGenerationOutcome outcome = provider.GenerateTopic(NewRequest());

        AssertFailure(outcome, "nvidia:bad-output:category-not-allowed");
    }

    [Fact]
    public void DescriptionShorterThan10_IsABadOutputFailure()
    {
        AiTopicProvider provider = ProviderFor(EnabledConfig("nvidia"), Stub(_ => HttpResponse(200, Envelope(TopicJson(description: "short.")))));

        TopicGenerationOutcome outcome = provider.GenerateTopic(NewRequest());

        AssertFailure(outcome, "nvidia:bad-output:description-shorter-than-10");
    }

    [Fact]
    public void DescriptionLongerThan200_IsABadOutputFailure()
    {
        AiTopicProvider provider = ProviderFor(
            EnabledConfig("nvidia"),
            Stub(_ => HttpResponse(200, Envelope(TopicJson(description: new string('d', 201))))));

        TopicGenerationOutcome outcome = provider.GenerateTopic(NewRequest());

        AssertFailure(outcome, "nvidia:bad-output:description-longer-than-200");
    }

    [Fact]
    public void NonObjectRoot_IsABadOutputFailure()
    {
        AiTopicProvider provider = ProviderFor(EnabledConfig("nvidia"), Stub(_ => HttpResponse(200, Envelope("[\"a\", \"b\"]"))));

        TopicGenerationOutcome outcome = provider.GenerateTopic(NewRequest());

        AssertFailure(outcome, "nvidia:bad-output:root-not-object");
    }

    [Fact]
    public void MalformedJson_IsABadOutputFailure()
    {
        AiTopicProvider provider = ProviderFor(EnabledConfig("nvidia"), Stub(_ => HttpResponse(200, Envelope("{ name: not json"))));

        TopicGenerationOutcome outcome = provider.GenerateTopic(NewRequest());

        AssertFailure(outcome, "nvidia:bad-output:json-parse-failed");
    }

    [Fact]
    public void UnknownField_IsRejectedWithItsName()
    {
        // The strictness rule: the schema is exactly the three fields — an
        // extra field from the wire is a malformed output, and the reason
        // names the sanitized field (contract 7.4's "first violation").
        AiTopicProvider provider = ProviderFor(
            EnabledConfig("nvidia"),
            Stub(_ => HttpResponse(200, Envelope(TopicJson(extraField: "\"extra\": 1,")))));

        TopicGenerationOutcome outcome = provider.GenerateTopic(NewRequest());

        AssertFailure(outcome, "nvidia:bad-output:unknown-field-extra");
    }

    [Fact]
    public void MissingField_IsABadOutputFailure()
    {
        AiTopicProvider provider = ProviderFor(EnabledConfig("nvidia"), Stub(_ => HttpResponse(200, Envelope(TopicJson(missingName: true)))));

        TopicGenerationOutcome outcome = provider.GenerateTopic(NewRequest());

        AssertFailure(outcome, "nvidia:bad-output:name-missing-or-empty");
    }

    // -------------------------------------------------------------------------
    // (c) The wire failure classes carry through as typed reasons
    // -------------------------------------------------------------------------
    [Fact]
    public void WireQuotaFailure_BecomesTheTypedQuotaReason()
    {
        AiTopicProvider provider = ProviderFor(EnabledConfig("nvidia"), Stub(_ => HttpResponse(429, "{}")));

        TopicGenerationOutcome outcome = provider.GenerateTopic(NewRequest());

        Assert.False(outcome.IsSuccess);
        // The wire client records the status as the quota detail (contract 7's
        // "<tier>:<class>[:<detail>]" grammar) — the provider carries it through.
        Assert.Equal("nvidia:quota:429", Assert.Single(outcome.ProviderReasons));
    }

    // -------------------------------------------------------------------------
    // (d) The chain semantics (contract 3, carried over for topics)
    // -------------------------------------------------------------------------
    [Fact]
    public void FirstSuccessWins_AndTheWinnerNamesItself()
    {
        AiProviderConfig first = EnabledConfig("tier-a");
        AiProviderConfig second = EnabledConfig("tier-b");

        TopicGenerationService service = new(
            ConfigOf(first, second),
            provider => new AiTopicProvider(
                provider,
                cfg => new ChatCompletionsClient(
                    cfg.Name,
                    cfg.BaseUrl,
                    cfg.ApiKey,
                    cfg.Model,
                    Stub(_ => HttpResponse(
                        200,
                        Envelope(cfg.Name == "tier-a" ? TopicJson(category: "General") : TopicJson()))),
                    timeout: null)));

        TopicGenerationOutcome outcome = service.GenerateTopic(NewRequest());

        Assert.True(outcome.IsSuccess);
        Assert.Equal("tier-b", outcome.ServedByTier);
        Assert.Equal("SQL Injection Basics", outcome.Draft!.Name);
    }

    [Fact]
    public void EveryTierFailing_YieldsOneReasonPerTierInConfigOrder()
    {
        AiProviderConfig first = EnabledConfig("tier-a");
        AiProviderConfig second = EnabledConfig("tier-b");

        TopicGenerationService service = new(
            ConfigOf(first, second),
            provider => new AiTopicProvider(
                provider,
                cfg => new ChatCompletionsClient(
                    cfg.Name,
                    cfg.BaseUrl,
                    cfg.ApiKey,
                    cfg.Model,
                    Stub(_ => HttpResponse(200, Envelope("{ not json"))),
                    timeout: null)));

        TopicGenerationOutcome outcome = service.GenerateTopic(NewRequest());

        Assert.False(outcome.IsSuccess);
        Assert.Null(outcome.Draft);
        Assert.Equal(2, outcome.ProviderReasons.Count);
        Assert.Equal("tier-a:bad-output:json-parse-failed", outcome.ProviderReasons[0]);
        Assert.Equal("tier-b:bad-output:json-parse-failed", outcome.ProviderReasons[1]);
    }

    [Fact]
    public void DisabledEntry_IsSkippedWithItsTypedReason()
    {
        AiProviderConfig disabled = new("tier-a", "https://tier-a.example/v1", "test-key-a", "model-a", enabled: false);
        AiProviderConfig working = EnabledConfig("tier-b");

        TopicGenerationService service = new(
            ConfigOf(disabled, working),
            provider => ProviderFor(provider, Stub(_ => HttpResponse(200, Envelope(TopicJson())))));

        TopicGenerationOutcome outcome = service.GenerateTopic(NewRequest());

        // The skip shows up in the report, and the second tier served the
        // request — but a success drops the collected skip reasons (3.3).
        Assert.True(outcome.IsSuccess);
        Assert.Equal("tier-b", outcome.ServedByTier);
    }

    [Fact]
    public void SkippedEntries_AppearInTheTerminalFailureReport()
    {
        AiProviderConfig disabled = new("tier-a", "https://tier-a.example/v1", "test-key-a", "model-a", enabled: false);
        AiProviderConfig noKey = new("tier-b", "https://tier-b.example/v1", "", "model-b", enabled: true);
        AiProviderConfig broken = new("tier-c", "", "test-key-c", "", enabled: true);

        TopicGenerationService service = new(ConfigOf(disabled, noKey, broken));

        TopicGenerationOutcome outcome = service.GenerateTopic(NewRequest());

        Assert.False(outcome.IsSuccess);
        Assert.Equal(
            new[]
            {
                "tier-a:not-configured:disabled",
                "tier-b:not-configured:api-key-empty",
                "tier-c:not-configured:invalid-config",
            },
            outcome.ProviderReasons.ToArray());
    }

    [Fact]
    public void EmptyProviderList_YieldsTheTerminalReason()
    {
        TopicGenerationService service = new(ConfigOf());

        TopicGenerationOutcome outcome = service.GenerateTopic(NewRequest());

        AssertFailure(outcome, TopicGenerationService.EmptyProviderListReason);
    }

    // -------------------------------------------------------------------------
    // (e) The request guard of FR1 (required, trimmed 5..300)
    // -------------------------------------------------------------------------
    [Fact]
    public void RequestBelowFiveCharacters_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TopicGenerationRequest("abcd"));
    }

    [Fact]
    public void RequestAboveThreeHundredCharacters_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TopicGenerationRequest(new string('d', 301)));
    }

    [Fact]
    public void RequestWhitespaceOnly_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new TopicGenerationRequest("   "));
    }

    [Fact]
    public void Request_IsTrimmedOnce()
    {
        var request = new TopicGenerationRequest("   SQL injection basics   ");

        Assert.Equal("SQL injection basics", request.Description);
    }

    /// <summary>The shared failure assertion: no winner, no draft, the one expected reason.</summary>
    private static void AssertFailure(TopicGenerationOutcome outcome, string expectedReason)
    {
        Assert.False(outcome.IsSuccess);
        Assert.Null(outcome.ServedByTier);
        Assert.Null(outcome.Draft);
        Assert.Equal(new[] { expectedReason }, outcome.ProviderReasons.ToArray());
    }

    // -------------------------------------------------------------------------
    // Stubs and builders (the AiQuizProviderTests pattern, local to this class)
    // -------------------------------------------------------------------------
    private static TopicGenerationRequest NewRequest() => new("Common SQL injection attacks and how to prevent them");

    private static AiProviderConfig EnabledConfig(string name)
        => new(name, $"https://{name}.example/v1", "test-key-" + name, "model-" + name, enabled: true);

    /// <summary>
    /// Writes the provider chain to a scratch config file and reads it through
    /// the REAL loader (the AiQuizProviderTests pattern): the chain tests then
    /// prove the loader's array order is the chain priority, not a test setup.
    /// </summary>
    private AppConfig ConfigOf(params AiProviderConfig[] providers)
    {
        string entries = string.Join(
            ",\n",
            providers.Select(provider =>
                "{ \"name\": \"" + provider.Name
                + "\", \"baseUrl\": \"" + provider.BaseUrl
                + "\", \"apiKey\": \"" + provider.ApiKey
                + "\", \"model\": \"" + provider.Model
                + "\", \"enabled\": " + (provider.Enabled ? "true" : "false") + " }"));
        string json = "{ \"aiProviders\": [" + entries + "] }";

        string path = Path.Combine(TempDirectory, $"codequest-topic-config-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        _tempFiles.Add(path);

        return AppConfig.LoadFromFile(path);
    }

    private static AiTopicProvider ProviderFor(AiProviderConfig config, StubHttpMessageHandler handler)
        => new(config, cfg => new ChatCompletionsClient(
            cfg.Name, cfg.BaseUrl, cfg.ApiKey, cfg.Model, handler, timeout: null));

    private static StubHttpMessageHandler Stub(Func<Uri, HttpResponseMessage> responder) => new(responder);

    private static HttpResponseMessage HttpResponse(int status, string body)
        => new((HttpStatusCode)status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    /// <summary>Wraps model output in the chat-completions response envelope.</summary>
    private static string Envelope(string content, string finishReason = "stop")
        => "{\"choices\": [{\"message\": {\"role\": \"assistant\", \"content\": "
           + JsonSerializer.Serialize(content)
           + "}, \"finish_reason\": \"" + finishReason + "\"}]}";

    private static string TopicJson(
        string? name = null,
        string? category = null,
        string? description = null,
        string? extraField = null,
        bool missingName = false)
        => "{ "
           + (extraField ?? string.Empty)
           + (missingName ? string.Empty : "\"name\": \"" + (name ?? "SQL Injection Basics") + "\", ")
           + "\"category\": \"" + (category ?? "Cybersecurity") + "\", "
           + "\"description\": \"" + (description ?? "Covers common SQL injection attacks and the parameterized-query defense.")
           + "\" }";

    /// <summary>One stubbed wire: the responder decides per request URI.</summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<Uri, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<Uri, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request.RequestUri!));
    }
}
