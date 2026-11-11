// Sends requests to the AI provider. (Abstraction)

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CodeQuest.Api;

internal static class ProviderProfiles
{
    public const string Nvidia = "nvidia";

    public const string OpenRouter = "openrouter";

    public const string AppTitle = "CodeQuest";

    public const string AppUrl = "https://github.com/Shuash11/queziah";

    public static bool SendsAttributionHeaders(string tierName) => tierName == OpenRouter;

    public static bool InspectsBudget403(string tierName) => tierName == OpenRouter;
}

public sealed class ChatCompletionsClient : IDisposable
{
    public const double RequestTemperature = 0.2;

    public const int RequestMaxTokens = 4096;

    public const int TimeoutSeconds = 60;

    private static readonly JsonSerializerOptions ResponseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _endpointUrl;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _tierName;
    private readonly HttpClient _httpClient;
    private readonly HttpMessageHandler? _externalHandler;

    public ChatCompletionsClient(string tierName, string baseUrl, string apiKey, string model)
        : this(tierName, baseUrl, apiKey, model, handler: null, timeout: null)
    {
    }

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

    public void Dispose()
    {
        _httpClient.Dispose();
    }

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

            using HttpResponseMessage response = SendSynchronously(request);
            return ReadOutcome(response);
        }
        catch (TaskCanceledException)
        {
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
            return ChatCompletionResult.Failure(
                ChatCompletionErrorClasses.Timeout,
                "network-unreachable: " + Sanitize(exception.Message),
                rawResponseBody: null);
        }
        catch (Exception exception)
        {
            return ChatCompletionResult.Failure(
                ChatCompletionErrorClasses.HttpError,
                "unexpected: " + Sanitize(exception.GetType().Name),
                rawResponseBody: null);
        }
    }

    private HttpResponseMessage SendSynchronously(HttpRequestMessage request)
        => _httpClient
            .SendAsync(request)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

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

        return ChatCompletionResult.Failure(ChatCompletionErrorClasses.HttpError, status.ToString(), body);
    }

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

    private string TimeoutDetail()
        => $"{(int)Math.Ceiling(_httpClient.Timeout.TotalSeconds)}s";

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
