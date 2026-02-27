using NightMarket.WebApi.Application.Common.Interfaces;
using NightMarket.WebApi.Domain.Auditing;
using NightMarket.WebApi.Domain.Common.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace NightMarket.WebApi.Infrastructure.Auditing;

/// <summary>
/// Helper class to build audit trail entries from EF Core EntityEntry.
/// </summary>
public static class AuditTrail
{
    public static Trail? TransformEntry(
        EntityEntry entry,
        Guid userId,
        ISerializerService serializer)
    {
        // Skip non-auditable entities
        if (entry.Entity is Trail) return null;

        var trail = new Trail
        {
            TableName = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name,
            UserId = userId,
            DateTime = DateTime.UtcNow
        };

        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();
        var affectedColumns = new List<string>();

        foreach (var property in entry.Properties)
        {
            var propertyName = property.Metadata.Name;

            if (property.Metadata.IsPrimaryKey())
                continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    newValues[propertyName] = property.CurrentValue;
                    affectedColumns.Add(propertyName);
                    trail.Type = TrailType.Create;
                    break;

                case EntityState.Modified:
                    if (!property.IsModified) continue;

                    // Soft delete detection: DeletedOn null → value
                    if (entry.Entity is ISoftDelete &&
                        propertyName == nameof(ISoftDelete.DeletedOn) &&
                        property.OriginalValue == null &&
                        property.CurrentValue != null)
                    {
                        trail.Type = TrailType.Delete;
                    }
                    else if (trail.Type != TrailType.Delete)
                    {
                        trail.Type = TrailType.Update;
                    }

                    oldValues[propertyName] = property.OriginalValue;
                    newValues[propertyName] = property.CurrentValue;
                    affectedColumns.Add(propertyName);
                    break;

                case EntityState.Deleted:
                    oldValues[propertyName] = property.OriginalValue;
                    affectedColumns.Add(propertyName);
                    trail.Type = TrailType.Delete;
                    break;
            }
        }

        trail.OldValues = oldValues.Count > 0 ? serializer.Serialize(oldValues) : null;
        trail.NewValues = newValues.Count > 0 ? serializer.Serialize(newValues) : null;
        trail.AffectedColumns = affectedColumns.Count > 0 ? string.Join(",", affectedColumns) : null;

        // Primary key
        var keyValues = new Dictionary<string, object?>();
        foreach (var keyProp in entry.Properties.Where(p => p.Metadata.IsPrimaryKey()))
        {
            keyValues[keyProp.Metadata.Name] = keyProp.CurrentValue;
        }

        trail.PrimaryKey = serializer.Serialize(keyValues);

        return trail;
    }
}
