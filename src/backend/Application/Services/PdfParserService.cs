using System.Text.RegularExpressions;
using Analyzer.Entities;
using UglyToad.PdfPig;

namespace Analyzer.Services;

public class PdfParserService
{
    
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

    
    public List<DocumentSection> ParseSections(string fullText)
    {
        var sections = new List<DocumentSection>();
        if (string.IsNullOrWhiteSpace(fullText)) return sections;

        
        var linePattern = new Regex(@"^(?<maddeNo>\d+(\.\d+)*)\s+(?<baslik>[^\r\n]+)", RegexOptions.Multiline);

        var lines = fullText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        
        DocumentSection? currentSection = null;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            var match = linePattern.Match(trimmedLine);

            if (match.Success)
            {
                
                if (currentSection != null)
                {
                    sections.Add(currentSection);
                }

                string maddeNo = match.Groups["maddeNo"].Value;
                string baslikText = match.Groups["baslik"].Value.Trim();

                
                string anaBaslik = baslikText;
                string altBaslik = baslikText; 

                if (maddeNo.Contains("."))
                {
                    
                    altBaslik = baslikText;
                }

                currentSection = new DocumentSection
                {
                    MaddeNo = maddeNo,
                    AnaBaslik = string.IsNullOrWhiteSpace(anaBaslik) ? "Genel Şartlar" : anaBaslik,
                    AltBaslik = string.IsNullOrWhiteSpace(altBaslik) ? "Detay Maddesi" : altBaslik, 
                    Content = trimmedLine
                };
            }
            else if (currentSection != null)
            {
                
                currentSection.Content += "\n" + trimmedLine;
            }
        }

        
        if (currentSection != null)
        {
            sections.Add(currentSection);
        }

        
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