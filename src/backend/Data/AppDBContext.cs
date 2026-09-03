using Analyzer.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace Analyzer;

public class AppDBContext : DbContext
{
    public AppDBContext(DbContextOptions<AppDBContext> options):base(options){
        
    }
    public DbSet<Document>Documents {get; set;}
    public DbSet<DocumentRevision>DocumentRevisions{get; set;}
    public DbSet<DocumentSection>DocumentSections{get; set;}

    public DbSet<Folder> Folders{get; set;}

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("vector");
        

        modelBuilder.Entity<DocumentSection>()
        
            .Property(b => b.Embedding)
            .HasColumnType("vector(768)");
        
    }
}