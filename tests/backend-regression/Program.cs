using Analyzer.Services;

var parser = new PdfParserService();
void Require(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
var sections = parser.ParseSections("1. Giriş\nİlk paragraf.\nİkinci paragraf.\n1.1. Alt başlık\nDeğişmeyen metin.\n2. Son bölüm\n55 yaşında.\nKaynak: https://example.com/source");
Require(sections.Count == 3, "Trailing-dot headings must be recognized.");
Require(sections[0].Content == "İlk paragraf. İkinci paragraf.", "Parent section body must be preserved.");
Require(sections[1].AnaBaslik == "Giriş", "Child must retain its parent title.");
Require(sections[2].Content.Contains("55 yaşında."), "Leading body numbers must not be stripped.");
Require(sections[2].Content.Contains("https://example.com/source"), "Source URL must not be dropped.");
Require(parser.ParseSections("Unnumbered text").Count == 0, "Unstructured documents must use the AI fallback.");
Console.WriteLine("Source parser regression checks passed.");
if (args.Length == 2)
{
    for (int i = 0; i < 2; i++)
    {
        using var stream = File.OpenRead(args[i]);
        var text = string.Join("\n", parser.ExtractPages(stream));
        Directory.CreateDirectory("output/diagnostics");
        File.WriteAllText(Path.Combine("output/diagnostics", $"pdfpig-{i}.txt"), text);
        var parsed = parser.ParseSections(text);
        Console.WriteLine("Parsed numbers: " + string.Join(", ", parsed.Select(s => s.MaddeNo)));
        Require(parsed.Select(s => s.MaddeNo).SequenceEqual(new[] { "1", "1.1", "1.2", "2" }), "Expected exactly four source sections.");
        Require(parsed[0].Content.StartsWith("Ansiklopediler"), "Introduction body missing.");
        Require(parsed[3].Content.Contains("sonucuna vardı."), "Original wording was changed.");
        if (i == 1) Require(parsed[3].Content.Contains("https://doi.org/10.64898/2026.02.09.26344392"), "Revision 2 source link missing.");
        Console.WriteLine($"{Path.GetFileName(args[i])}: source text checks passed.");
    }
}
