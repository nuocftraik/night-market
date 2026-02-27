using NightMarket.WebApi.Domain.Common.Contracts;

namespace NightMarket.WebApi.Application.Common.Extensions;

/// <summary>
/// Extension methods cho soft delete operations.
/// </summary>
public static class SoftDeleteExtensions
{
    /// <summary>
    /// Restore deleted entity bằng cách set DeletedOn/DeletedBy = null.
    /// </summary>
    public static void Restore<T>(this T entity) where T : ISoftDelete
    {
        entity.DeletedOn = null;
        entity.DeletedBy = null;
    }

    /// <summary>
    /// Check if entity đã bị soft delete.
    /// </summary>
    public static bool IsDeleted<T>(this T entity) where T : ISoftDelete
    {
        return entity.DeletedOn.HasValue;
    }

    /// <summary>
    /// Soft delete entity manually.
    /// </summary>
    public static void SoftDelete<T>(this T entity, Guid userId) where T : ISoftDelete
    {
        entity.DeletedOn = DateTime.UtcNow;
        entity.DeletedBy = userId;
    }
}
