using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AiAnaliz;

public class AiService
{
    // 🔥 Güncel API Key'iniz
    private readonly string _apiKey = "AQ.Ab8RN6KgMHJVp8QW2zXU9ANq1ohQ9GHS8END3-yiZSxDftQUng";
    private readonly HttpClient _httpClient = new HttpClient() { Timeout = TimeSpan.FromMinutes(10) };

    private readonly string _analysisPrompt = @"<role>
Sen teknik şartnameleri ve kalite standartlarını (ISO 9001, IATF 16949, CSR) satır satır karşılaştıran kıdemli bir kalite denetçisisin. Görevin ESKİ ve YENİ belge arasındaki maddeleri kıyaslamak ve farkları tespit etmektir.
</role>

<strict_rules>
- Bir maddede birden fazla değişiklik varsa bunları TEK bir kayıtta yaz ama AiAnaliz kısmında (a, b, c...) şeklinde maddeleyerek açıkla.
- Hiçbir maddeyi özetleme veya dışarıda bırakma.
- Metinde kaç tane farklı madde numarası görüyorsan HER BİRİ İÇİN ayrı bir JSON nesnesi üret. Ardışık maddeler birbirine benzese bile HİÇBİRİNİ atlama, birleştirme veya özetleme. Tek numaralı bölüm üst başlıklarını (örn. sadece '4', '5', '6' gibi) da ayrı birer madde olarak listele.
- 'AnaBaslik' ve 'AltBaslik' ALANLARINA KESİNLİKLE açıklama, gerekçe, kod notu, şema terimi veya sistem mesajı YAZMA. Bu alanlara SADECE belgeden gelen kısa ve temiz başlık ismini yaz. Bütün analiz açıklamalarını SADECE 'AiAnaliz' alanına yaz.
- Çift dilli metinlerde (örn. Almanca/İngilizce) 'EskiMetin' ve 'YeniMetin' alanlarına SADECE İngilizce metni yaz.
- Maddeleri asla '10.2.1 and 10.2.2' gibi birleştirme. Her alt madde numarasını bağımsız birer satır olarak dök.
- Metin içerikleri İngilizce olsa bile 'AiAnaliz' açıklamasını mutlaka net ve profesyonel bir TÜRKÇE ile yaz.
- Sayfa numarası, footer, header veya telif hakkı gibi tekrarlayan bilgileri analiz dışı tut.
</strict_rules>";

    // 🌐 1. İKİ BELGEYİ KARŞILAŞTIRAN ANA ANALİZ METODU
    public async Task<string> AnalyzePdfsAsync(string oldBase64, string newBase64)
    {
        string jsonResult = await CallGeminiApiAsync(_analysisPrompt, oldBase64, newBase64);
        return ConsolidateAndSort(jsonResult);
    }

    public async Task<string> AnalyzeSectionsAsync(string oldSectionsJson, string newSectionsJson)
    {
        string prompt = _analysisPrompt + $@"

Aşağıda ESKİ ve YENİ belgelerin ayrıştırılmış madde listeleri JSON formatında verilmiştir. Bu yapılandırılmış veriyi kullanarak karşılaştırma yap.

ESKİ BELGE MADDELERİ:
{oldSectionsJson}

YENİ BELGE MADDELERİ:
{newSectionsJson}";

        var fullSchema = new
        {
            type = "ARRAY",
            items = new
            {
                type = "OBJECT",
                properties = new
                {
                    MaddeNo = new { type = "STRING" },
                    AnaBaslik = new { type = "STRING" },
                    AltBaslik = new { type = "STRING" },
                    EskiMetin = new { type = "STRING" },
                    YeniMetin = new { type = "STRING" },
                    Durum = new { type = "STRING" },
                    AiAnaliz = new { type = "STRING" }
                },
                required = new[] { "MaddeNo", "Durum" }
            }
        };

        string jsonResult = await CallGeminiTextApiAsync(prompt, fullSchema);
        return ConsolidateAndSort(jsonResult);
    }

