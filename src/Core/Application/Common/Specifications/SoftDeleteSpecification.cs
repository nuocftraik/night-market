using Ardalis.Specification;
using NightMarket.WebApi.Domain.Common.Contracts;

namespace NightMarket.WebApi.Application.Common.Specifications;

/// <summary>
/// Specification base cho soft delete queries.
/// </summary>
public abstract class SoftDeleteSpecification<T> : Specification<T>
    where T : class, ISoftDelete
{
    /// <summary>
    /// Include deleted entities trong query (disable global filter).
    /// </summary>
    protected void IncludeDeleted()
    {
        Query.IgnoreQueryFilters();
    }

    /// <summary>
    /// Query chỉ deleted entities.
    /// </summary>
    protected void OnlyDeleted()
    {
        Query.IgnoreQueryFilters()
            .Where(e => e.DeletedOn != null);
    }
}
