// The data sent to and received from the AI.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeQuest.Api;

public sealed class ChatMessage
{
    public ChatMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }

    [JsonPropertyName("role")]
    public string Role { get; }

    [JsonPropertyName("content")]
    public string Content { get; }
}

public sealed class ChatCompletionsRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = [];

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}

public sealed class ChatCompletionsResponse
{
    [JsonPropertyName("choices")]
    public List<ChatChoice>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public ChatResponseUsage? Usage { get; set; }
}

public sealed class ChatChoice
{
    [JsonPropertyName("message")]
    public ChatResponseMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public sealed class ChatResponseMessage
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }
}

public sealed class ChatResponseUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int? PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int? CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int? TotalTokens { get; set; }
}

public sealed class ProviderErrorBody
{
    [JsonPropertyName("error")]
    public ProviderErrorDetail? Error { get; set; }
}

public sealed class ProviderErrorDetail
{
    [JsonPropertyName("code")]
    public long? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, JsonElement>? Metadata { get; set; }
}

public static class ChatCompletionErrorClasses
{
    public const string HttpError = "http-error";

    public const string AuthFailed = "auth-failed";

    public const string Quota = "quota";

    public const string BadOutput = "bad-output";

    public const string Timeout = "timeout";
}

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

    public bool IsSuccess { get; }

    public string? Content { get; }

    public string? ErrorClass { get; }

    public string? ErrorDetail { get; }

    public string? RawResponseBody { get; }

    public static ChatCompletionResult Success(string content, string? rawResponseBody)
        => new(true, content, null, null, rawResponseBody);

    public static ChatCompletionResult Failure(string errorClass, string errorDetail, string? rawResponseBody)
        => new(false, null, errorClass, errorDetail, rawResponseBody);
}
