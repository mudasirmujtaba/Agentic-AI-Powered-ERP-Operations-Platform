namespace OpsPilot.Application.Common.Models;

public static class Codes
{
    /// <summary>Business codes are stored upper-case and trimmed so uniqueness checks are case-insensitive on every provider.</summary>
    public static string Normalize(string code) => code.Trim().ToUpperInvariant();
}
