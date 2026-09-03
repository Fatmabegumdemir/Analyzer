using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Analyzer.Migrations
{
    
    public partial class AddExtractedContentToDocument : Migration
    {
        
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentRevisions_Documents_DocumentId",
                table: "DocumentRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentSections_DocumentRevisions_DocumentRevisionId",
                table: "DocumentSections");

            migrationBuilder.DropIndex(
                name: "IX_DocumentSections_DocumentRevisionId",
                table: "DocumentSections");

            migrationBuilder.DropColumn(
                name: "DocumentRevisionId",
                table: "DocumentSections");

            migrationBuilder.DropColumn(
                name: "FilePath",
                table: "DocumentRevisions");

            migrationBuilder.DropColumn(
                name: "RevisionCode",
                table: "DocumentRevisions");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "Documents",
                newName: "FileName");

            migrationBuilder.RenameColumn(
                name: "DocumentNumber",
                table: "Documents",
                newName: "FileHash");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Documents",
                newName: "UploadedAt");

            migrationBuilder.RenameColumn(
                name: "UploadDate",
                table: "DocumentRevisions",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "DocumentId",
                table: "DocumentRevisions",
                newName: "OldDocumentId");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentRevisions_DocumentId",
                table: "DocumentRevisions",
                newName: "IX_DocumentRevisions_OldDocumentId");

            migrationBuilder.AlterColumn<string>(
                name: "MaddeNo",
                table: "DocumentSections",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AnaBaslik",
                table: "DocumentSections",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AltBaslik",
                table: "DocumentSections",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedContent",
                table: "Documents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AnalysisResultJson",
                table: "DocumentRevisions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NewDocumentId",
                table: "DocumentRevisions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRevisions_NewDocumentId",
                table: "DocumentRevisions",
                column: "NewDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentRevisions_Documents_NewDocumentId",
                table: "DocumentRevisions",
                column: "NewDocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentRevisions_Documents_OldDocumentId",
                table: "DocumentRevisions",
                column: "OldDocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentRevisions_Documents_NewDocumentId",
                table: "DocumentRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentRevisions_Documents_OldDocumentId",
                table: "DocumentRevisions");

            migrationBuilder.DropIndex(
                name: "IX_DocumentRevisions_NewDocumentId",
                table: "DocumentRevisions");

            migrationBuilder.DropColumn(
                name: "ExtractedContent",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "AnalysisResultJson",
                table: "DocumentRevisions");

            migrationBuilder.DropColumn(
                name: "NewDocumentId",
                table: "DocumentRevisions");

            migrationBuilder.RenameColumn(
                name: "UploadedAt",
                table: "Documents",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "FileName",
                table: "Documents",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "FileHash",
                table: "Documents",
                newName: "DocumentNumber");

            migrationBuilder.RenameColumn(
                name: "OldDocumentId",
                table: "DocumentRevisions",
                newName: "DocumentId");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "DocumentRevisions",
                newName: "UploadDate");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentRevisions_OldDocumentId",
                table: "DocumentRevisions",
                newName: "IX_DocumentRevisions_DocumentId");

            migrationBuilder.AlterColumn<string>(
                name: "MaddeNo",
                table: "DocumentSections",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "AnaBaslik",
                table: "DocumentSections",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "AltBaslik",
                table: "DocumentSections",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "DocumentRevisionId",
                table: "DocumentSections",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                table: "DocumentRevisions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RevisionCode",
                table: "DocumentRevisions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSections_DocumentRevisionId",
                table: "DocumentSections",
                column: "DocumentRevisionId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentRevisions_Documents_DocumentId",
                table: "DocumentRevisions",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentSections_DocumentRevisions_DocumentRevisionId",
                table: "DocumentSections",
                column: "DocumentRevisionId",
                principalTable: "DocumentRevisions",
                principalColumn: "Id");
        }
    }
}
