using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Analyzer.Migrations
{
    
    public partial class AddCustomFileNameToDocument : Migration
    {
        
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FileName",
                table: "Documents",
                newName: "OriginalFileName");

            migrationBuilder.RenameColumn(
                name: "FileHash",
                table: "Documents",
                newName: "FilePath");

            migrationBuilder.RenameColumn(
                name: "ExtractedContent",
                table: "Documents",
                newName: "CustomFileName");

            migrationBuilder.AddColumn<int>(
                name: "FolderId",
                table: "Documents",
                type: "integer",
                nullable: true);
        }

        
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "Documents");

            migrationBuilder.RenameColumn(
                name: "OriginalFileName",
                table: "Documents",
                newName: "FileName");

            migrationBuilder.RenameColumn(
                name: "FilePath",
                table: "Documents",
                newName: "FileHash");

            migrationBuilder.RenameColumn(
                name: "CustomFileName",
                table: "Documents",
                newName: "ExtractedContent");
        }
    }
}
