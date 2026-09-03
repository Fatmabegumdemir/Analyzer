using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Analyzer.Migrations
{
    
    public partial class AddDocumentRevisions : Migration
    {
        
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Embedding",
                table: "DocumentSections",
                newName: "Empedding");

            migrationBuilder.RenameColumn(
                name: "UploadedAt",
                table: "Documents",
                newName: "uploadedat");

            migrationBuilder.RenameColumn(
                name: "OriginalFileName",
                table: "Documents",
                newName: "originalfilename");

            migrationBuilder.RenameColumn(
                name: "FilePath",
                table: "Documents",
                newName: "filepath");

            migrationBuilder.RenameColumn(
                name: "CustomFileName",
                table: "Documents",
                newName: "customfilename");
        }

        
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Empedding",
                table: "DocumentSections",
                newName: "Embedding");

            migrationBuilder.RenameColumn(
                name: "uploadedat",
                table: "Documents",
                newName: "UploadedAt");

            migrationBuilder.RenameColumn(
                name: "originalfilename",
                table: "Documents",
                newName: "OriginalFileName");

            migrationBuilder.RenameColumn(
                name: "filepath",
                table: "Documents",
                newName: "FilePath");

            migrationBuilder.RenameColumn(
                name: "customfilename",
                table: "Documents",
                newName: "CustomFileName");
        }
    }
}
