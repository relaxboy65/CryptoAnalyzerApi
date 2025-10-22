namespace CryptoAnalyzerApi.Services;

public static class FormatUtils
{
    public static string ToSlashDecimal(decimal value, int decimals = 2)
    {
        var s = Math.Round(value, decimals).ToString($"N{decimals}");
        return s.Replace('.', '/');
    }
}
