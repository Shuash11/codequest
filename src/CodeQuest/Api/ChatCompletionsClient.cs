// =============================================================================
// CodeQuest — Phase P5: AI Provider Chain (wire client)
// Contract: contracts/ai-quiz-api.md — ONE client, parameterized per provider:
//           both tiers (NVIDIA NIM, OpenRouter) accept the same OpenAI
//           chat-completions envelope (contract section 4), so every provider
//           difference is a header or a base URL, never a second client.
// Look at: Complete() below — one HTTP attempt (the chain never retries inside
//          a tier, contract 3.3) that turns every possible outcome into the
//          typed result of the contract's error classes (section 7):
//            200 + finish_reason "stop" + non-empty content  -> success
//            any other 200 shape                             -> bad-output (class 4)
//            401 / 403                                        -> auth-failed (class 2)
//            403 with an OpenRouter budget block             -> quota (class 3)
//            429 (Retry-After recorded, never slept on)      -> quota (class 3)
//            every other status (incl. NVIDIA 202 pending)   -> http-error (class 1)
//            no response in time                             -> timeout (class 5)
//            transport failure (DNS / refused / TLS)         -> timeout (class 5,
//               ruling R-3 a: "network-unreachable" detail — never an exception)
// Secrets: the API key is written ONLY into the Authorization header of the
//          outgoing request. It never appears in a result, a reason string or
//          an exception message (contract 7.8, invariant 3; re-audited P15).
// =============================================================================

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CodeQuest.Api;

/// <summary>
/// The per-provider deltas of the two wire profiles (contract section 5).
/// Everything not listed there is exactly the shared envelope of section 4.
/// </summary>
internal static class ProviderProfiles
{
    /// <summary>The tier identifier of the NVIDIA NIM profile (contract 5.1).</summary>
    public const string Nvidia = "nvidia";

    /// <summary>The tier identifier of the OpenRouter profile (contract 5.2).</summary>
    public const string OpenRouter = "openrouter";

    /// <summary>Static, non-secret OpenRouter attribution: the application name.</summary>
    public const string AppTitle = "CodeQuest";

    /// <summary>Static, non-secret OpenRouter attribution: the application URL.</summary>
    public const string AppUrl = "https://github.com/Shuash11/queziah";

    /// <summary>
    /// Whether the tier receives the optional OpenRouter attribution headers
    /// (contract 5.2 — optional; omitting them changes nothing functionally).
    /// </summary>
    public static bool SendsAttributionHeaders(string tierName) => tierName == OpenRouter;

    /// <summary>
    /// Whether a 403 body is inspected for the OpenRouter budget block
    /// (contract 5.2: 403 with <c>error.metadata.limit_source</c> is a quota
    /// block, not an auth failure; NVIDIA 401/403 is always class 2).
    /// </summary>
    public static bool InspectsBudget403(string tierName) => tierName == OpenRouter;
}

/// <summary>
/// The HTTP client of the AI wire contract. One instance per tier attempt;
/// it builds the request envelope, performs ONE attempt and maps the outcome
/// to the contract's error classes.
/// </summary>
public sealed class ChatCompletionsClient : IDisposable
{
    /// <summary>Sampling temperature sent on every request (contract 4.1).</summary>
    public const double RequestTemperature = 0.2;

    /// <summary>Output token budget sent on every request (contract 4.1).</summary>
    public const int RequestMaxTokens = 4096;

    /// <summary>The per-attempt timeout in seconds (contract 7.5 — a client constant).</summary>
    public const int TimeoutSeconds = 60;

    private static readonly JsonSerializerOptions ResponseOptions = new()
    {
        // The response is read leniently: unknown fields are ignored and the
        // wire names are matched case-insensitively (contract 4.2).
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _endpointUrl;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _tierName;
    private readonly HttpClient _httpClient;
    private readonly HttpMessageHandler? _externalHandler;

    /// <summary>
    /// Creates the client for one provider tier.
    /// </summary>
    /// <param name="tierName">The configured tier identifier ("nvidia", "openrouter"; contract 2).</param>
    /// <param name="baseUrl">The provider base URL including "/v1" (contract 8); the client appends "/chat/completions".</param>
    /// <param name="apiKey">The secret key — used only for the Authorization header.</param>
    /// <param name="model">The configured model slug, sent verbatim (contract 5.3).</param>
    public ChatCompletionsClient(string tierName, string baseUrl, string apiKey, string model)
        : this(tierName, baseUrl, apiKey, model, handler: null, timeout: null)
    {
    }

    /// <summary>
    /// Creates the client with an injected message handler and timeout — the
    /// seam the tests use (a stub handler instead of the network, a short
    /// timeout instead of the 60 seconds of the contract).
    /// </summary>
    public ChatCompletionsClient(
        string tierName,
        string baseUrl,
        string apiKey,
        string model,
        HttpMessageHandler? handler,
        TimeSpan? timeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tierName);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        _tierName = tierName;
        _apiKey = apiKey;
        _model = model;
        _externalHandler = handler;
        _endpointUrl = baseUrl.TrimEnd('/') + "/chat/completions";
        _httpClient = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _httpClient.Timeout = timeout ?? TimeSpan.FromSeconds(TimeoutSeconds);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // A handler supplied by a caller (the tests) stays owned by that caller.
        _httpClient.Dispose();
    }

