using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Analyzer.Services;

public class EmbeddingService
{
    private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    private readonly string _apiKey;

    public EmbeddingService(string apiKey)
    {
        _apiKey = apiKey ?? string.Empty;
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        if(string.IsNullOrWhiteSpace(text)) return Array.Empty<float>();
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-004:embedContent?key={_apiKey}";

        var requestBody = new
        {
            model = "models/text-embedding-004",
            content = new
            {
                parts = new[] { new { text = text } }
            },
            outputDimensionality = 1536
        };

        var json = JsonSerializer.Serialize(requestBody);

        // 🔄 503 (Yoğunluk) durumunda 3 defa otomatik yeniden deneme
        int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);

                // Google geçici yoğunluk döndüyse azıcık bekleip tekrar dene
                if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable && attempt < maxRetries)
                {
                    await Task.Delay(1500);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseString);

                return doc.RootElement
                    .GetProperty("embedding")
                    .GetProperty("values")
                    .EnumerateArray()
                    .Select(x => x.GetSingle())
                    .ToArray();
            }
            catch (Exception)
            {
                if (attempt == maxRetries) return Array.Empty<float>();
                await Task.Delay(1500);
            }
        }

        return Array.Empty<float>();
    }
}