using Analyzer;
using Analyzer.Entities;
using AiAnaliz;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Analyzer.Services;

public class DocumentService
{
    private readonly AppDBContext _context;
    private readonly PdfParserService _pdfParser;
    private readonly AiService _aiService;

    public DocumentService(
        AppDBContext context,
        PdfParserService pdfParser,
        AiService aiService)
    {
        _context = context;
        _pdfParser = pdfParser;
        _aiService = aiService;
    }

    public async Task<List<CsrAiItem>> GetOrAnalyzeAsync(
        IFormFile? oldPdf, string? oldCustomName, string? oldFolderName, int? oldDocumentId,
        IFormFile? newPdf, string? newCustomName, string? newFolderName, int? newDocumentId)
    {
        Document oldDoc;
        if (oldDocumentId.HasValue)
        {
            oldDoc = await _context.Documents.FindAsync(oldDocumentId.Value)
                ?? throw new Exception("Seçilen eski belge bulunamadı.");
        }
        else if (oldPdf != null)
        {
            byte[] oldBytes = await ReadAllBytesAsync(oldPdf);
            string oldHash = ComputeHash(oldBytes);
            oldDoc = await GetOrCreateDocumentAsync(oldPdf, oldBytes, oldHash, oldCustomName, oldFolderName);
        }
        else
        {
            throw new Exception("Eski belge için ya dosya yüklenmeli ya da kayıtlı bir dosya seçilmelidir.");
        }

        Document newDoc;
        if (newDocumentId.HasValue)
        {
            newDoc = await _context.Documents.FindAsync(newDocumentId.Value)
                ?? throw new Exception("Seçilen yeni belge bulunamadı.");
        }
        else if (newPdf != null)
        {
            byte[] newBytes = await ReadAllBytesAsync(newPdf);
            string newHash = ComputeHash(newBytes);
            newDoc = await GetOrCreateDocumentAsync(newPdf, newBytes, newHash, newCustomName, newFolderName);
        }
        else
        {
            throw new Exception("Yeni belge için ya dosya yüklenmeli ya da kayıtlı bir dosya seçilmelidir.");
        }

        
        // Repair source-backed sections before considering a cached comparison.
        await RefreshSourceSectionsAsync(oldDoc);
        if (newDoc.Id != oldDoc.Id) await RefreshSourceSectionsAsync(newDoc);

        var existingRevision = await _context.DocumentRevisions
            .FirstOrDefaultAsync(r => r.OldDocumentId == oldDoc.Id && r.NewDocumentId == newDoc.Id);

        if (existingRevision != null && !string.IsNullOrWhiteSpace(existingRevision.AnalysisResultJson) && existingRevision.AnalysisResultJson.Trim() != "[]")
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<CsrAiItem>>(existingRevision.AnalysisResultJson, options) ?? new List<CsrAiItem>();
        }

        
        var oldSections = await _context.DocumentSections.Where(s => s.DocumentId == oldDoc.Id).ToListAsync();
        var newSections = await _context.DocumentSections.Where(s => s.DocumentId == newDoc.Id).ToListAsync();

