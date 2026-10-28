using System.Text.RegularExpressions;
using Analyzer.Entities;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Analyzer.Services;

public class PdfParserService
{
    public string ExtractTextFromPdf(Stream pdfStream) => string.Join("\n", ExtractPages(pdfStream));

    public List<string> ExtractPages(Stream pdfStream)
    {
        if (pdfStream == null || pdfStream.Length == 0) return new();
        if (pdfStream.CanSeek) pdfStream.Position = 0;
        using var document = PdfDocument.Open(pdfStream);
        return document.GetPages().Select(page => ContentOrderTextExtractor.GetText(page).Trim()).ToList();
    }

    // Prefer source text for clearly numbered headings; AI is a fallback for other layouts.
    public List<DocumentSection> ParseSections(string fullText)
    {
        var sections = new List<DocumentSection>();
        var heading = new Regex(@"^(?<number>\d+(?:\.\d+)*)(?<dot>\.)?\s+(?<title>\p{L}[^\r\n]*)$");
        DocumentSection? current = null;
        string mainTitle = "";
        foreach (string line in fullText.Split('\n'))
        {
            string text = line.Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;
            var match = heading.Match(text);
            string candidate = match.Groups["number"].Value;
            bool isHeading = match.Success && (candidate.Contains('.')
                ? sections.Any(section => section.MaddeNo == candidate.Split('.')[0])
                : match.Groups["dot"].Success);
            if (isHeading)
            {
                string number = match.Groups["number"].Value;
                string title = match.Groups["title"].Value;
                if (!number.Contains('.')) mainTitle = title;
                current = new DocumentSection
                {
                    MaddeNo = number,
                    AnaBaslik = string.IsNullOrWhiteSpace(mainTitle) ? title : mainTitle,
                    AltBaslik = title,
                    Content = ""
                };
                sections.Add(current);
            }
            else if (current != null)
            {
                current.Content += (current.Content.Length == 0 ? "" : " ") + text;
            }
        }
        return sections;
    }
}
