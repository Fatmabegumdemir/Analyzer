using System.Text.RegularExpressions;
using Analyzer.Entities;
using UglyToad.PdfPig;

namespace Analyzer.Services;

public class PdfParserService
{
    // 1. PDF'ten düz metin çıkarma
    public string ExtractTextFromPdf(Stream pdfStream)
{
    if (pdfStream == null || pdfStream.Length == 0) return string.Empty;

    if (pdfStream.CanSeek && pdfStream.Position != 0) pdfStream.Position = 0;

    using var pdfDocument = PdfDocument.Open(pdfStream);

    var pages = pdfDocument.GetPages().ToList();
    var sb = new System.Text.StringBuilder();

    for (int i = 0; i < pages.Count; i++)
    {
        sb.Append(pages[i].Text.Trim());
        sb.Append($"\n\n[SAYFA_SONU:{i + 1}]\n\n");
    }

    return sb.ToString();
}
public List<string> ExtractPages(Stream pdfStream)
{
    if (pdfStream == null || pdfStream.Length == 0) return new List<string>();
    if (pdfStream.CanSeek && pdfStream.Position != 0) pdfStream.Position = 0;

    using var pdfDocument = PdfDocument.Open(pdfStream);
    return pdfDocument.GetPages().Select(p => p.Text.Trim()).ToList();
}

    // 🎯 2. Metni Maddelere ve Alt Başlıklara Ayırma Metodu (YENİ EKLENEN KISIM)
    public List<DocumentSection> ParseSections(string fullText)
    {
        var sections = new List<DocumentSection>();
        if (string.IsNullOrWhiteSpace(fullText)) return sections;

        // "1. Scope", "1.1 General Requirements", "3.2.1 Quality Standard" gibi madde kalıplarını yakalar
        var linePattern = new Regex(@"^(?<maddeNo>\d+(\.\d+)*)\s+(?<baslik>[^\r\n]+)", RegexOptions.Multiline);

        var lines = fullText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        
        DocumentSection? currentSection = null;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            var match = linePattern.Match(trimmedLine);

            if (match.Success)
            {
                // Önceki madde varsa listeye ekle
                if (currentSection != null)
                {
                    sections.Add(currentSection);
                }

                string maddeNo = match.Groups["maddeNo"].Value;
                string baslikText = match.Groups["baslik"].Value.Trim();

                // 💡 Alt Başlık Garantisi: Başlığı ve Madde Seviyesini Ayrıştırıyoruz
                string anaBaslik = baslikText;
                string altBaslik = baslikText; // Default olarak baslikText veriyoruz ki asla null/boş kalmasın

                if (maddeNo.Contains("."))
                {
                    // Örneğin 1.1 veya 3.2.1 gibi alt maddeler için
                    altBaslik = baslikText;
                }

                currentSection = new DocumentSection
                {
                    MaddeNo = maddeNo,
                    AnaBaslik = string.IsNullOrWhiteSpace(anaBaslik) ? "Genel Şartlar" : anaBaslik,
                    AltBaslik = string.IsNullOrWhiteSpace(altBaslik) ? "Detay Maddesi" : altBaslik, // 👈 NOT-NULL GARANTİSİ
                    Content = trimmedLine
                };
            }
            else if (currentSection != null)
            {
                // Başlık satırı değilse mevcut maddenin içeriğine ekle
                currentSection.Content += "\n" + trimmedLine;
            }
        }

        // Son kalan maddeyi de ekle
        if (currentSection != null)
        {
            sections.Add(currentSection);
        }

        // Eğer regex hiç madde yakalayamadıysa (düz metin ise) belgenin tamamını tek madde yapma fallback'i
        if (sections.Count == 0 && !string.IsNullOrWhiteSpace(fullText))
        {
            sections.Add(new DocumentSection
            {
                MaddeNo = "1",
                AnaBaslik = "Genel Şartname Metni",
                AltBaslik = "Genel Şartname Metni",
                Content = fullText
            });
        }

        return sections;
    }
}