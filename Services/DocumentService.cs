using Analyzer;
using Analyzer.Entities;
using AiAnaliz;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Analyzer.Services;

public class DocumentService
{
    private readonly AppDBContext _context;
    private readonly PdfParserService _pdfParser;
    private readonly AiService _aiService;
    private readonly EmbeddingService _embeddingService;

    public DocumentService(
        AppDBContext context,
        PdfParserService pdfParser,
        AiService aiService,
        EmbeddingService embeddingService)
    {
        _context = context;
        _pdfParser = pdfParser;
        _aiService = aiService;
        _embeddingService = embeddingService;
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

        // 1. Önceki analiz sonucu varsa getir
        var existingRevision = await _context.DocumentRevisions
            .FirstOrDefaultAsync(r => r.OldDocumentId == oldDoc.Id && r.NewDocumentId == newDoc.Id);

        if (existingRevision != null && !string.IsNullOrWhiteSpace(existingRevision.AnalysisResultJson) && existingRevision.AnalysisResultJson.Trim() != "[]")
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<CsrAiItem>>(existingRevision.AnalysisResultJson, options) ?? new List<CsrAiItem>();
        }

        // 2. Eski ve Yeni Maddeleri Çek
        var oldSections = await _context.DocumentSections.Where(s => s.DocumentId == oldDoc.Id).ToListAsync();
        var newSections = await _context.DocumentSections.Where(s => s.DocumentId == newDoc.Id).ToListAsync();

        var analysisResults = new List<CsrAiItem>();
        var claimedNewSectionIds = new HashSet<int>();
        var claimedOldSectionIds = new HashSet<int>();
        var pairsToAnalyze = new List<object>();

        // 🎯 KURAL 1: Birebir Madde Numarası Eşleşenler
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

                string oldContent = CleanContent(oldSec.Content);
                string newContent = CleanContent(matchingNewSec.Content);

                bool isTitleSame = string.Equals(oldSec.AltBaslik?.Trim(), matchingNewSec.AltBaslik?.Trim(), StringComparison.OrdinalIgnoreCase);
                bool isContentSame = string.Equals(NormalizeWhitespace(oldContent), NormalizeWhitespace(newContent), StringComparison.OrdinalIgnoreCase);

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

        // 🎯 KURAL 2: Eşleşmeyen Eski Maddeleri Değerlendirme (Kaldırıldı Kontrolü)
        foreach (var oldSec in oldSections.Where(s => !claimedOldSectionIds.Contains(s.Id)))
        {
            claimedOldSectionIds.Add(oldSec.Id);
            string oldContent = CleanContent(oldSec.Content);

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

        // 🎯 KURAL 3: Eşleşmeyen Yeni Maddeleri Değerlendirme (Eklendi Kontrolü)
        foreach (var newSec in newSections.Where(s => !claimedNewSectionIds.Contains(s.Id)))
        {
            claimedNewSectionIds.Add(newSec.Id);
            string newContent = CleanContent(newSec.Content);

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

        // 🎯 KURAL 4: Değişen Maddeleri AI ile Karşılaştırma
        if (pairsToAnalyze.Any())
        {
            string pairsJson = JsonSerializer.Serialize(pairsToAnalyze);
            string aiResponseJson = await _aiService.AnalyzeChangedSectionsAsync(pairsJson);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var aiResults = JsonSerializer.Deserialize<List<CsrAiItem>>(aiResponseJson, options) ?? new List<CsrAiItem>();

            foreach (var aiItem in aiResults)
            {
                if (string.IsNullOrWhiteSpace(aiItem.Durum))
                    aiItem.Durum = "Değişti";

                if (string.IsNullOrWhiteSpace(aiItem.AiAnaliz))
                    aiItem.AiAnaliz = "Bu madde metninde teknik/biçimsel değişiklik tespit edilmiştir.";

                analysisResults.Add(aiItem);
            }
        }

        analysisResults = analysisResults
            .OrderBy(x => x.MaddeNo, new ClauseComparer())
            .ToList();

        // 5. Sonuçları Kaydet
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

        var semaphore = new SemaphoreSlim(5);

        var embedTask = dedupedSections.Select(async section =>
        {
            await semaphore.WaitAsync();
            try
            {
                string safeAnaBaslik = string.IsNullOrWhiteSpace(section.AnaBaslik) ? "Genel" : section.AnaBaslik;
                string safeAltBaslik = string.IsNullOrWhiteSpace(section.AltBaslik) ? safeAnaBaslik : section.AltBaslik;
                string cleanedContent = CleanContent(section.Content);
                string contentText = string.IsNullOrWhiteSpace(cleanedContent) ? safeAltBaslik : cleanedContent;

                float[] embeddingArray = await _embeddingService.GetEmbeddingAsync(contentText);

                return new DocumentSection
                {
                    DocumentId = doc.Id,
                    MaddeNo = string.IsNullOrWhiteSpace(section.MaddeNo) ? "-" : section.MaddeNo,
                    AnaBaslik = safeAnaBaslik,
                    AltBaslik = safeAltBaslik,
                    Content = contentText,
                    Embedding = embeddingArray.Length > 0 ? new Vector(embeddingArray) : null
                };
            }
            finally
            {
                semaphore.Release();
            }
        });

        var docSections = await Task.WhenAll(embedTask);
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