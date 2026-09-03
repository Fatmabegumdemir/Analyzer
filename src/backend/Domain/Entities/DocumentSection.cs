using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace Analyzer.Entities;

[Table("DocumentSections")]
public class DocumentSection
{
    [Column("Id")]
    public int Id { get; set; }

    [Column("DocumentId")]
    public int DocumentId { get; set; }

    public Document Document { get; set; } = null!;

    [Column("MaddeNo")]
    public string MaddeNo { get; set; } = string.Empty;

    [Column("AnaBaslik")]
    public string AnaBaslik { get; set; } = string.Empty;

    [Column("AltBaslik")]
    public string AltBaslik { get; set; } = string.Empty;

    [Column("Content")]
    public string Content { get; set; } = string.Empty;

    
    [Column("Embedding")]
    public Vector? Embedding { get; set; }
}