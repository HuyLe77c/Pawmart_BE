using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Pawmart.Middleware;

namespace Pawmart.Services.Integrations;

public sealed record AiChatMessage(string Role, string Content);

public sealed class OmnirouteClient(HttpClient http, IConfiguration config)
{
    public async Task<string> CompleteAsync(IReadOnlyList<AiChatMessage> messages, CancellationToken ct)
    {
        var key = config["Omniroute:ApiKey"];
        var baseUrl = config["Omniroute:BaseUrl"] ?? "http://127.0.0.1:20128/v1";
        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/chat/completions", UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ApiException(503, "Địa chỉ Omniroute chưa hợp lệ.", "AI unavailable");
        if (string.IsNullOrWhiteSpace(key) && !uri.IsLoopback)
            throw new ApiException(503, "Omniroute cần API key khi kết nối qua mạng.", "AI unavailable");

        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        if (!string.IsNullOrWhiteSpace(key))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            model = config["Omniroute:Model"] ?? "cloudflare-ai/@cf/meta/llama-3.3-70b-instruct-fp8-fast",
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            max_tokens = 1200,
            stream = false
        }), Encoding.UTF8, "application/json");

        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                throw new ApiException(503, "Omniroute chưa sẵn sàng. Vui lòng thử lại sau.", "AI unavailable");
            if (response.Content.Headers.ContentLength is > 131072)
                throw new ApiException(502, "Phản hồi AI không hợp lệ.", "AI response error");

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            var bytes = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(bytes, ct)) > 0)
            {
                if (buffer.Length + count > 131072)
                    throw new ApiException(502, "Phản hồi AI không hợp lệ.", "AI response error");
                await buffer.WriteAsync(bytes.AsMemory(0, count), ct);
            }
            buffer.Position = 0;
            using var json = await JsonDocument.ParseAsync(buffer, cancellationToken: ct);
            var content = json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(content))
                throw new ApiException(502, "Phản hồi AI không hợp lệ.", "AI response error");
            return content.Trim();
        }
        catch (ApiException) { throw; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ApiException(503, "Omniroute phản hồi quá lâu. Vui lòng thử lại.", "AI unavailable");
        }
        catch (HttpRequestException)
        {
            throw new ApiException(503, "Không kết nối được Omniroute. Vui lòng thử lại sau.", "AI unavailable");
        }
        catch (JsonException)
        {
            throw new ApiException(502, "Phản hồi AI không hợp lệ.", "AI response error");
        }
        catch (KeyNotFoundException)
        {
            throw new ApiException(502, "Phản hồi AI không hợp lệ.", "AI response error");
        }
        catch (InvalidOperationException)
        {
            throw new ApiException(502, "Phản hồi AI không hợp lệ.", "AI response error");
        }
        catch (IndexOutOfRangeException)
        {
            throw new ApiException(502, "Phản hồi AI không hợp lệ.", "AI response error");
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ApiException(502, "Phản hồi AI không hợp lệ.", "AI response error");
        }
    }
}