        var analysisResults = new List<CsrAiItem>();
        var claimedNewSectionIds = new HashSet<int>();
        var claimedOldSectionIds = new HashSet<int>();
        var pairsToAnalyze = new List<object>();

        
        foreach (var oldSec in oldSections)
        {
            if (claimedOldSectionIds.Contains(oldSec.Id)) continue;

            string oldMaddeNo = NormalizeMaddeNo(oldSec.MaddeNo);
            if (string.IsNullOrEmpty(oldMaddeNo)) continue;

            var matchingNewSec = newSections.FirstOrDefault(ns => !claimedNewSectionIds.Contains(ns.Id) && NormalizeMaddeNo(ns.MaddeNo) == oldMaddeNo);

            if (matchingNewSec != null)
            {
                claimedNewSectionIds.Add(matchingNewSec.Id);
                claimedOldSectionIds.Add(oldSec.Id);

                string oldContent = oldSec.Content.Trim();
                string newContent = matchingNewSec.Content.Trim();

                bool isTitleSame = string.Equals(oldSec.AltBaslik?.Trim(), matchingNewSec.AltBaslik?.Trim(), StringComparison.Ordinal)
                    && string.Equals(oldSec.AnaBaslik?.Trim(), matchingNewSec.AnaBaslik?.Trim(), StringComparison.Ordinal);
                bool isContentSame = string.Equals(NormalizeWhitespace(oldContent), NormalizeWhitespace(newContent), StringComparison.Ordinal);

                if (isTitleSame && isContentSame)
                {
                    analysisResults.Add(new CsrAiItem
                    {
                        MaddeNo = oldSec.MaddeNo ?? "-",
                        AnaBaslik = string.IsNullOrWhiteSpace(matchingNewSec.AnaBaslik) ? (oldSec.AnaBaslik ?? "Genel") : matchingNewSec.AnaBaslik,
                        AltBaslik = string.IsNullOrWhiteSpace(matchingNewSec.AltBaslik) ? (oldSec.AltBaslik ?? oldSec.MaddeNo ?? "-") : matchingNewSec.AltBaslik,
                        Durum = "Değişmedi",
                        EskiMetin = oldContent,
                        YeniMetin = newContent,
                        AiAnaliz = "Bu madde metninde değişiklik tespit edilmemiştir."
                    });
                }
                else
                {
                    pairsToAnalyze.Add(new
                    {
                        MaddeNo = matchingNewSec.MaddeNo ?? oldSec.MaddeNo,
                        OldAnaBaslik = oldSec.AnaBaslik ?? "",
                        NewAnaBaslik = matchingNewSec.AnaBaslik ?? "",
                        OldAltBaslik = oldSec.AltBaslik ?? "",
                        NewAltBaslik = matchingNewSec.AltBaslik ?? "",
                        OldContent = oldContent,
                        NewContent = newContent
                    });
                }
            }
        }

        
        foreach (var oldSec in oldSections.Where(s => !claimedOldSectionIds.Contains(s.Id)))
        {
            claimedOldSectionIds.Add(oldSec.Id);
            string oldContent = oldSec.Content.Trim();

            if (IsInformationalOrEmpty(oldContent, oldSec.AltBaslik, oldSec.AnaBaslik))
                continue;

            analysisResults.Add(new CsrAiItem
            {
                MaddeNo = oldSec.MaddeNo ?? "-",
                AnaBaslik = oldSec.AnaBaslik ?? "Genel",
                AltBaslik = oldSec.AltBaslik ?? oldSec.MaddeNo ?? "-",
                Durum = "Kaldırıldı",
                EskiMetin = oldContent,
                YeniMetin = "-",
                AiAnaliz = "Bu madde ve kural metni yeni şartnameden tamamen çıkarılmıştır."
            });
        }

        
        foreach (var newSec in newSections.Where(s => !claimedNewSectionIds.Contains(s.Id)))
        {
            claimedNewSectionIds.Add(newSec.Id);
            string newContent = newSec.Content.Trim();

            if (IsInformationalOrEmpty(newContent, newSec.AltBaslik, newSec.AnaBaslik))
                continue;

            analysisResults.Add(new CsrAiItem
            {
                MaddeNo = newSec.MaddeNo ?? "-",
                AnaBaslik = newSec.AnaBaslik ?? "Genel",
                AltBaslik = newSec.AltBaslik ?? newSec.MaddeNo ?? "-",
                Durum = "Eklendi",
                EskiMetin = "-",
                YeniMetin = newContent,
                AiAnaliz = "Bu madde ve kural metni yeni şartnameye sıfırdan eklenmiştir."
            });
        }

        
        if (pairsToAnalyze.Any())
        {
            string pairsJson = JsonSerializer.Serialize(pairsToAnalyze);
            string aiResponseJson = await _aiService.AnalyzeChangedSectionsAsync(pairsJson);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var aiResults = JsonSerializer.Deserialize<List<CsrAiItem>>(aiResponseJson, options) ?? new List<CsrAiItem>();

            // AI supplies explanations only: source fields always come from the stored PDF text.
            foreach (var oldSection in oldSections)
            {
                var newSection = newSections.FirstOrDefault(section =>
                    NormalizeMaddeNo(section.MaddeNo) == NormalizeMaddeNo(oldSection.MaddeNo));
                if (newSection == null || analysisResults.Any(item =>
                    NormalizeMaddeNo(item.MaddeNo) == NormalizeMaddeNo(oldSection.MaddeNo))) continue;
                var explanation = aiResults.FirstOrDefault(item =>
                    NormalizeMaddeNo(item.MaddeNo) == NormalizeMaddeNo(oldSection.MaddeNo));
                string oldText = oldSection.Content.Trim();
                string newText = newSection.Content.Trim();
                string details = DescribeSourceChanges(oldSection, newSection);
                analysisResults.Add(new CsrAiItem
                {
                    MaddeNo = newSection.MaddeNo,
                    AnaBaslik = newSection.AnaBaslik,
                    AltBaslik = newSection.AltBaslik,
                    EskiMetin = oldText,
                    YeniMetin = newText,
                    Durum = "Değişti",
                    AiAnaliz = string.IsNullOrWhiteSpace(explanation?.AiAnaliz)
                        ? details : explanation.AiAnaliz + " " + details
                });
            }
        }