    /// <summary>
    /// Performs the ONE chat-completions attempt of this tier (contract 3.3):
    /// sends the envelope and returns either the model output or one typed
    /// error class. No exception escapes this method (contract 7.8).
    /// </summary>
    /// <param name="messages">Exactly two messages: the system prompt, then the user prompt.</param>
    public ChatCompletionResult Complete(IReadOnlyList<ChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        ChatCompletionsRequest wireRequest = new()
        {
            Model = _model,
            Messages = [.. messages],
            Temperature = RequestTemperature,
            MaxTokens = RequestMaxTokens,
            Stream = false,
        };

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Post, _endpointUrl);
            request.Content = new StringContent(
                JsonSerializer.Serialize(wireRequest),
                Encoding.UTF8,
                "application/json");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            if (ProviderProfiles.SendsAttributionHeaders(_tierName))
            {
                request.Headers.Add("HTTP-Referer", ProviderProfiles.AppUrl);
                request.Headers.Add("X-Title", ProviderProfiles.AppTitle);
            }

            // The provider seam (IQuizProvider) is synchronous, so this client
            // blocks until the answer arrives. The async send is awaited with
            // ConfigureAwait(false) — the blocking keeps compatibility with any
            // message handler (the sync Send overload requires handler-side
            // sync support, which custom and test handlers do not provide).
            using HttpResponseMessage response = SendSynchronously(request);
            return ReadOutcome(response);
        }
        catch (TaskCanceledException)
        {
            // HttpClient turns an exceeded Timeout into a cancellation —
            // class 5 (contract 7.5).
            return ChatCompletionResult.Failure(
                ChatCompletionErrorClasses.Timeout,
                TimeoutDetail(),
                rawResponseBody: null);
        }
        catch (TimeoutException)
        {
            return ChatCompletionResult.Failure(
                ChatCompletionErrorClasses.Timeout,
                TimeoutDetail(),
                rawResponseBody: null);
        }
        catch (HttpRequestException exception)
        {
            // Transport-level failure (DNS, connection refused/reset, TLS) —
            // the timeout/network-unreachable class with the distinguishing
            // detail in the reason (ruling R-3 a), never an unhandled exception.
            return ChatCompletionResult.Failure(
                ChatCompletionErrorClasses.Timeout,
                "network-unreachable: " + Sanitize(exception.Message),
                rawResponseBody: null);
        }
        catch (Exception exception)
        {
            // Defensive last net: nothing of this client may cross to the UI
            // as an exception (contract 7.8). An unexpected local failure maps
            // to the generic invocation-error class (class 1).
            return ChatCompletionResult.Failure(
                ChatCompletionErrorClasses.HttpError,
                "unexpected: " + Sanitize(exception.GetType().Name),
                rawResponseBody: null);
        }
    }

    /// <summary>
    /// Performs one blocking send of the request. An exceeded timeout surfaces
    /// here as a cancellation (TaskCanceledException / TimeoutException) and a
    /// transport failure as an HttpRequestException — both mapped in Complete.
    /// </summary>
    private HttpResponseMessage SendSynchronously(HttpRequestMessage request)
        => _httpClient
            .SendAsync(request)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

    /// <summary>
    /// Maps one received HTTP response to the contract's outcome (section 7):
    /// the success precondition of 4.2, the OpenRouter budget-403 of 5.2, the
    /// quota-429 of 7.3 and the generic classes around them.
    /// </summary>
    private ChatCompletionResult ReadOutcome(HttpResponseMessage response)
    {
        int status = (int)response.StatusCode;
        string body = ReadBody(response);

        if (status == 200)
        {
            return ParseSuccessBody(body);
        }

        if (status == 401)
        {
            return ChatCompletionResult.Failure(ChatCompletionErrorClasses.AuthFailed, status.ToString(), body);
        }

        if (status == 403)
        {
            if (ProviderProfiles.InspectsBudget403(_tierName)
                && TryGetLimitSource(body, out string? limitSource))
            {
                // A budget/credit block, not a bad key (contract 5.2 / 7.3).
                return ChatCompletionResult.Failure(
                    ChatCompletionErrorClasses.Quota,
                    $"{status};limit_source={limitSource}",
                    body);
            }

            return ChatCompletionResult.Failure(ChatCompletionErrorClasses.AuthFailed, status.ToString(), body);
        }

        if (status == 429)
        {
            return ChatCompletionResult.Failure(
                ChatCompletionErrorClasses.Quota,
                BuildQuotaDetail(status, response),
                body);
        }

        // Every other status — 400, 404, 422, 500, 503, and unexpected 2xx
        // codes such as NVIDIA's 202 "pending" (contract 7.1).
        return ChatCompletionResult.Failure(ChatCompletionErrorClasses.HttpError, status.ToString(), body);
    }

    /// <summary>
    /// Applies the success precondition of contract 4.2 to a 200 body: JSON,
    /// at least one choice, finish_reason exactly "stop", non-empty content.
    /// Every deviation is the malformed-output class (class 4) with a distinct
    /// cause — including zero choices (ruling R-3 c) — so the chain fails over.
    /// </summary>
    private static ChatCompletionResult ParseSuccessBody(string body)
    {
        ChatCompletionsResponse? parsed;

        try
        {
            parsed = JsonSerializer.Deserialize<ChatCompletionsResponse>(body, ResponseOptions);
        }
        catch (JsonException)
        {
            return ChatCompletionResult.Failure(ChatCompletionErrorClasses.BadOutput, "response-not-json", body);
        }

        if (parsed?.Choices is null || parsed.Choices.Count == 0)
        {
            return ChatCompletionResult.Failure(ChatCompletionErrorClasses.BadOutput, "empty-choices", body);
        }

        ChatChoice choice = parsed.Choices[0];

        if (!string.Equals(choice.FinishReason, "stop", StringComparison.Ordinal))
        {
            // A "length" finish means truncated JSON; tool_calls / content_filter /
            // error mean no usable quiz text (contract 4.2 / 7.4).
            return ChatCompletionResult.Failure(
                ChatCompletionErrorClasses.BadOutput,
                $"finish_reason={choice.FinishReason ?? "null"}",
                body);
        }

        if (string.IsNullOrWhiteSpace(choice.Message?.Content))
        {
            return ChatCompletionResult.Failure(ChatCompletionErrorClasses.BadOutput, "empty-content", body);
        }

        return ChatCompletionResult.Success(choice.Message.Content, body);
    }

    /// <summary>
    /// Reads the quota detail of a 429 response: the status, plus the seconds
    /// of a Retry-After header when one is present (contract 7.3 — recorded,
    /// never slept on inside the same user action).
    /// </summary>
    private static string BuildQuotaDetail(int status, HttpResponseMessage response)
    {
        string detail = status.ToString();

        TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta
            ?? (response.Headers.RetryAfter?.Date is DateTimeOffset retryDate
                ? retryDate - DateTimeOffset.UtcNow
                : null);

        if (retryAfter is TimeSpan delay && delay > TimeSpan.Zero)
        {
            detail += $";retry-after={(int)Math.Ceiling(delay.TotalSeconds)}s";
        }

        return detail;
    }

    /// <summary>
    /// Reads the OpenRouter budget marker out of a 403 error body
    /// (contract 5.2: <c>error.metadata.limit_source</c>).
    /// </summary>
    private static bool TryGetLimitSource(string body, out string? limitSource)
    {
        limitSource = null;

        try
        {
            ProviderErrorBody? parsed = JsonSerializer.Deserialize<ProviderErrorBody>(body, ResponseOptions);

            if (parsed?.Error?.Metadata is not Dictionary<string, JsonElement> metadata
                || !metadata.TryGetValue("limit_source", out JsonElement source)
                || source.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            string? value = source.GetString();

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            limitSource = Sanitize(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>The timeout detail of a class-5 reason: the seconds actually waited.</summary>
    private string TimeoutDetail()
        => $"{(int)Math.Ceiling(_httpClient.Timeout.TotalSeconds)}s";

    /// <summary>The body text of a response, defensively — a read failure never throws.</summary>
    private static string ReadBody(HttpResponseMessage response)
    {
        try
        {
            return response
                .Content.ReadAsStringAsync()
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Reduces a diagnostic text to short printable ASCII on one line — the
    /// grammar of the reason strings (contract 2: printable ASCII, no newlines)
    /// and the key-free guarantee of contract 7.8.
    /// </summary>
    internal static string Sanitize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        char[] characters = text
            .Select(character => character >= ' ' && character <= '~' ? character : ' ')
            .ToArray();

        string singleLine = string.Join(" ", new string(characters).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        singleLine = singleLine.Trim();

        return singleLine.Length <= 200 ? singleLine : singleLine[..200];
    }
}
