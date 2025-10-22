using System.Text;
using CryptoAnalyzerApi.Models;

namespace CryptoAnalyzerApi.Services;

public class AnalyzerService
{
    private readonly CryptoCompareClient _client = new();

    // وضعیت آخرین سیگنال برای هشدار تکراری
    private static string? lastSignal = null;

    public async Task<(AnalysisResult result5, AnalysisResult result15, string finalSignal)> AnalyzeAsync(string symbol)
    {
        var data5 = await _client.GetHistoricAsync(symbol, 5, 300);
        var data15 = await _client.GetHistoricAsync(symbol, 15, 300);

        var result5 = AnalyzeSingleTimeframe(symbol, "5m", data5);
        var result15 = AnalyzeSingleTimeframe(symbol, "15m", data15);

        // --- ترکیب سیگنال‌ها ---
        string finalSignal = "🤔 بدون قطعیت (HOLD)";
        bool short5 = result5.Signal.Contains("SHORT");
        bool short15 = result15.Signal.Contains("SHORT");
        bool long5 = result5.Signal.Contains("LONG");
        bool long15 = result15.Signal.Contains("LONG");

        if (long5 && long15)
            finalSignal = "✅ خرید (LONG)";
        else if (short5 && short15)
            finalSignal = "🚨 فروش (SHORT)";

        return (result5, result15, finalSignal);
    }
    private AnalysisResult AnalyzeSingleTimeframe(string symbol, string tf, List<Candle> candles)
    {
        var res = new AnalysisResult
        {
            Symbol = symbol.ToUpper(),
            Timeframe = tf,
            Signal = "🤝 HOLD",
            Verdict = "🤝 خنثی"
        };

        if (candles == null || candles.Count < 50) 
        {
            res.Signal = "⚠️ داده کافی نیست";
            return res;
        }

        var closes = candles.Select(c => c.Close).ToList();
        var rsiList = IndicatorService.CalculateRSI(closes, 14);
        var ema14List = IndicatorService.CalculateEMA(closes, 14);
        var ema50List = IndicatorService.CalculateEMA(closes, 50);
        var macdTuple = IndicatorService.CalculateMACD(closes);

        if (!rsiList.Any() || !ema14List.Any() || !ema50List.Any() || !macdTuple.macd.Any() || !macdTuple.signal.Any())
        {
            res.Signal = "⚠️ داده کافی نیست";
            return res;
        }

        decimal rsi = rsiList.Last();
        decimal ema14 = ema14List.Last();
        decimal ema50 = ema50List.Last();
        decimal macd = macdTuple.macd.Last();
        decimal macdSignal = macdTuple.signal.Last();

        // ذخیره اندیکاتورها
        res.RSI = rsi;
        res.EMA14 = ema14;
        res.EMA50 = ema50;
        res.MACD = macd;
        res.MACDSignal = macdSignal;

        int score = IndicatorService.ScoreConfluence(rsi, ema14, ema50, macd, macdSignal);
        res.Signal = score >= 2 ? "📈 LONG" : (score <= -2 ? "📉 SHORT" : "🤝 HOLD");

        // محاسبه سطوح
        decimal atr = IndicatorService.CalculateATR(candles);
        res.ATR = atr;
        decimal price = closes.Last();
        res.Entry = price;

        bool isLong = res.Signal.Contains("LONG");
        bool isShort = res.Signal.Contains("SHORT");

        if (isLong)
        {
            res.SL = price - atr;
            res.TP1 = price + atr * 2;
            res.TP2 = price + atr * 3;
        }
        else if (isShort)
        {
            res.SL = price + atr;
            res.TP1 = price - atr * 2;
            res.TP2 = price - atr * 3;
        }

        // هشدار سیگنال تکراری
        if (lastSignal == res.Signal && (isLong || isShort))
            res.Warnings.Add("سیگنال تکراری صادر شد، اما TP/SL دوباره محاسبه شد.");
        lastSignal = res.Signal;

        // Risk/Reward
        res.RiskPerUnit = res.SL == 0 ? 0 : Math.Abs(res.Entry - res.SL);
        var reward1 = res.TP1 == 0 ? 0 : Math.Abs(res.TP1 - res.Entry);
        res.RRR = res.RiskPerUnit > 0 ? reward1 / res.RiskPerUnit : 0;

        // هشدارها
        res.Warnings.AddRange(BuildWarnings(res.Signal, rsi, rsi, ema50, atr, res.Entry, res.SL, res.TP1));

        // Verdict
        bool hasCritical = res.Warnings.Any(w => w.Contains("اشباع") || w.Contains("SL") || w.Contains("R/R"));
        if (isLong)
            res.Verdict = hasCritical ? "⛔ اسکپ (ورود نکن)" : "✅ ورود LONG";
        else if (isShort)
            res.Verdict = hasCritical ? "⛔ اسکپ (ورود نکن)" : "✅ ورود SHORT";

        return res;
    }