    private string ConsolidateAndSort(string jsonResult)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var reportItems = JsonSerializer.Deserialize<List<CsrAiItem>>(jsonResult, options) ?? new List<CsrAiItem>();

        var consolidatedItems = reportItems
            .GroupBy(x => x.MaddeNo?.Trim())
            .Select(g => new CsrAiItem
            {
                MaddeNo = g.Key ?? "",
                AnaBaslik = g.First().AnaBaslik,
                AltBaslik = g.First().AltBaslik,
                EskiMetin = string.Join(" | ", g.Select(x => x.EskiMetin).Where(x => !string.IsNullOrEmpty(x) && x != "-").Distinct()),
                YeniMetin = string.Join(" | ", g.Select(x => x.YeniMetin).Where(x => !string.IsNullOrEmpty(x) && x != "-").Distinct()),
                Durum = g.First().Durum,
                AiAnaliz = g.Count() == 1
                    ? g.First().AiAnaliz
                    : string.Join(" ", g.Select((x, idx) => (x.AiAnaliz ?? "").StartsWith($"{idx + 1}.") ? x.AiAnaliz : $"{idx + 1}. {x.AiAnaliz}"))
            })
            .ToList();

        var sorted = consolidatedItems.OrderBy(x => x.MaddeNo, new ClauseComparer()).ToList();
        return JsonSerializer.Serialize(sorted);
    }

    // 🎯 DEĞİŞEN MADDE ÇİFTLERİNİ ANALİZ EDEN METOT
    public async Task<string> AnalyzeChangedSectionsAsync(string pairsJson)
    {
        string prompt = $@"Sana eski ve yeni şartnamede Madde No'ları eşleşen ancak metinleri veya başlıkları farklı olan maddeler JSON formatında verilmiştir.
Lütfen her bir madde çiftini hem başlıklar (OldAltBaslik vs NewAltBaslik) hem de metinler (OldContent vs NewContent) açısından dikkatlice kıyasla.

HAKEM KURALLARI:
1. 'anaBaslik' VE 'altBaslik' ALANLARI İÇİN:
   - SADECE belgeden gelen en güncel temiz başlık isimlerini yaz. 
   - Bu alanlara KESİNLİKLE İngilizce açıklama, gerekçe, kod notu veya sistem mesajı YAZMA! Bütün analiz açıklamalarını SADECE 'aiAnaliz' alanına yaz.

2. BAŞLIK DEĞİŞİKLİKLERİ:
   - Eğer 'OldAltBaslik' ile 'NewAltBaslik' (veya ana başlıklar) arasında bir kelime, harf, ek veya zaman farkı VARSA:
     - Bunu 'aiAnaliz' açıklamasının BAŞINDA açıkça belirt (Örn: 'Alt başlık ""X"" iken ""Y"" olarak güncellenmiştir.').

3. İÇERİK DEĞİŞİKLİKLERİ:
   - Eğer iki metin arasında URL adresi, portal referansı (Örn: 'Supplier Quality Hub'), teknik kural, kelime veya rakam değişikliği VARSA:
     - 'durum' alanını 'Değişti' yap.
     - 'aiAnaliz' alanında metinde neyin değiştiğini net bir şekilde Türkçe olarak açıkla.
   - Eğer hem başlıkta hem metinde değişiklik varsa, İKİSİNİ DE aynı 'aiAnaliz' açıklamasında birleştirerek yaz.

4. BİÇİMSEL BİREBİR AYNI DURUMLAR:
   - Eğer iki metin ve başlık arasında SADECE biçimsel/yazı stili farkı varsa ve ANLAMSAL/BAĞLANTI HİÇBİR FARK YOKSA:
     - 'durum' alanını 'Değişmedi' yap.
     - 'aiAnaliz' alanına 'Bu madde metninde ve başlığında teknik bir değişiklik tespit edilmemiştir.' yaz.

DEĞİŞEN MADDELER:
{pairsJson}";

        var analyzeSchema = new
        {
            type = "ARRAY",
            items = new
            {
                type = "OBJECT",
                properties = new
                {
                    maddeNo = new { type = "STRING" },
                    anaBaslik = new { type = "STRING" },
                    altBaslik = new { type = "STRING" },
                    durum = new { type = "STRING" },
                    eskiMetin = new { type = "STRING" },
                    yeniMetin = new { type = "STRING" },
                    aiAnaliz = new { type = "STRING" }
                },
                required = new[] { "maddeNo", "durum", "eskiMetin", "yeniMetin", "aiAnaliz" }
            }
        };

        string jsonResult = await CallGeminiTextApiAsync(prompt, analyzeSchema);
        return ConsolidateAndSort(jsonResult);
    }

    // 🎯 2. TEK BİR BELGEYİ AI İLE MADDELERİNE PARÇALAYAN METOT
   public async Task<string> ParseRawTextToSectionsAsync(string rawText)
{
    string prompt = $@"
Sana bir teknik şartnamenin ham metni verilmiştir. Görevin bu metni hiyerarşik maddelerine ayırarak eksiksiz ayrıştırmaktır.

ÇOK ÖNEMLİ VE KESİN TEMİZLİK / AYRIŞTIRMA KURALLARI:

1. HİÇBİR MADDEYİ ATLAMA (TAM ENUMERASYON): 
   Metinde kaç tane farklı madde numarası (Örn: 3.1, 3.2, 3.3 ... 3.31 veya 8.5.4, 8.5.4.1) görüyorsan, HER BİRİ İÇİN ayrı bir JSON nesnesi üret. 
   Ardışık tanımlar birbirine benzese bile HİÇBİRİNİ atlama, birleştirme veya özetleme.

1a. BÖLÜM ÜST BAŞLIKLARINI DA MADDE OLARAK ÇIKAR:
   Tek numaralı bölüm/chapter başlıkları (örn. sadece '4', '5', '6', '7', '8', '9', '10' gibi alt
   madde numarası içermeyen ana başlıklar) kendi altında ayrı bir açıklama paragrafı olmasa BİLE
   MUTLAKA bağımsız bir madde olarak listele. Bu durumda 'content' alanına o başlığın kendi adını yaz
   (örn. maddeNo '4' için content: 'Context of the organization'). Böyle bir üst başlığı asla atlama.

1b. BİRLEŞİK MADDE NUMARALARINI AYIR:
   Kaynak metinde bir başlığın altında '6.1.1 and 6.1.2' veya '10.2.1 and 10.2.2' gibi birden fazla
   madde numarası birlikte yazılmışsa, bunu TEK bir maddeNo olarak DEĞİL, ayrı ayrı maddeler olarak
   çıkar (örn. '6.1.1' ve '6.1.2' iki ayrı JSON objesi olsun, aynı content'i paylaşabilirler). Bu
   ayrıştırmayı tutarlı uygula ki aynı madde farklı belgelerde farklı şekilde (bazen birleşik bazen
   ayrı) çıkmasın.

2. 'content' ALANI TEMİZLİĞİ (BAŞLIK VEYA MADDE NO YAZMA):
   - 'content' alanının BAŞINA madde numarasını (Örn: '3.1') veya madde başlığını (Örn: 'Active Part') ASLA YAZMA!
   - 'content' alanına SADECE VE SADECE kuralın/paragrafın kendi gerçek gövde metnini yaz.
   - YANLIŞ KULLANIM: ""3.1 Active Part An active part is one currently supplied...""
   - DOĞRU KULLANIM: ""An active part is one currently supplied...""

3. PDF ÜST/ALT BİLGİLERİNİ (HEADER/FOOTER) ATLA:
   - 'Page X of Y', 'Page 5 of 44' gibi sayfa numaralarını 'content' içine KESİNLİKLE DAHİL ETME.
   - 'Copyright © Ford Motor Company', 'Summary of IATF-16949...' gibi tekrarlayan sayfa altlıklarını/başlıklarını 'content' alanına yazma.

4. DİL VE FORMAT DÜZENİ:
   - Metinde hem Almanca hem İngilizce varsa 'content' alanına SADECE İngilizce metni yaz.
   - Paragraflar arasındaki gereksiz satır sonlarını birleştirerek düzgün tek bir metin bloğu haline getir.

5. BAŞLIK ATAMALARI:
   - 'anaBaslik': Maddenin bağlı olduğu ana bölüm adıdır (Örn: 'Terms and definitions', 'Context of the organization').
   - 'altBaslik': Maddenin kendi özel başlığıdır (Örn: 3.1 için 'Active Part', 3.2 için 'Aftermarket Parts'). Alt başlığa ana başlığın adını tekrarlama.

ÇIKTI FORMATI (SADECE JSON ARRAY):
[
  {{
    ""maddeNo"": ""3.1"",
    ""anaBaslik"": ""Terms and definitions"",
    ""altBaslik"": ""Active Part"",
    ""content"": ""An active part is one currently supplied to the customer for original equipment or service applications...""
  }}
]

ŞARTNAME METNİ:
{rawText}";

    var parseSchema = new
    {
        type = "ARRAY",
        items = new
        {
            type = "OBJECT",
            properties = new
            {
                maddeNo = new { type = "STRING" },
                anaBaslik = new { type = "STRING" },
                altBaslik = new { type = "STRING" },
                content = new { type = "STRING" }
            },
            required = new[] { "maddeNo", "anaBaslik", "altBaslik", "content" }
        }
    };

    return await CallGeminiTextApiAsync(prompt, parseSchema);
}

    public async Task<string> ProcessAndAnalyzePdfsAsync(string oldPdfPath, string newPdfPath)
    {
        byte[] oldBytes = await File.ReadAllBytesAsync(oldPdfPath);
        byte[] newBytes = await File.ReadAllBytesAsync(newPdfPath);

        string oldBase64 = Convert.ToBase64String(oldBytes);
        string newBase64 = Convert.ToBase64String(newBytes);

        return await AnalyzePdfsAsync(oldBase64, newBase64);
    }

    public string GenerateCsv(List<CsrAiItem> items)
    {
        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("Madde No;Ana Başlık;Alt Başlık;Eski Metin;Yeni Metin;Durum;AI Analiz");

        foreach (var item in items)
        {
            string maddeNo = $"\"\t{item.MaddeNo}\"";
            string anaBaslik = CleanForCsv(item.AnaBaslik);
            string altBaslik = CleanForCsv(item.AltBaslik);
            string eski = string.IsNullOrWhiteSpace(item.EskiMetin) ? "-" : CleanForCsv(item.EskiMetin);
            string yeni = string.IsNullOrWhiteSpace(item.YeniMetin) ? "-" : CleanForCsv(item.YeniMetin);
            string durum = string.IsNullOrWhiteSpace(item.Durum) ? "Değişmedi" : CleanForCsv(item.Durum);
            string analiz = string.IsNullOrWhiteSpace(item.AiAnaliz) ? "-" : CleanForCsv(item.AiAnaliz);

            csvBuilder.AppendLine($"{maddeNo};\"{anaBaslik}\";\"{altBaslik}\";\"{eski}\";\"{yeni}\";{durum};\"{analiz}\"");
        }

        return csvBuilder.ToString();
    }

    private async Task<string> CallGeminiApiAsync(string prompt, string oldBase64, string newBase64)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent?key={_apiKey}";
        var partsList = new List<object>
        {
            new { text = prompt }
        };

        if (!string.IsNullOrEmpty(oldBase64))
        {
            partsList.Add(new { inline_data = new { mime_type = "application/pdf", data = oldBase64 } });
        }

        if (!string.IsNullOrEmpty(newBase64))
        {
            partsList.Add(new { inline_data = new { mime_type = "application/pdf", data = newBase64 } });
        }

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = partsList.ToArray()
                }
            },
            generationConfig = new
            {
                max_output_tokens = 65536,
                response_mime_type = "application/json",
                thinking_config = new { thinking_level = "low" }
            }
        };

        JsonElement responseJson = default;
        int maxRetries = 5;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Content = JsonContent.Create(requestBody);

            var response = await _httpClient.SendAsync(httpRequest);

            if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable && attempt < maxRetries)
            {
                await Task.Delay(5000 * attempt);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gemini API Hatası ({response.StatusCode}): {error}");
            }

            responseJson = await response.Content.ReadFromJsonAsync<JsonElement>();
            break;
        }

        string jsonResult = responseJson
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString()!;

        string cleanJson = CleanJsonMarkdown(jsonResult);
        return FixIncompleteJson(cleanJson);
    }

    private async Task<string> CallGeminiTextApiAsync(string prompt, object response_schema)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent?key={_apiKey}";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                max_output_tokens = 65536,
                response_mime_type = "application/json",
                response_schema = response_schema,
                thinking_config = new { thinking_level = "low" }
            }
        };

        JsonElement responseJson = default;
        int maxRetries = 5;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Content = JsonContent.Create(requestBody);

            var response = await _httpClient.SendAsync(httpRequest);

            if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable && attempt < maxRetries)
            {
                await Task.Delay(5000 * attempt);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gemini API Hatası ({response.StatusCode}): {error}");
            }

            responseJson = await response.Content.ReadFromJsonAsync<JsonElement>();
            break;
        }

        string jsonResult = responseJson
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString()!;

        string cleanJson = CleanJsonMarkdown(jsonResult);
        return FixIncompleteJson(cleanJson);
    }

    private string CleanJsonMarkdown(string jsonText)
    {
        if (string.IsNullOrEmpty(jsonText)) return "[]";

        string cleaned = jsonText.Trim();

        if (cleaned.StartsWith("```json")) cleaned = cleaned.Substring(7);
        else if (cleaned.StartsWith("```")) cleaned = cleaned.Substring(3);
        if (cleaned.EndsWith("```")) cleaned = cleaned.Substring(0, cleaned.Length - 3);

        cleaned = cleaned.Trim();

        cleaned = Regex.Replace(
            cleaned,
            @"\\u(?![0-9a-fA-F]{4})",
            @"\\\\u"
        );

        return cleaned;
    }

    private string FixIncompleteJson(string json)
    {
        try
        {
            json = json.Trim();

            json = Regex.Replace(json, @"^```json\s*", "");
            json = Regex.Replace(json, @"^```\s*", "");
            json = Regex.Replace(json, @"\s*```$", "");
            json = json.Trim();

            if (string.IsNullOrEmpty(json))
                return "[]";

            json = Regex.Replace(json, @"\\u(?![0-9a-fA-F]{4})", @"\\u");

            int openArrays = json.Count(c => c == '[');
            int closeArrays = json.Count(c => c == ']');
            int openObjects = json.Count(c => c == '{');
            int closeObjects = json.Count(c => c == '}');

            json = Regex.Replace(json, @",\s*$", "");

            for (int i = closeObjects; i < openObjects; i++)
                json += "}";
            for (int i = closeArrays; i < openArrays; i++)
                json += "]";

            JsonDocument.Parse(json);
            return json;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[JSON DÜZELTME HATASI]: {ex.Message}");
            return "[]";
        }
    }

    private string CleanForCsv(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input.Replace("\"", "\"\"").Replace("\r\n", " ").Replace("\n", " ");
    }
}

public class ClauseComparer : IComparer<string?>
{
    public int Compare(string? x, string? y)
    {
        if (x == y) return 0;
        if (string.IsNullOrWhiteSpace(x)) return -1;
        if (string.IsNullOrWhiteSpace(y)) return 1;

        var partsX = x.Split(new[] { '.', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var partsY = y.Split(new[] { '.', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        int minLength = Math.Min(partsX.Length, partsY.Length);

        for (int i = 0; i < minLength; i++)
        {
            bool isNumX = int.TryParse(partsX[i], out int numX);
            bool isNumY = int.TryParse(partsY[i], out int numY);

            if (isNumX && isNumY)
            {
                if (numX != numY) return numX.CompareTo(numY);
            }
            else
            {
                int strCompare = string.Compare(partsX[i], partsY[i], StringComparison.OrdinalIgnoreCase);
                if (strCompare != 0) return strCompare;
            }
        }

        return partsX.Length.CompareTo(partsY.Length);
    }
}