        analysisResults = analysisResults
            .OrderBy(x => x.MaddeNo, new ClauseComparer())
            .ToList();

        
        string finalResultJson = JsonSerializer.Serialize(analysisResults);
        var newRevision = new DocumentRevision
        {
            OldDocumentId = oldDoc.Id,
            NewDocumentId = newDoc.Id,
            AnalysisResultJson = finalResultJson,
            CreatedAt = DateTime.UtcNow
        };

        _context.DocumentRevisions.Add(newRevision);
        await _context.SaveChangesAsync();

        return analysisResults;
    }

    private static bool HasCompleteNumberedStructure(List<DocumentSection> sections) =>
        sections.Count > 0 && sections.Any(section => section.MaddeNo == "1")
        && sections.Select(section => section.MaddeNo).Distinct().Count() == sections.Count;

    private async Task RefreshSourceSectionsAsync(Document doc)
    {
        if (!File.Exists(doc.FilePath)) return;
        using var stream = File.OpenRead(doc.FilePath);
        var parsed = _pdfParser.ParseSections(string.Join("\n", _pdfParser.ExtractPages(stream)));
        if (!HasCompleteNumberedStructure(parsed)) return;
        var stored = await _context.DocumentSections.Where(section => section.DocumentId == doc.Id).ToListAsync();
        bool unchanged = stored.Count == parsed.Count && parsed.All(section => stored.Any(old =>
            old.MaddeNo == section.MaddeNo && old.AnaBaslik == section.AnaBaslik && old.AltBaslik == section.AltBaslik
            && NormalizeWhitespace(old.Content) == NormalizeWhitespace(section.Content)));
        if (unchanged) return;
        using var transaction = await _context.Database.BeginTransactionAsync();
        _context.DocumentSections.RemoveRange(stored);
        foreach (var section in parsed) section.DocumentId = doc.Id;
        _context.DocumentSections.AddRange(parsed);
        var revisions = await _context.DocumentRevisions.Where(revision =>
            revision.OldDocumentId == doc.Id || revision.NewDocumentId == doc.Id).ToListAsync();
        _context.DocumentRevisions.RemoveRange(revisions);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static string DescribeSourceChanges(DocumentSection oldSection, DocumentSection newSection)
    {
        var details = new List<string>();
        if (oldSection.AltBaslik != newSection.AltBaslik)
            details.Add($"Başlık değişikliği: «{oldSection.AltBaslik}» → «{newSection.AltBaslik}».");
        if (oldSection.AnaBaslik != newSection.AnaBaslik && oldSection.AnaBaslik != oldSection.AltBaslik)
            details.Add($"Ana başlık değişikliği: «{oldSection.AnaBaslik}» → «{newSection.AnaBaslik}».");
        var oldWords = Regex.Split(oldSection.Content.Trim(), @"\s+");
        var newWords = Regex.Split(newSection.Content.Trim(), @"\s+");
        int prefix = 0;
        while (prefix < oldWords.Length && prefix < newWords.Length && oldWords[prefix] == newWords[prefix]) prefix++;
        int oldEnd = oldWords.Length, newEnd = newWords.Length;
        while (oldEnd > prefix && newEnd > prefix && oldWords[oldEnd - 1] == newWords[newEnd - 1]) { oldEnd--; newEnd--; }
        if (oldEnd > prefix) details.Add("Çıkarılan/değiştirilen metin: «" + string.Join(" ", oldWords[prefix..oldEnd]) + "».");
        if (newEnd > prefix) details.Add("Eklenen/yeni metin: «" + string.Join(" ", newWords[prefix..newEnd]) + "».");
        return string.Join(" ", details);
    }

    private static bool IsInformationalOrEmpty(string content, string? altBaslik, string? anaBaslik)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Trim() == "-") return true;

        string trimmed = content.Trim();
        string[] emptyIndicators =
        {
            "No Ford Customer-Specific Requirement for this section",
            "See ISO 9001:2015 requirements",
            "See IATF 16949 for applicable references",
            "See ISO/TS"
        };

        foreach (var ind in emptyIndicators)
        {
            if (trimmed.StartsWith(ind, StringComparison.OrdinalIgnoreCase)) return true;
        }

        if (string.Equals(trimmed, altBaslik?.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(trimmed, anaBaslik?.Trim(), StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }

    private static string CleanContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return "";
        return Regex.Replace(content.Trim(), @"^((\d+(\.\d+)*)|\([a-zA-Z0-9]+\))\s*", "");
    }

    private static string NormalizeWhitespace(string input)
    {
        return Regex.Replace(input.Trim(), @"\s+", " ");
    }

    private static string NormalizeMaddeNo(string? maddeNo)
    {
        if (string.IsNullOrWhiteSpace(maddeNo)) return "";
        return string.Join(" ", maddeNo.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }

    private async Task<Document> GetOrCreateDocumentAsync(IFormFile file, byte[] fileBytes, string hash, string? customFileName, string? folderName)
    {
        var existingDoc = await _context.Documents.FirstOrDefaultAsync(d => d.FileHash == hash);
        if (existingDoc != null) return existingDoc;

        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);
        await File.WriteAllBytesAsync(filePath, fileBytes);
        int? folderId = await GetOrCreateFolderIdAsync(folderName);

        var newDoc = new Document
        {
            FileHash = hash,
            OriginalFileName = file.FileName,
            CustomFileName = string.IsNullOrWhiteSpace(customFileName) ? file.FileName : customFileName,
            FilePath = filePath,
            FolderId = folderId,
            UploadedAt = DateTime.UtcNow
        };

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Documents.Add(newDoc);
            await _context.SaveChangesAsync();

            await ParseAndStoreSectionsAsync(newDoc, fileBytes);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            if (File.Exists(filePath)) File.Delete(filePath);
            throw;
        }

        return newDoc;
    }

    private static async Task<byte[]> ReadAllBytesAsync(IFormFile file)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        return ms.ToArray();
    }

    private string ComputeHash(byte[] fileBytes)
    {
        using var sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(fileBytes);
        return Convert.ToHexString(hashBytes);
    }

    private async Task ParseAndStoreSectionsAsync(Document doc, byte[] fileBytes)
    {
        List<string> pages;
        using (var ms = new MemoryStream(fileBytes))
        {
            pages = _pdfParser.ExtractPages(ms);
        }

        if (pages.Count == 0)
            throw new InvalidOperationException($"PDF'ten metin çıkarılamadı (belge: {doc.OriginalFileName}).");

        var sourceSections = _pdfParser.ParseSections(string.Join("\n", pages));
        if (HasCompleteNumberedStructure(sourceSections))
        {
            foreach (var section in sourceSections) section.DocumentId = doc.Id;
            _context.DocumentSections.AddRange(sourceSections);
            await _context.SaveChangesAsync();
            return;
        }

        const int chunkSize = 10;
        var allParsedSections = new List<ParsedSectionItem>();

        for (int start = 0; start < pages.Count; start += chunkSize)
        {
            var chunkPages = pages.Skip(start).Take(chunkSize).ToList();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < chunkPages.Count; i++)
            {
                sb.Append(chunkPages[i]);
                sb.Append($"\n\n[SAYFA_SONU:{start + i + 1}]\n\n");
            }

            string sectionsJson = await _aiService.ParseRawTextToSectionsAsync(sb.ToString());

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            List<ParsedSectionItem>? chunkSections;
            try
            {
                chunkSections = JsonSerializer.Deserialize<List<ParsedSectionItem>>(sectionsJson, options);
            }
            catch
            {
                chunkSections = new List<ParsedSectionItem>();
            }

            if (chunkSections != null)
                allParsedSections.AddRange(chunkSections);
        }

        if (allParsedSections.Count == 0)
            throw new InvalidOperationException($"Gemini parse 0 madde döndürdü (belge: {doc.OriginalFileName}).");

        var dedupedSections = allParsedSections
            .Where(s => !string.IsNullOrWhiteSpace(s.MaddeNo) && s.MaddeNo != "-")
            .GroupBy(s => NormalizeMaddeNo(s.MaddeNo))
            .Select(g =>
            {
                var bestItem = g.OrderByDescending(x => x.Content?.Trim().Length ?? 0).First();
                return new ParsedSectionItem
                {
                    MaddeNo = bestItem.MaddeNo,
                    AnaBaslik = g.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.AnaBaslik) && x.AnaBaslik != "Genel")?.AnaBaslik ?? bestItem.AnaBaslik,
                    AltBaslik = g.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.AltBaslik) && x.AltBaslik != "Terms and definitions")?.AltBaslik ?? bestItem.AltBaslik,
                    Content = bestItem.Content
                };
            })
            .Where(s => !string.IsNullOrWhiteSpace(s.Content))
            .ToList();

        // Comparison uses clause numbers and text; no embedding requests are needed.
        var docSections = dedupedSections.Select(section =>
        {
            string safeAnaBaslik = string.IsNullOrWhiteSpace(section.AnaBaslik) ? "Genel" : section.AnaBaslik;
            string safeAltBaslik = string.IsNullOrWhiteSpace(section.AltBaslik) ? safeAnaBaslik : section.AltBaslik;
            string cleanedContent = CleanContent(section.Content);
            return new DocumentSection
            {
                DocumentId = doc.Id,
                MaddeNo = section.MaddeNo,
                AnaBaslik = safeAnaBaslik,
                AltBaslik = safeAltBaslik,
                Content = string.IsNullOrWhiteSpace(cleanedContent) ? safeAltBaslik : cleanedContent
            };
        }).ToList();
        _context.DocumentSections.AddRange(docSections);

        await _context.SaveChangesAsync();
    }

    private async Task<int?> GetOrCreateFolderIdAsync(string? folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName)) return null;

        var trimmedName = folderName.Trim();
        var existing = await _context.Folders.FirstOrDefaultAsync(f => f.FolderName == trimmedName);
        if (existing != null) return existing.Id;

        var newFolder = new Folder { FolderName = trimmedName, CreatedAt = DateTime.UtcNow };
        _context.Folders.Add(newFolder);
        await _context.SaveChangesAsync();
        return newFolder.Id;
    }

    public async Task<List<Folder>> GetFoldersAsync()
    {
        return await _context.Folders.OrderBy(f => f.FolderName).ToListAsync();
    }

    public async Task<List<Document>> GetDocumentsByFolderAsync(int folderId)
    {
        return await _context.Documents
            .Where(d => d.FolderId == folderId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();
    }
}