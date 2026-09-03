using Analyzer.Services;
using AiAnaliz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Analyzer;

var builder = WebApplication.CreateBuilder(args);


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
Console.WriteLine($"[DEBUG] Kullanılan connection string: {connectionString}");
builder.Services.AddDbContext<AppDBContext>(options =>
    options.UseNpgsql(connectionString, o => o.UseVector()));

var geminiApiKey = builder.Configuration["Gemini:ApiKey"] ?? string.Empty;


builder.Services.AddScoped<PdfParserService>();
builder.Services.AddScoped(_ => new AiService(geminiApiKey));
builder.Services.AddScoped(_ => new EmbeddingService(geminiApiKey));
builder.Services.AddScoped<DocumentService>();


builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddAntiforgery();

var app = builder.Build();

app.UseCors("AllowAll");
app.UseAntiforgery();

app.MapGet("/api/Folder/list", async ([FromServices] DocumentService docService) =>
{
    try
    {
        var folders = await docService.GetFoldersAsync();
        return Results.Ok(folders);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[FOLDER LIST HATA]: {ex}");
        return Results.Problem($"Klasör listesi hatası: {ex.InnerException?.Message ?? ex.Message}");
    }
})
.RequireCors("AllowAll");

app.MapGet("/api/Folder/{folderId:int}/documents", async (int folderId, [FromServices] DocumentService docService) =>
{
    try
    {
        var docs = await docService.GetDocumentsByFolderAsync(folderId);
        return Results.Ok(docs);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[FOLDER DOCUMENTS HATA]: {ex}");
        return Results.Problem($"Belge listesi hatası: {ex.InnerException?.Message ?? ex.Message}");
    }
})
.RequireCors("AllowAll");


app.MapPost("/api/Document/analyze", async (
    HttpRequest request,
    [FromServices] DocumentService docService) =>
{
    try
    {
        var form = await request.ReadFormAsync();

        IFormFile? oldPdf = form.Files.GetFile("oldPdf");
        IFormFile? newPdf = form.Files.GetFile("newPdf");

        string? oldCustomName = form["oldCustomName"];
        string? newCustomName = form["newCustomName"];
        string? oldFolderName = form["oldFolderName"];
        string? newFolderName = form["newFolderName"];

        int? oldDocumentId = int.TryParse(form["oldDocumentId"], out var oid) ? oid : (int?)null;
        int? newDocumentId = int.TryParse(form["newDocumentId"], out var nid) ? nid : (int?)null;

        if (oldPdf == null && !oldDocumentId.HasValue)
            return Results.BadRequest("Eski belge için dosya yükleyin ya da kayıtlı bir belge seçin.");

        if (newPdf == null && !newDocumentId.HasValue)
            return Results.BadRequest("Yeni belge için dosya yükleyin ya da kayıtlı bir belge seçin.");

        var result = await docService.GetOrAnalyzeAsync(
            oldPdf, oldCustomName, oldFolderName, oldDocumentId,
            newPdf, newCustomName, newFolderName, newDocumentId);

        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ANALİZ DETAYLI HATA]: {ex.ToString()}");
        var detail = ex.InnerException?.Message ?? ex.Message;
        return Results.Problem($"Analiz hatası: {detail}");
    }
})
.RequireCors("AllowAll")
.DisableAntiforgery();

app.Run("http://localhost:5000");