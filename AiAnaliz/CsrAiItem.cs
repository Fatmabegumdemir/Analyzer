using System.Text.Json.Serialization;

namespace AiAnaliz;

public class CsrAiItem
{
    [JsonPropertyName("maddeNo")]  // camelCase
public string MaddeNo { get; set; } = "";

    [JsonPropertyName("AnaBaslik")]
    public string AnaBaslik { get; set; } = "";

    [JsonPropertyName("AltBaslik")]
    public string AltBaslik { get; set; } = "";

    [JsonPropertyName("Durum")]
    public string Durum { get; set; } = "";

    [JsonPropertyName("EskiMetin")]
    public string EskiMetin { get; set; } = "";

    [JsonPropertyName("YeniMetin")]
    public string YeniMetin { get; set; } = "";

    [JsonPropertyName("AiAnaliz")]
    public string AiAnaliz { get; set; } = "";
}