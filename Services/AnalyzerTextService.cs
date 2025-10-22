using System.Text;
using CryptoAnalyzerApi.Models;

namespace CryptoAnalyzerApi.Services;

public class AnalyzerTextService
{
    private readonly CryptoCompareClient _client = new();

    public async Task<string> AnalyzeTextAsync(string symbol)
    {
        var data5 = await _client.GetHistoricAsync(symbol, 5, 300);
        var data15 = await _client.GetHistoricAsync(symbol, 15, 300);

        var block5 = BuildBlock(data5, "5m");
        var block15 = BuildBlock(data15, "15m");

        // --- ترکیب سیگنال‌ها ---
        string finalSignal = "🤔 بدون قطعیت (HOLD)";
        if (block5.Signal.Contains("LONG") && block15.Signal.Contains("LONG"))
            finalSignal = "✅ خرید (LONG)";
        else if (block5.Signal.Contains("SHORT") && block15.Signal.Contains("SHORT"))
            finalSignal = "🚨 فروش (SHORT)";

        // --- فیلتر روند کلان (trendUp) ---
        bool? trendUp = null;
        if (block5.EMA50 > 0 && block15.EMA50 > 0)
            trendUp = block15.EMA50 > block5.EMA50;

        if (trendUp.HasValue)
        {
            if (trendUp.Value && finalSignal.Contains("SHORT"))
                finalSignal = "🤔 خلاف روند صعودی → HOLD";
            else if (!trendUp.Value && finalSignal.Contains("LONG"))
                finalSignal = "🤔 خلاف روند نزولی → HOLD";
        }

        // --- محاسبه سطوح فقط اگر LONG یا SHORT باشد ---
        var entry = block5.LastPrice;
        var atr = block5.ATR;
        decimal sl = 0, tp1 = 0, tp2 = 0;

        if (finalSignal.Contains("LONG"))
        {
            sl = entry - atr;
            tp1 = entry + atr * 2;
            tp2 = entry + atr * 3;
        }
        else if (finalSignal.Contains("SHORT"))
        {
            sl = entry + atr;
            tp1 = entry - atr * 2;
            tp2 = entry - atr * 3;
        }

        var sb = new StringBuilder();

        // --- بلاک 5m ---
        sb.AppendLine($"📊 تایم‌فریم {block5.Label}");
        sb.AppendLine($"   RSI: {FormatUtils.ToSlashDecimal(block5.RSI)}");
        sb.AppendLine($"   EMA14/50: {FormatUtils.ToSlashDecimal(block5.EMA14)} / {FormatUtils.ToSlashDecimal(block5.EMA50)}");
        sb.AppendLine($"   MACD: {FormatUtils.ToSlashDecimal(block5.MACD)} / {FormatUtils.ToSlashDecimal(block5.MACDSignal)}");
        sb.AppendLine($"   سیگنال: {block5.Signal}");

        // --- بلاک 15m ---
        sb.AppendLine($"📊 تایم‌فریم {block15.Label}");
        sb.AppendLine($"   RSI: {FormatUtils.ToSlashDecimal(block15.RSI)}");
        sb.AppendLine($"   EMA14/50: {FormatUtils.ToSlashDecimal(block15.EMA14)} / {FormatUtils.ToSlashDecimal(block15.EMA50)}");
        sb.AppendLine($"   MACD: {FormatUtils.ToSlashDecimal(block15.MACD)} / {FormatUtils.ToSlashDecimal(block15.MACDSignal)}");
        sb.AppendLine($"   سیگنال: {block15.Signal}");

        // --- نتیجه نهایی ---
        sb.AppendLine($"-----------------------------------");
        sb.AppendLine($"📌 نتیجه نهایی: {finalSignal}");

        // --- اگر LONG یا SHORT بود، سطوح و هشدارها چاپ شوند ---
        if (finalSignal.Contains("LONG") || finalSignal.Contains("SHORT"))
        {
            sb.AppendLine($"------------------------------");
            sb.AppendLine($"📏 ATR(14): {FormatUtils.ToSlashDecimal(atr)}");
            sb.AppendLine($"------------------------------");
            sb.AppendLine($"🎯 نقطه ورود: {FormatUtils.ToSlashDecimal(entry)}");
            sb.AppendLine($"🎯 TP1: {FormatUtils.ToSlashDecimal(tp1)}");
            sb.AppendLine($"🎯 TP2: {FormatUtils.ToSlashDecimal(tp2)}");
            sb.AppendLine($"🛑 SL: {FormatUtils.ToSlashDecimal(sl)}");

            // --- هشدارها ---
            var warnings = BuildWarnings(finalSignal, block5.RSI, block15.RSI, block5.EMA50, atr, entry, sl, tp1);
            if (warnings.Count > 0)
            {
                sb.AppendLine("⚠️ هشدارها:");
                foreach (var w in warnings)
                    sb.AppendLine("   - " + w);
            }
        }

        return sb.ToString();
    }

    private BlockMetrics BuildBlock(List<Candle> candles, string label)
    {
        var result = new BlockMetrics { Label = label, Signal = "🤝 HOLD" };
        if (candles == null || candles.Count < 50) return result;

        var closes = candles.Select(c => c.Close).ToList();
        var rsiList = IndicatorService.CalculateRSI(closes, 14);
        var ema14List = IndicatorService.CalculateEMA(closes, 14);
        var ema50List = IndicatorService.CalculateEMA(closes, 50);
        var (macdList, sigList) = IndicatorService.CalculateMACD(closes);

        if (!rsiList.Any() || !ema14List.Any() || !ema50List.Any() || !macdList.Any() || !sigList.Any())
            return result;

        result.RSI = rsiList.Last();
        result.EMA14 = ema14List.Last();
        result.EMA50 = ema50List.Last();
        result.MACD = macdList.Last();
        result.MACDSignal = sigList.Last();
        result.ATR = IndicatorService.CalculateATR(candles);
        result.LastPrice = closes.Last();

        int score = IndicatorService.ScoreConfluence(result.RSI, result.EMA14, result.EMA50, result.MACD, result.MACDSignal);
        result.Signal = score >= 2 ? "📈 LONG" : (score <= -2 ? "📉 SHORT" : "🤝 HOLD");

        return result;
    }

    private List<string> BuildWarnings(string finalSignal, decimal rsi5, decimal rsi15,
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

    private class BlockMetrics
    {
        public string Label { get; set; } = "";
        public decimal RSI { get; set; }
        public decimal EMA14 { get; set; }
        public decimal EMA50 { get; set; }
        public decimal MACD { get; set; }
        public decimal MACDSignal { get; set; }
        public decimal ATR { get; set; }
        public decimal LastPrice { get; set; }
        public string Signal { get; set; } = "🤝 HOLD";
    }
}
