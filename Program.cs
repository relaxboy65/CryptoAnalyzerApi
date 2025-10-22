using CryptoAnalyzerApi.Services;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Urls.Add($"http://*:{port}");

app.MapGet("/", () => "API آماده است ✅");

// خروجی JSON ساختاریافته
app.MapGet("/analyze", async (string symbol) =>
{
    var service = new AnalyzerService();
    var (r5, r15, finalSignal) = await service.AnalyzeAsync(symbol);
    return Results.Json(new { result5 = r5, result15 = r15, finalSignal });
});

// خروجی متنی مثل WinForms
app.MapGet("/analyzeText", async (string symbol) =>
{
    var service = new AnalyzerService();
    var (r5, r15, finalSignal) = await service.AnalyzeAsync(symbol);
    var text = service.ToText(r5, r15, finalSignal);
    return Results.Text(text);
});

app.Run();
