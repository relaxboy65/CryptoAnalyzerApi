using System.Text.Json;
using CryptoAnalyzerApi.Models;

namespace CryptoAnalyzerApi.Services;

public class CryptoCompareClient
{
    private readonly HttpClient _http = new HttpClient();

    public async Task<List<Candle>> GetHistoricAsync(string fsym, int minutes, int limit = 300)
    {
        string endpoint = minutes < 60 ? "histominute" : (minutes == 60 ? "histohour" : "histoday");
        int aggregate = minutes < 60 ? minutes : minutes / 60;

        string url = $"https://min-api.cryptocompare.com/data/v2/{endpoint}?fsym={fsym}&tsym=USD&limit={limit}&aggregate={aggregate}";

        var response = await _http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(response);

        if (!doc.RootElement.TryGetProperty("Data", out var dataObj) ||
            !dataObj.TryGetProperty("Data", out var arr))
            return new List<Candle>();

        var candles = new List<Candle>();
        foreach (var d in arr.EnumerateArray())
        {
            candles.Add(new Candle
            {
                TimeUnix = d.TryGetProperty("time", out var t) ? t.GetInt64() : 0,
                Open     = d.TryGetProperty("open", out var o) ? o.GetDecimal() : 0,
                High     = d.TryGetProperty("high", out var h) ? h.GetDecimal() : 0,
                Low      = d.TryGetProperty("low", out var l) ? l.GetDecimal() : 0,
                Close    = d.TryGetProperty("close", out var c) ? c.GetDecimal() : 0
            });
        }
        return candles;
    }
}
