using Microsoft.EntityFrameworkCore;

namespace OpsPilot.Application.Common.Models;

public static class DocumentNumbers
{
    /// <summary>
    /// Next sequential number such as SO-10042. Numbers are zero-padded so they sort as strings;
    /// the unique index on the column guards against two concurrent creates taking the same number.
    /// </summary>
    public static async Task<string> NextAsync(IQueryable<string> existingNumbers, string prefix, int firstNumber, CancellationToken cancellationToken)
    {
        var last = await existingNumbers
            .Where(n => n.StartsWith(prefix))
            .OrderByDescending(n => n)
            .FirstOrDefaultAsync(cancellationToken);

        var next = last is not null && int.TryParse(last[prefix.Length..], out var current) ? current + 1 : firstNumber;
        return $"{prefix}{next}";
    }
}
