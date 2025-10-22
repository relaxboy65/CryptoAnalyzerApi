using CryptoAnalyzerApi.Services;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Urls.Add($"http://*:{port}");

app.MapGet("/", () => "API آماده است ✅");

// خروجی متنی مثل برنامه اصلی
app.MapGet("/analyzeText", async (string symbol) =>
{
    var service = new AnalyzerTextService();
    var text = await service.AnalyzeTextAsync(symbol);
    return Results.Text(text);
});

app.Run();