    private static List<string> BuildWarnings(string finalSignal, decimal rsi5, decimal rsi15,
                                              decimal ema50_5m, decimal atr, decimal entry, decimal sl, decimal tp1)
    {
        var warnings = new List<string>();
        bool isLong = finalSignal.Contains("LONG");
        bool isShort = finalSignal.Contains("SHORT");

        decimal risk = Math.Abs(entry - sl);
        decimal reward = Math.Abs(tp1 - entry);
        decimal rrr = risk > 0 ? reward / risk : 0;

        if (isLong && rsi5 >= 65) warnings.Add("RSI 5m نزدیک اشباع خرید است → احتمال اصلاح کوتاه‌مدت.");
        if (isShort && rsi5 <= 35) warnings.Add("RSI 5m نزدیک اشباع فروش است → احتمال اصلاح کوتاه‌مدت.");
        if (isLong && rsi15 >= 65) warnings.Add("RSI 15m در اشباع خرید است.");
        if (isShort && rsi15 <= 35) warnings.Add("RSI 15m در اشباع فروش است.");

        if (isLong && sl > ema50_5m) warnings.Add("SL بهتر است زیر EMA50 (5m) باشد.");
        if (isShort && sl < ema50_5m) warnings.Add("SL بهتر است بالای EMA50 (5m) باشد.");

        if (risk < atr * 0.8m) warnings.Add("فاصله SL کمتر از 0.8×ATR است → احتمال خوردن SL زیاد.");
        if (rrr < 1.8m) warnings.Add($"نسبت R/R پایین است ({rrr:N2}).");

        return warnings;
    }

    // ✅ متد خروجی متنی
    public string ToText(AnalysisResult result5, AnalysisResult result15, string finalSignal)
{
    var sb = new StringBuilder();

    sb.AppendLine($"📊 تایم‌فریم {result5.Timeframe}");
    sb.AppendLine($"   RSI: {FormatNumber(result5.RSI)}");
    sb.AppendLine($"   EMA14/50: {FormatNumber(result5.EMA14)} / {FormatNumber(result5.EMA50)}");
    sb.AppendLine($"   MACD: {FormatNumber(result5.MACD)} / {FormatNumber(result5.MACDSignal)}");
    sb.AppendLine($"   سیگنال: {result5.Signal}");

    sb.AppendLine($"📊 تایم‌فریم {result15.Timeframe}");
    sb.AppendLine($"   RSI: {FormatNumber(result15.RSI)}");
    sb.AppendLine($"   EMA14/50: {FormatNumber(result15.EMA14)} / {FormatNumber(result15.EMA50)}");
    sb.AppendLine($"   MACD: {FormatNumber(result15.MACD)} / {FormatNumber(result15.MACDSignal)}");
    sb.AppendLine($"   سیگنال: {result15.Signal}");

    sb.AppendLine($"-----------------------------------");
    sb.AppendLine($"📌 نتیجه نهایی: {finalSignal}");

    if (finalSignal.Contains("LONG") || finalSignal.Contains("SHORT"))
    {
        sb.AppendLine($"🎯 Entry: {FormatNumber(result5.Entry)}");
        sb.AppendLine($"🎯 TP1: {FormatNumber(result5.TP1)}");
        sb.AppendLine($"🎯 TP2: {FormatNumber(result5.TP2)}");
        sb.AppendLine($"🛑 SL: {FormatNumber(result5.SL)}");

        if (result5.Warnings.Any())
        {
            sb.AppendLine("⚠️ هشدارها:");
            foreach (var w in result5.Warnings)
                sb.AppendLine("   - " + w);
        }
    }

    return sb.ToString();
}

	private string FormatNumber(decimal value)
{
    // فرمت استاندارد با هزارگان و دو رقم اعشار
    string formatted = string.Format("{0:N2}", value);
    // تبدیل نقطه اعشار به "/"
    formatted = formatted.Replace(".", "/");
    return formatted;
}

}
