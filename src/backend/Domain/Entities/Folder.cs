using System.ComponentModel.DataAnnotations.Schema;
namespace Analyzer.Entities;

[Table("Folders")]
public class Folder
{
    [Column("Id")]
    public int Id {get; set;}

    [Column("FolderName")]
    public string FolderName {get; set;} = string.Empty;

    [Column("CreatedAt")]
    public DateTime CreatedAt {get; set;}= DateTime.UtcNow;

    public ICollection<Document> Documents{get; set;} = new List<Document>();
}