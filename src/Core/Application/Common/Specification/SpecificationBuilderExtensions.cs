using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Ardalis.Specification;
using NightMarket.WebApi.Application.Common.Models;

namespace NightMarket.WebApi.Application.Common.Specification;

public static class SpecificationBuilderExtensions
{
    public static ISpecificationBuilder<T> SearchBy<T>(this ISpecificationBuilder<T> query, BaseFilter filter)
    {
        bool hasSearch = false;
        
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            query.SearchByKeyword(filter.Keyword);
            hasSearch = true;
        }

        if (filter.AdvancedSearch is not null)
        {
            query.AdvancedSearch(filter.AdvancedSearch);
            hasSearch = true;
        }

        if (filter.AdvancedFilter is not null)
        {
            query.AdvancedFilter(filter.AdvancedFilter);
            hasSearch = true;
        }

        return query;
    }

    public static ISpecificationBuilder<T> PaginateBy<T>(this ISpecificationBuilder<T> query, PaginationFilter filter)
    {
        if (filter.PageNumber <= 0) filter.PageNumber = 1;

        if (filter.PageSize > 0)
        {
            query.Skip((filter.PageNumber - 1) * filter.PageSize)
                 .Take(filter.PageSize);
        }

        if (filter.HasOrderBy())
        {
            query.OrderBy(filter.OrderBy!);
        }

        return query;
    }

    public static ISpecificationBuilder<T> SearchByKeyword<T>(this ISpecificationBuilder<T> query, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return query;

        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.IsPrimitive || p.PropertyType == typeof(string) || p.PropertyType == typeof(decimal))
            .ToList();

        if (!properties.Any()) return query;

        var param = Expression.Parameter(typeof(T), "x");
        Expression? combined = null;

        var k = keyword.ToLower();
        var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) })!;
        var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes)!;
        var toStringMethod = typeof(object).GetMethod("ToString")!;

        foreach (var prop in properties)
        {
            var propExpr = Expression.Property(param, prop);
            Expression stringExpr;

            if (prop.PropertyType == typeof(string))
            {
                stringExpr = propExpr;
            }
            else
            {
                stringExpr = Expression.Condition(
                    Expression.Equal(Expression.Convert(propExpr, typeof(object)), Expression.Constant(null, typeof(object))),
                    Expression.Constant(null, typeof(string)),
                    Expression.Call(propExpr, toStringMethod)
                );
            }

            var lowerExpr = Expression.Call(stringExpr, toLowerMethod);
            var containsExpr = Expression.Call(lowerExpr, containsMethod, Expression.Constant(k));

            var checkNullExpr = Expression.AndAlso(
                Expression.NotEqual(stringExpr, Expression.Constant(null, typeof(string))),
                containsExpr
            );

            combined = combined == null ? checkNullExpr : Expression.OrElse(combined, checkNullExpr);
        }

        if (combined != null)
        {
            query.Where(Expression.Lambda<Func<T, bool>>(combined, param));
        }

        return query;
    }

    public static ISpecificationBuilder<T> AdvancedSearch<T>(this ISpecificationBuilder<T> query, Search search)
    {
        if (string.IsNullOrWhiteSpace(search.Keyword)) return query;
        if (search.Fields == null || !search.Fields.Any()) return query.SearchByKeyword(search.Keyword);

        var param = Expression.Parameter(typeof(T), "x");
        Expression? combined = null;

        var k = search.Keyword.ToLower();
        var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) })!;
        var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes)!;
        var toStringMethod = typeof(object).GetMethod("ToString")!;

        foreach (var field in search.Fields)
        {
            try
            {
                var propExpr = GetPropertyExpression(field, param);
                Expression stringExpr;

                if (propExpr.Type == typeof(string))
                {
                    stringExpr = propExpr;
                }
                else
                {
                    stringExpr = Expression.Condition(
                        Expression.Equal(Expression.Convert(propExpr, typeof(object)), Expression.Constant(null, typeof(object))),
                        Expression.Constant(null, typeof(string)),
                        Expression.Call(propExpr, toStringMethod)
                    );
                }

                var lowerExpr = Expression.Call(stringExpr, toLowerMethod);
                var containsExpr = Expression.Call(lowerExpr, containsMethod, Expression.Constant(k));

                var checkNullExpr = Expression.AndAlso(
                    Expression.NotEqual(stringExpr, Expression.Constant(null, typeof(string))),
                    containsExpr
                );

                combined = combined == null ? checkNullExpr : Expression.OrElse(combined, checkNullExpr);
            }
            catch
            {
                // Ignore invalid fields in advanced search
                continue;
            }
        }

        if (combined != null)
        {
            query.Where(Expression.Lambda<Func<T, bool>>(combined, param));
        }

        return query;
    }

    public static ISpecificationBuilder<T> AdvancedFilter<T>(this ISpecificationBuilder<T> query, Filter filter)
    {
        var param = Expression.Parameter(typeof(T), "x");
        var expr = CreateFilterExpression(filter, param);
        
        if (expr != null)
        {
            query.Where(Expression.Lambda<Func<T, bool>>(expr, param));
        }
        
        return query;
    }

    private static Expression? CreateFilterExpression(Filter filter, ParameterExpression param)
    {
        if (!string.IsNullOrWhiteSpace(filter.Logic))
        {
            if (filter.Filters == null || !filter.Filters.Any()) return null;

            Expression? combined = null;
            foreach (var childFilter in filter.Filters)
            {
                var childExpr = CreateFilterExpression(childFilter, param);
                if (childExpr == null) continue;

                if (combined == null)
                {
                    combined = childExpr;
                }
                else
                {
                    combined = filter.Logic.ToLower() switch
                    {
                        "and" => Expression.AndAlso(combined, childExpr),
                        "or"  => Expression.OrElse(combined, childExpr),
                        "xor" => Expression.ExclusiveOr(combined, childExpr),
                        _ => throw new Exception("Invalid logic")
                    };
                }
            }
            return combined;
        }

        if (string.IsNullOrWhiteSpace(filter.Field) || string.IsNullOrWhiteSpace(filter.Operator)) return null;

        var memberExpr = GetPropertyExpression(filter.Field, param);
        var constExpr = BuildConstant(filter.Field, filter.Value, memberExpr.Type);
        
        return BuildCompareExpression(memberExpr, constExpr, filter.Operator.ToLower());
    }

    private static Expression GetPropertyExpression(string propertyName, ParameterExpression parameter)
    {
        Expression expression = parameter;
        foreach (var member in propertyName.Split('.'))
        {
            expression = Expression.PropertyOrField(expression, member);
        }
        return expression;
    }

    private static Expression BuildConstant(string field, object? value, Type propertyType)
    {
        if (value is null)
            return Expression.Constant(null, propertyType);

        var json = (JsonElement)value;

        if (propertyType.IsEnum)
        {
            var s = json.GetString();
            if (!Enum.TryParse(propertyType, s, true, out var parsed))
                throw new Exception($"Value {s} invalid for {field}");
            return Expression.Constant(parsed, propertyType);
        }

        if (propertyType == typeof(Guid) || propertyType == typeof(Guid?))
        {
            var s = json.GetString();
            if (!Guid.TryParse(s, out var g))
                throw new Exception($"Value {s} invalid for {field}");
            
            var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            return Expression.Constant(g, targetType);
        }

        if (propertyType == typeof(string))
            return Expression.Constant(json.GetString(), propertyType);

        if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?))
        {
            var s = json.GetString();
            if (!DateTime.TryParse(s, out var dt))
                throw new Exception($"Value {s} invalid for {field}");
                
            var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            return Expression.Constant(dt, targetType);
        }

        var uType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        var rType = Convert.ChangeType(json.GetRawText(), uType);
        return Expression.Constant(rType, propertyType);
    }

    private static Expression BuildCompareExpression(Expression memberExpr, Expression constExpr, string op)
    {
        if (memberExpr.Type == typeof(string) && constExpr.Type == typeof(string))
        {
            var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes)!;
            var safeMember = Expression.Condition(
                Expression.Equal(memberExpr, Expression.Constant(null, typeof(string))),
                Expression.Constant(string.Empty),
                memberExpr
            );
            
            memberExpr = Expression.Call(safeMember, toLowerMethod);
            
            var safeConst = Expression.Condition(
                Expression.Equal(constExpr, Expression.Constant(null, typeof(string))),
                Expression.Constant(string.Empty),
                constExpr
            );
            
            constExpr = Expression.Call(safeConst, toLowerMethod);
        }

        return op switch
        {
            "eq" => Expression.Equal(memberExpr, constExpr),
            "neq" => Expression.NotEqual(memberExpr, constExpr),
            "gt" => Expression.GreaterThan(memberExpr, constExpr),
            "gte" => Expression.GreaterThanOrEqual(memberExpr, constExpr),
            "lt" => Expression.LessThan(memberExpr, constExpr),
            "lte" => Expression.LessThanOrEqual(memberExpr, constExpr),
            "contains" => Expression.Call(memberExpr, typeof(string).GetMethod("Contains", new[] { typeof(string) })!, constExpr),
            "startswith" => Expression.Call(memberExpr, typeof(string).GetMethod("StartsWith", new[] { typeof(string) })!, constExpr),
            "endswith" => Expression.Call(memberExpr, typeof(string).GetMethod("EndsWith", new[] { typeof(string) })!, constExpr),
            _ => throw new Exception($"Invalid operator {op}")
        };
    }

    public static ISpecificationBuilder<T> OrderBy<T>(this ISpecificationBuilder<T> query, string[] orderByFields)
    {
        if (orderByFields == null || orderByFields.Length == 0) return query;

        IOrderedSpecificationBuilder<T>? orderedQuery = null;

        foreach (var field in orderByFields)
        {
            var parts = field.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var propertyName = parts[0];
            var descending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

            var param = Expression.Parameter(typeof(T), "x");
            var member = GetPropertyExpression(propertyName, param);
            var converted = Expression.Convert(member, typeof(object));
            var lambda = Expression.Lambda<Func<T, object?>>(converted, param);

            if (orderedQuery == null)
            {
                orderedQuery = descending
                    ? query.OrderByDescending(lambda)
                    : query.OrderBy(lambda);
            }
            else
            {
                orderedQuery = descending
                    ? orderedQuery.ThenByDescending(lambda)
                    : orderedQuery.ThenBy(lambda);
            }
        }

        return query;
    }
}
