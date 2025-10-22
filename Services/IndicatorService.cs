using CryptoAnalyzerApi.Models;
namespace CryptoAnalyzerApi.Services;

public static class IndicatorService
{
    public static List<decimal> CalculateRSI(List<decimal> closes, int period)
    {
        var rsi = new List<decimal>();
        if (closes.Count <= period) return rsi;

        var gains = new List<decimal>();
        var losses = new List<decimal>();

        for (int i = 1; i < closes.Count; i++)
        {
            var diff = closes[i] - closes[i - 1];
            gains.Add(diff > 0 ? diff : 0);
            losses.Add(diff < 0 ? -diff : 0);
        }

        decimal avgGain = gains.Take(period).Average();
        decimal avgLoss = losses.Take(period).Average();
        decimal rs0 = avgLoss == 0 ? decimal.MaxValue : avgGain / avgLoss;
        rsi.Add(100 - (100 / (1 + rs0)));

        for (int i = period; i < gains.Count; i++)
        {
            avgGain = ((avgGain * (period - 1)) + gains[i]) / period;
            avgLoss = ((avgLoss * (period - 1)) + losses[i]) / period;

            decimal rs = avgLoss == 0 ? decimal.MaxValue : avgGain / avgLoss;
            rsi.Add(100 - (100 / (1 + rs)));
        }

        return rsi;
    }

    public static List<decimal> CalculateEMA(List<decimal> prices, int period)
    {
        var ema = new List<decimal>();
        if (prices.Count < period) return ema;

        decimal sma = prices.Take(period).Average();
        ema.Add(sma);
        decimal k = 2m / (period + 1);

        for (int i = period; i < prices.Count; i++)
        {
            decimal next = ((prices[i] - ema.Last()) * k) + ema.Last();
            ema.Add(next);
        }
        return ema;
    }

    public static (List<decimal> macd, List<decimal> signal) CalculateMACD(List<decimal> closes)
    {
        var ema12 = CalculateEMA(closes, 12);
        var ema26 = CalculateEMA(closes, 26);
        if (!ema12.Any() || !ema26.Any()) return (new List<decimal>(), new List<decimal>());

        int min = Math.Min(ema12.Count, ema26.Count);
        var macd = ema12.Skip(ema12.Count - min).Zip(ema26.Skip(ema26.Count - min), (a, b) => a - b).ToList();
        var signal = CalculateEMA(macd, 9);
        return (macd, signal);
    }

    public static decimal CalculateATR(List<Candle> candles, int period = 14)
    {
        if (candles.Count < period + 1) return 0;

        var trueRanges = new List<decimal>();

        for (int i = 1; i < candles.Count; i++)
        {
            decimal high = candles[i].High;
            decimal low = candles[i].Low;
            decimal prevClose = candles[i - 1].Close;

            decimal tr1 = high - low;
            decimal tr2 = Math.Abs(high - prevClose);
            decimal tr3 = Math.Abs(low - prevClose);

            decimal trueRange = Math.Max(tr1, Math.Max(tr2, tr3));
            trueRanges.Add(trueRange);
        }

        return trueRanges.Skip(trueRanges.Count - period).Take(period).Average();
    }

    public static int ScoreConfluence(decimal rsi, decimal emaShort, decimal emaLong, decimal macd, decimal macdSignal)
    {
        int score = 0;

        decimal emaDiff = emaShort - emaLong;
        if (emaDiff > 0.5m) score += 2;
        else if (emaDiff < -0.5m) score -= 2;

        decimal macdDiff = macd - macdSignal;
        if (macdDiff > 0.3m) score += 2;
        else if (macdDiff < -0.3m) score -= 2;

        if (rsi < 40) score += 1;
        else if (rsi > 60) score -= 1;

        return score;
    }
}
