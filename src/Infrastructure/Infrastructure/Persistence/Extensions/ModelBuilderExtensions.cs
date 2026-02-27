using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace NightMarket.WebApi.Infrastructure.Persistence.Extensions;

/// <summary>
/// Extension methods for ModelBuilder to work with global query filters on interfaces.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Apply global query filter cho tất cả entities implement một interface.
    /// </summary>
    public static void AppendGlobalQueryFilter<TInterface>(
        this ModelBuilder modelBuilder,
        Expression<Func<TInterface, bool>> filterExpression)
    {
        var entities = modelBuilder.Model.GetEntityTypes();

        foreach (var entityType in entities)
        {
            var clrType = entityType.ClrType;

            if (!typeof(TInterface).IsAssignableFrom(clrType))
                continue;

            var parameter = Expression.Parameter(clrType, "e");
            var castExpression = Expression.Convert(parameter, typeof(TInterface));
            var invokeExpression = Expression.Invoke(filterExpression, castExpression);
            var lambdaExpression = Expression.Lambda(invokeExpression, parameter);

            var existingFilter = entityType.GetQueryFilter();

            if (existingFilter != null)
            {
                var existingParameter = existingFilter.Parameters[0];
                var newParameter = lambdaExpression.Parameters[0];
                var leftExpression = new ReplacingExpressionVisitor(
                    new[] { existingParameter },
                    new[] { newParameter })
                    .Visit(existingFilter.Body);

                var combinedBody = Expression.AndAlso(leftExpression, lambdaExpression.Body);
                var combinedLambda = Expression.Lambda(combinedBody, newParameter);
                entityType.SetQueryFilter(combinedLambda);
            }
            else
            {
                entityType.SetQueryFilter(lambdaExpression);
            }
        }
    }
}
