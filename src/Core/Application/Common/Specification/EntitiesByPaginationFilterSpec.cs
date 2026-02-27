using NightMarket.WebApi.Application.Common.Models;

namespace NightMarket.WebApi.Application.Common.Specification;

/// <summary>
/// Base spec với search + filter + pagination
/// </summary>
public class EntitiesByPaginationFilterSpec<T> : EntitiesByBaseFilterSpec<T>
{
    public EntitiesByPaginationFilterSpec(PaginationFilter filter)
        : base(filter) =>
        Query.PaginateBy(filter);
}

/// <summary>
/// Base spec với search + filter + pagination và projection
/// </summary>
public class EntitiesByPaginationFilterSpec<T, TResult> : EntitiesByBaseFilterSpec<T, TResult>
{
    public EntitiesByPaginationFilterSpec(PaginationFilter filter)
        : base(filter) =>
        Query.PaginateBy(filter);
}
