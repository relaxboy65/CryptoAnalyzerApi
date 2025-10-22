namespace CryptoAnalyzerApi.Models;

public class Candle
{
    public long TimeUnix { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
}
