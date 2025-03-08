using System.Linq.Expressions;
using System.Reflection;

namespace SearchFilters;

public static class FilterQueryExtensions
{
    private static Expression<Func<TSource, bool>> FilterString<TSource, TProperty>(
            Expression<Func<TSource, TProperty>> propertySelector,
            DataFilter<TProperty>                filter)
    {
        var valueExpr = Expression.Constant(filter.Values.First());
        var methodInfo = filter.ComparisonName switch
                         {
                                 FilterComparisons.Contains or FilterComparisons.NotContains =>
                                         typeof(TProperty).GetMethod("Contains", new[] { typeof(TProperty) }),
                                 FilterComparisons.Equals or FilterComparisons.NotEqual =>
                                         typeof(TProperty).GetMethod("Equals", new[] { typeof(TProperty) }),
                                 FilterComparisons.StartsWith =>
                                         typeof(TProperty).GetMethod("StartsWith", new[] { typeof(TProperty) }),
                                 FilterComparisons.EndsWith =>
                                         typeof(TProperty).GetMethod("EndsWith", new[] { typeof(TProperty) }),
                                 _ => null
                         }
                         ?? throw new InvalidOperationException(
                                 $"Filter comparison '{filter.ComparisonName}' not supported."
                         );

        Expression filterExpr = Expression.Call(
                propertySelector.Body,
                methodInfo,
                valueExpr
        );

        // Niega la expresión donde corresponda.
        if (filter.ComparisonName is FilterComparisons.NotContains or FilterComparisons.NotEqual)
        {
            filterExpr = Expression.Not(filterExpr);
        }

        return Expression.Lambda<Func<TSource, bool>>(filterExpr, propertySelector.Parameters[0]);
    }

    private static Expression<Func<TSource, bool>> FilterNumber<TSource, TProperty>(
            Expression<Func<TSource, TProperty>> propertySelector,
            DataFilter<TProperty>                filter)
    {
        var value1Expr = Expression.Constant(filter.Values.First());
        Expression value2Expr = filter.Values.Count() > 1 && filter.Values.ElementAt(1) != null
                ? Expression.Constant(filter.Values.ElementAt(1))
                : Expression.MakeMemberAccess(null, typeof(TProperty).GetMember("MaxValue").First());
        var filterExpr = filter.ComparisonName switch
                         {
                                 FilterComparisons.Equals =>
                                         Expression.Equal(propertySelector.Body, value1Expr),
                                 FilterComparisons.NotEqual =>
                                         Expression.NotEqual(propertySelector.Body, value1Expr),
                                 FilterComparisons.LessThan =>
                                         Expression.LessThan(propertySelector.Body, value1Expr),
                                 FilterComparisons.LessThanOrEqual =>
                                         Expression.LessThanOrEqual(propertySelector.Body, value1Expr),
                                 FilterComparisons.GreaterThan =>
                                         Expression.GreaterThan(propertySelector.Body, value1Expr),
                                 FilterComparisons.GreaterThanOrEqual =>
                                         Expression.GreaterThanOrEqual(propertySelector.Body, value1Expr),
                                 FilterComparisons.IsInRange =>
                                         Expression.And(
                                                 Expression.GreaterThanOrEqual(propertySelector.Body, value1Expr),
                                                 Expression.LessThanOrEqual(propertySelector.Body, value2Expr)
                                         ),
                                 _ => null
                         }
                         ?? throw new InvalidOperationException(
                                 $"Filter comparison '{filter.ComparisonName}' not supported."
                         );

        return Expression.Lambda<Func<TSource, bool>>(filterExpr, propertySelector.Parameters[0]);
    }

