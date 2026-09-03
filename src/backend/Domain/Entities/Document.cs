using System.ComponentModel.DataAnnotations.Schema;

namespace Analyzer.Entities;

[Table("Documents")]
public class Document
{
    [Column("Id")]
    public int Id { get; set; }

    [Column("FolderId")]
    public int? FolderId { get; set; }

    [Column("filehash")]
    public string FileHash { get; set; } = string.Empty;

    [Column("customfilename")]
    public string CustomFileName { get; set; } = string.Empty;

    [Column("originalfilename")]
    public string OriginalFileName { get; set; } = string.Empty;

    [Column("filepath")]
    public string FilePath { get; set; } = string.Empty;

    [Column("uploadedat")]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DocumentSection> Sections { get; set; } = new List<DocumentSection>();

    public Folder? Folder{get; set;}
}