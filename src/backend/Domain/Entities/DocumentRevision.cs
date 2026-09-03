namespace Analyzer.Entities;

public class DocumentRevision
{
    public int Id { get; set; }

    public int OldDocumentId { get; set; }
    public Document OldDocument { get; set; } = null!;

    public int NewDocumentId { get; set; }
    public Document NewDocument { get; set; } = null!;

    public string? AnalysisResultJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}