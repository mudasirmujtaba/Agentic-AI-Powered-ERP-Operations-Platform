namespace OpsPilot.Domain.Insights;

/// <summary>The stored result of a scheduled analysis, e.g. the nightly inventory risk scan (design doc §40).</summary>
public class InsightReport
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }

    /// <summary>Markdown narrative (AI-written when the model is available, otherwise a deterministic digest).</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Number of items flagged, e.g. products at risk.</summary>
    public int ItemCount { get; set; }

    /// <summary>The structured findings the summary was written from.</summary>
    public string DataJson { get; set; } = "[]";

    public bool AiGenerated { get; set; }
}

public static class InsightKinds
{
    public const string InventoryRisk = "InventoryRisk";
}
