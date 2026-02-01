namespace GeminiSimulator.DesignPatterns
{
    public interface IReportable
    {
        Dictionary<string, ReportField> GetReportData();
    }
    public record ReportField(
    object Value,
    string CssClass = "",
    bool IsBold = false,
    string TextSize = "inherit",
    string Color = "inherit"
);
}
