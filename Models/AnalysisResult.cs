using System.Text.Json.Serialization;

public class AnalysisResult
{
    public string Symbol { get; set; } = "";
    public string Timeframe { get; set; } = "";
    public string Signal { get; set; } = "";
    public decimal Entry { get; set; }
    public decimal SL { get; set; }

    [JsonPropertyName("TP1")]
    public decimal TP1 { get; set; }

    [JsonPropertyName("TP2")]
    public decimal TP2 { get; set; }

    public decimal ATR { get; set; }
    public decimal RiskPerUnit { get; set; }
    public decimal RRR { get; set; }
    public List<string> Warnings { get; set; } = new();
    public string Verdict { get; set; } = "";
}
