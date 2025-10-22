using CryptoAnalyzerApi.Services;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "API آماده است ✅");

// خروجی متنی مثل برنامه اصلی
app.MapGet("/analyzeText", async (string symbol) =>
{
    var service = new AnalyzerTextService();
    var text = await service.AnalyzeTextAsync(symbol);
    return Results.Text(text);
});

app.Run();
