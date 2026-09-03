using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Analyzer.Migrations
{
    
    public partial class AddFileHashToDocuments : Migration
    {
        
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "filehash",
                table: "Documents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_filehash",
                table: "Documents",
                column: "filehash",
                unique: true);
        }

        
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_filehash",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "filehash",
                table: "Documents");
        }
    }
}
