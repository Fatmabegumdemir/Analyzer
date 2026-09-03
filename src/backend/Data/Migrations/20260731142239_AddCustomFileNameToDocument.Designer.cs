
using System;
using Analyzer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

namespace Analyzer.Migrations
{
    [DbContext(typeof(AppDBContext))]
    [Migration("20260731142239_AddCustomFileNameToDocument")]
    partial class AddCustomFileNameToDocument
    {
        
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.3")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.HasPostgresExtension(modelBuilder, "vector");
            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Analyzer.Entities.Document", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("Id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("CustomFileName")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("CustomFileName");

                    b.Property<string>("FilePath")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("FilePath");

                    b.Property<int?>("FolderId")
                        .HasColumnType("integer")
                        .HasColumnName("FolderId");

                    b.Property<string>("OriginalFileName")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("OriginalFileName");

                    b.Property<DateTime>("UploadedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("UploadedAt");

                    b.HasKey("Id");

                    b.ToTable("Documents");
                });

            modelBuilder.Entity("Analyzer.Entities.DocumentRevision", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("AnalysisResultJson")
                        .HasColumnType("text");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("NewDocumentId")
                        .HasColumnType("integer");

                    b.Property<int>("OldDocumentId")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("NewDocumentId");

                    b.HasIndex("OldDocumentId");

                    b.ToTable("DocumentRevisions");
                });

            modelBuilder.Entity("Analyzer.Entities.DocumentSection", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("AltBaslik")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("AnaBaslik")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("DocumentId")
                        .HasColumnType("integer");

                    b.Property<Vector>("Embedding")
                        .HasColumnType("vector(1536)");

                    b.Property<string>("MaddeNo")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("DocumentId");

                    b.ToTable("DocumentSections");
                });

            modelBuilder.Entity("Analyzer.Entities.DocumentRevision", b =>
                {
                    b.HasOne("Analyzer.Entities.Document", "NewDocument")
                        .WithMany()
                        .HasForeignKey("NewDocumentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Analyzer.Entities.Document", "OldDocument")
                        .WithMany()
                        .HasForeignKey("OldDocumentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("NewDocument");

                    b.Navigation("OldDocument");
                });

            modelBuilder.Entity("Analyzer.Entities.DocumentSection", b =>
                {
                    b.HasOne("Analyzer.Entities.Document", "Document")
                        .WithMany("Sections")
                        .HasForeignKey("DocumentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Document");
                });

            modelBuilder.Entity("Analyzer.Entities.Document", b =>
                {
                    b.Navigation("Sections");
                });
#pragma warning restore 612, 618
        }
    }
}
