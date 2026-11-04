// =============================================================================
// CodeQuest — Phase P5: AI Provider Chain (wire DTOs)
// Contract: contracts/ai-quiz-api.md section 4 (shared wire envelope) and
//           section 5 (provider profiles). Both providers (NVIDIA NIM,
//           OpenRouter) accept the same OpenAI chat-completions shape, so one
//           set of DTOs covers both; the client of this folder parameterizes
//           them per provider (base URL, key, model).
// Look at: the JsonPropertyName attributes — the REQUEST and RESPONSE JSON uses
//          the OpenAI snake_case wire names (contract section 2), while the
//          app-side result type ChatCompletionResult uses plain C# names.
// Strictness: the request is serialized exactly as the contract's table pins
//          it (model, messages, temperature, max_tokens, stream — and nothing
//          else, because NVIDIA's request schema rejects unknown fields with
//          HTTP 422, contract 4.1). The response is read leniently: unknown
//          extra fields are ignored, only choices[0].message.content and
//          choices[0].finish_reason are interpreted (contract 4.2/4.3).
// =============================================================================

using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeQuest.Api;

/// <summary>
/// One message of the chat-completions request (contract 4.1): the quiz
/// application always sends exactly two — roles <c>system</c> then <c>user</c>.
/// </summary>
public sealed class ChatMessage
{
    /// <summary>Creates a wire message ("system" or "user" in this application).</summary>
    public ChatMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }

    /// <summary>The wire role (snake_case wire name "role").</summary>
    [JsonPropertyName("role")]
    public string Role { get; }

    /// <summary>The text of the message (wire name "content").</summary>
    [JsonPropertyName("content")]
    public string Content { get; }
}

/// <summary>
/// The request body both providers accept (contract 4.1). The client fills the
/// sampling fields from its fixed constants; the model slug comes from the
/// configuration and is never hard-coded (contract 5.3).
/// </summary>
public sealed class ChatCompletionsRequest
{
    /// <summary>The configured model slug, sent verbatim (wire name "model").</summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    /// <summary>Exactly two messages: system prompt first, then the user prompt.</summary>
    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = [];

    /// <summary>Sampling temperature 0.2 (wire name "temperature"; contract 4.1).</summary>
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    /// <summary>Output token budget 4096 (wire name "max_tokens"; contract 4.1).</summary>
    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }

    /// <summary>Always false — the client parses one JSON body, not a stream.</summary>
    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}

/// <summary>
/// The response envelope both providers return (contract 4.2). Only
/// <c>choices[0]</c> is used; the rest is ignored.
/// </summary>
public sealed class ChatCompletionsResponse
{
    /// <summary>The completions; at least one item is expected (wire name "choices").</summary>
    [JsonPropertyName("choices")]
    public List<ChatChoice>? Choices { get; set; }

    /// <summary>
    /// Token accounting (wire name "usage"). Diagnostic only — never parsed for
    /// quiz data (contract 4.2).
    /// </summary>
    [JsonPropertyName("usage")]
    public ChatResponseUsage? Usage { get; set; }
}

/// <summary>One completion choice of the response (contract 4.2).</summary>
public sealed class ChatChoice
{
    /// <summary>The generated message (wire name "message").</summary>
    [JsonPropertyName("message")]
    public ChatResponseMessage? Message { get; set; }

    /// <summary>
    /// Why the model stopped (wire name "finish_reason"). The client accepts
    /// only "stop" — every other value means no usable quiz text (contract 4.2).
    /// </summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

/// <summary>The generated assistant message (contract 4.2).</summary>
public sealed class ChatResponseMessage
{
    /// <summary>The model output, parsed as the strict quiz JSON (wire name "content").</summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>The role of the responder (wire name "role"); not interpreted.</summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }
}

/// <summary>Token accounting of a response — diagnostic only (contract 4.2).</summary>
public sealed class ChatResponseUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int? PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int? CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int? TotalTokens { get; set; }
}

/// <summary>
/// The documented error body of OpenRouter (contract 5.2):
/// <c>{ "error": { "code": …, "message": …, "metadata": { … } } }</c>.
/// The client reads it only for the budget-403 rule: a 403 whose metadata
/// carries <c>limit_source</c> is a quota block (class 3), not an auth failure.
/// </summary>
public sealed class ProviderErrorBody
{
    [JsonPropertyName("error")]
    public ProviderErrorDetail? Error { get; set; }
}

/// <summary>The nested detail of <see cref="ProviderErrorBody"/>.</summary>
public sealed class ProviderErrorDetail
{
    [JsonPropertyName("code")]
    public long? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, JsonElement>? Metadata { get; set; }
}

/// <summary>
/// The class codes of the contract's six error classes (contract 7.7). The
/// client produces one of these per failed attempt; the sixth class
/// ("not-configured") is a chain-level skip decision made by the service,
/// never by the client.
/// </summary>
public static class ChatCompletionErrorClasses
{
    /// <summary>Class 1 — any non-200 HTTP status that is not auth or quota (contract 7.1).</summary>
    public const string HttpError = "http-error";

    /// <summary>Class 2 — HTTP 401/403 auth failure, except a budget-403 (contract 7.2).</summary>
    public const string AuthFailed = "auth-failed";

    /// <summary>Class 3 — HTTP 429, plus an OpenRouter budget-403 (contract 7.3).</summary>
    public const string Quota = "quota";

    /// <summary>Class 4 — malformed / failed-JSON model output (contract 7.4).</summary>
    public const string BadOutput = "bad-output";

    /// <summary>Class 5 — timeout or unreachable network (contract 7.5, ruling R-3 a).</summary>
    public const string Timeout = "timeout";
}

/// <summary>
/// The typed outcome of one chat-completions attempt: either the model output
/// text, or one error class with a short diagnostic detail — never an exception
/// (contract 7.8, invariant 1). <see cref="RawResponseBody"/> keeps the raw HTTP
/// body for diagnostics (the smoke evidence and later logging); it never
/// contains key material — the key travels only in the request header.
/// </summary>
public sealed class ChatCompletionResult
{
    private ChatCompletionResult(
        bool isSuccess,
        string? content,
        string? errorClass,
        string? errorDetail,
        string? rawResponseBody)
    {
        IsSuccess = isSuccess;
        Content = content;
        ErrorClass = errorClass;
        ErrorDetail = errorDetail;
        RawResponseBody = rawResponseBody;
    }

    /// <summary>True when the attempt produced usable model output.</summary>
    public bool IsSuccess { get; }

    /// <summary>The model output (the strict quiz JSON); null on a failure.</summary>
    public string? Content { get; }

    /// <summary>The contract's error class code (see <see cref="ChatCompletionErrorClasses"/>); null on a success.</summary>
    public string? ErrorClass { get; }

    /// <summary>A short, printable-ASCII, key-free diagnostic detail; null on a success.</summary>
    public string? ErrorDetail { get; }

    /// <summary>The raw HTTP response body, for diagnostics only; null when no body was read.</summary>
    public string? RawResponseBody { get; }

    /// <summary>Builds the success outcome.</summary>
    public static ChatCompletionResult Success(string content, string? rawResponseBody)
        => new(true, content, null, null, rawResponseBody);

    /// <summary>Builds the failure outcome for one error class.</summary>
    public static ChatCompletionResult Failure(string errorClass, string errorDetail, string? rawResponseBody)
        => new(false, null, errorClass, errorDetail, rawResponseBody);
}
