using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace OpsPilot.Application.Common.Models;

public static class QueryableExtensions
{
    public static IQueryable<T> ApplySort<T>(
        this IQueryable<T> source,
        PagedQuery query,
        IReadOnlyDictionary<string, Expression<Func<T, object>>> sortMap,
        string defaultSortKey)
    {
        var key = query.SortBy is not null && sortMap.ContainsKey(query.SortBy) ? query.SortBy : defaultSortKey;
        var keySelector = sortMap[key];

        return query.IsDescending ? source.OrderByDescending(keySelector) : source.OrderBy(keySelector);
    }

    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> source,
        PagedQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, PagedQuery.MaxPageSize);

        var totalCount = await source.CountAsync(cancellationToken);
        var items = await source
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, page, pageSize, totalCount);
    }
}