    private static Expression<Func<TSource, bool>> FilterDateTime<TSource, TProperty>(
            Expression<Func<TSource, TProperty>> propertySelector,
            DataFilter<TProperty>                filter,
            bool                                 ignoreTime)
    {
        Expression value1Expr = Expression.Constant(filter.Values.First());
        Expression value2Expr = filter.Values.Count() > 1 && filter.Values.ElementAt(1) != null
                ? Expression.Constant(filter.Values.ElementAt(1))
                : Expression.MakeMemberAccess(null, typeof(TProperty).GetMember("MaxValue").First());

        if (ignoreTime)
        {
            value1Expr = Expression.MakeMemberAccess(value1Expr, typeof(TProperty).GetMember("Date").First());
            value2Expr = Expression.MakeMemberAccess(value2Expr, typeof(TProperty).GetMember("Date").First());
        }

        var filterExpr = filter.ComparisonName switch
                         {
                                 FilterComparisons.Equals =>
                                         Expression.Equal(propertySelector.Body, value1Expr),
                                 FilterComparisons.NotEqual =>
                                         Expression.NotEqual(propertySelector.Body, value1Expr),
                                 FilterComparisons.LessThan or FilterComparisons.LessThanOrEqual =>
                                         Expression.LessThanOrEqual(propertySelector.Body, value1Expr),
                                 FilterComparisons.GreaterThan or FilterComparisons.GreaterThanOrEqual =>
                                         Expression.GreaterThanOrEqual(propertySelector.Body, value1Expr),
                                 FilterComparisons.IsInRange =>
                                         Expression.And(
                                                 Expression.GreaterThanOrEqual(propertySelector.Body, value1Expr),
                                                 Expression.LessThanOrEqual(propertySelector.Body, value2Expr)
                                         ),
                                 _ => null
                         }
                         ?? throw new InvalidOperationException(
                                 $"Filter comparison '{filter.ComparisonName}' not supported."
                         );

        return Expression.Lambda<Func<TSource, bool>>(filterExpr, propertySelector.Parameters[0]);
    }

    private static Expression<Func<TSource, bool>> FilterValues<TSource, TProperty>(
            Expression<Func<TSource, TProperty>> propertySelector,
            DataFilter<TProperty>                filter)
    {
        var valuesExpr = Expression.Constant(filter.Values);
        var methodInfo = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                           .Where(method => method.Name == "Contains"        &&
                                                            method.IsGenericMethodDefinition &&
                                                            method.GetParameters().Length == 2)
                                           .Select(method => method.MakeGenericMethod(typeof(TProperty)))
                                           .FirstOrDefault();
        Expression filterExpression = Expression.Call(
                methodInfo!,
                valuesExpr,
                propertySelector.Body);

        if (filter.ComparisonName == FilterComparisons.IsNotOneOf)
        {
            filterExpression = Expression.Not(filterExpression);
        }

        return Expression.Lambda<Func<TSource, bool>>(filterExpression, propertySelector.Parameters[0]);
    }

    public static IQueryable<TSource> Filter<TSource, TProperty>(
            this IQueryable<TSource>             query,
            Expression<Func<TSource, TProperty>> propertySelector,
            DataFilter<TProperty>?               filter,
            FilterOptions?                       options = null)
    {
        var canHaveEmptyValues = new[] { FilterComparisons.IsOneOf, FilterComparisons.IsNotOneOf };

        if (filter is null || (!filter.Values.Any() && !canHaveEmptyValues.Contains(filter.ComparisonName)))
        {
            return query;
        }

        options ??= new FilterOptions();

        if (filter.ComparisonName is FilterComparisons.IsOneOf or FilterComparisons.IsNotOneOf)
        {
            query = query.Where(FilterValues(propertySelector, filter));
        }
        else
        {
            switch (Type.GetTypeCode(typeof(TProperty)))
            {
                case TypeCode.String:
                    query = query.Where(FilterString(propertySelector, filter));

                    break;

                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    query = query.Where(FilterNumber(propertySelector, filter));

                    break;

                case TypeCode.DateTime:
                    query = query.Where(FilterDateTime(propertySelector, filter, options.IgnoreTime));

                    break;

                default: throw new InvalidOperationException("Property type not supported.");
            }
        }

        return query;
    }
}
