using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NavigationPlatform.Gateway.Middleware;

internal sealed class RequestBodyCaptureMiddleware
{
    public const string ItemKey = "RequestBody";
    public const string TruncatedItemKey = "RequestBodyTruncated";
    public const int MaxBodyBytes = 8 * 1024;

    private static readonly HashSet<string> SensitiveKeys = new(
        new[]
        {
            "password",
            "pass",
            "token",
            "access_token",
            "accessToken",
            "refresh_token",
            "refreshToken",
            "client_secret",
            "clientSecret",
            "secret",
            "authorization"
        },
        StringComparer.OrdinalIgnoreCase);

    private readonly RequestDelegate _next;

    public RequestBodyCaptureMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        if (!ShouldCapture(context.Request))
        {
            await _next(context);
            return;
        }

        context.Request.EnableBuffering();

        var buffer = new byte[MaxBodyBytes + 1];
        var bytesRead = await context.Request.Body.ReadAsync(
            buffer.AsMemory(0, buffer.Length),
            context.RequestAborted);

        var truncated = bytesRead > MaxBodyBytes;
        var used = Math.Min(bytesRead, MaxBodyBytes);

        context.Request.Body.Position = 0;

        if (used > 0)
        {
            var raw = Encoding.UTF8.GetString(buffer, 0, used);
            var sanitized = TrySanitizeJson(raw, out var sanitizedJson)
                ? sanitizedJson
                : raw;

            context.Items[ItemKey] = sanitized;
            context.Items[TruncatedItemKey] = truncated;
        }

        await _next(context);
    }

    private static bool ShouldCapture(HttpRequest request)
    {
        if (HttpMethods.IsGet(request.Method) ||
            HttpMethods.IsHead(request.Method) ||
            HttpMethods.IsDelete(request.Method) ||
            HttpMethods.IsOptions(request.Method))
            return false;

        var contentType = request.ContentType;
        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        // Only JSON payloads.
        if (!contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) &&
            !contentType.Contains("+json", StringComparison.OrdinalIgnoreCase))
            return false;

        // Skip known sensitive endpoints.
        var path = request.Path.Value ?? string.Empty;
        if (path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private static bool TrySanitizeJson(string json, out string sanitized)
    {
        sanitized = json;

        try
        {
            var node = JsonNode.Parse(json);
            if (node == null) return false;

            Redact(node);
            sanitized = node.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = false
            });
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void Redact(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var kvp in obj.ToList())
            {
                if (SensitiveKeys.Contains(kvp.Key))
                {
                    obj[kvp.Key] = "[REDACTED]";
                }
                else if (kvp.Value != null)
                {
                    Redact(kvp.Value);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var child in arr)
            {
                if (child != null)
                    Redact(child);
            }
        }
    }
}

