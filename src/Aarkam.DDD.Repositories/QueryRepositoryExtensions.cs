using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Aarkam.Domain.Repositories;

public static class QueryRepositoryExtensions
{
    /// <summary>
    /// Returns a paged subset of entities from the repository, optionally filtered by a specified predicate.
    /// </summary>
    /// <remarks>This method enables efficient paging of query results by applying filtering and pagination at
    /// the data source level. The returned query is not executed until enumerated.</remarks>
    /// <typeparam name="TEntity">The type of the entity contained in the repository.</typeparam>
    /// <param name="repository">The repository from which to retrieve entities.</param>
    /// <param name="predicate">An optional expression to filter the entities. If null, all entities are considered.</param>
    /// <param name="pageNumber">The one-based page number to retrieve. Must be greater than or equal to 1.</param>
    /// <param name="pageSize">The number of entities to include in each page. Must be greater than 0.</param>
    /// <returns>An <see cref="IQueryable{TEntity}"/> containing the entities for the specified page. The result may be empty if
    /// no entities match the filter or the page is out of range.</returns>
    public static IQueryable<TEntity> GetPaged<TEntity>(this IRepository<TEntity> repository, Expression<Func<TEntity, bool>> predicate = null,
        int pageNumber = 1,
        int pageSize = 10) where TEntity : class
    {
        var query = repository.AsQueryable();
        if (predicate != null)
            query = query.Where(predicate);

        return query.Skip((pageNumber - 1) * pageSize).Take(pageSize);
    }
    /// <summary>
    /// Applies search filtering on one or more string properties with support for Contains (default) and Exact match.
    /// Always use the allowedProperties parameter in production 
    /// to prevent users from searching on sensitive fields (like PasswordHash, Secret, SSN, etc.).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Format examples:
    /// - "Name=John"                    → Contains search (case-insensitive)
    /// - "Name=John,Email=john@ex.com"  → Multiple properties
    /// - "Name=John,Exact=true"         → Exact match on Name
    /// </para>
    /// <b>Security Recommendation:</b> Always pass <paramref name="allowedProperties"/> whitelist to prevent unauthorized data access.
    /// </remarks>
    /// <typeparam name="TEntity">The type of entity.</typeparam>
    /// <param name="source">The source queryable.</param>
    /// <param name="searchExpression">Search string in format "Property=Value" or "Property=Value,Property2=Value2,Exact=true".</param>
    /// <param name="allowedProperties">Optional whitelist of properties allowed for searching.</param>
    /// <returns>The filtered queryable, or original source if no valid search is provided.</returns>
    public static IQueryable<TEntity> AddSearch<TEntity>(this IQueryable<TEntity> source,
        string searchExpression,
        IReadOnlyList<string> allowedProperties = null)
    {
        if (string.IsNullOrWhiteSpace(searchExpression))
            return source;

        // Security: If whitelist is provided and empty → block all search
        if (allowedProperties != null && allowedProperties.Count == 0)
            return source;

        var parts = searchExpression.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return source;

        var parameter = Expression.Parameter(typeof(TEntity), "x");
        Expression combinedExpression = null;
        bool exactMatch = false;

        foreach (var part in parts)
        {
            if (part.Equals("Exact=true", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("Exact=1", StringComparison.OrdinalIgnoreCase))
            {
                exactMatch = true;
                continue;
            }

            var kv = part.Split('=', 2);
            if (kv.Length != 2)
                continue;

            var propertyName = kv[0].Trim();
            var value = kv[1].Trim();

            if (string.IsNullOrEmpty(value))
                continue;

            // Security: Whitelist check
            if (allowedProperties != null && !allowedProperties.Contains(propertyName))
                continue;

            var propertyInfo = typeof(TEntity).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));

            if (propertyInfo == null || propertyInfo.PropertyType != typeof(string))
                continue;

            var propertyExpr = Expression.Property(parameter, propertyInfo);
            Expression searchExpr;

            if (exactMatch)
            {
                // Exact match (Equals)
                searchExpr = Expression.Equal(
                    propertyExpr,
                    Expression.Constant(value, typeof(string)));
            }
            else
            {
                // Contains search (case-insensitive by default)
                var toLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
                var containsMethod = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;

                var lowerProperty = Expression.Call(propertyExpr, toLowerMethod);
                var lowerValue = Expression.Constant(value.ToLower(), typeof(string));

                searchExpr = Expression.Call(lowerProperty, containsMethod, lowerValue);
            }

            combinedExpression = combinedExpression == null
                ? searchExpr
                : Expression.AndAlso(combinedExpression, searchExpr);   // Multiple conditions are ANDed
        }

        if (combinedExpression != null)
        {
            var lambda = Expression.Lambda<Func<TEntity, bool>>(combinedExpression, parameter);
            return source.Where(lambda);
        }

        return source;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="source"></param>
    /// <param name="sort"></param>
    /// <param name="allowedProperties"></param>
    /// <returns></returns>
    public static IQueryable<TEntity> AddSorting<TEntity>(this IQueryable<TEntity> source,
        SortingField sort,
        IEnumerable<string> allowedProperties = null)
    {
        if (source == null || sort == null || string.IsNullOrWhiteSpace(sort.Fields))
            return source;

        if (allowedProperties == null || !allowedProperties.Any())
            return source;

        var allowedSet = allowedProperties
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (allowedSet.Count == 0)
            return source;

        var parameter = Expression.Parameter(typeof(TEntity), "x");
        IQueryable<TEntity> query = source;
        bool isFirst = true;

        var fields = sort.Fields.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var field in fields)
        {
            var propertyName = field.Trim();
            bool descending = false;

            if (propertyName.EndsWith(" DESC", StringComparison.OrdinalIgnoreCase))
            {
                descending = true;
                propertyName = propertyName[..^5].Trim();
            }
            else if (propertyName.EndsWith(" ASC", StringComparison.OrdinalIgnoreCase))
            {
                propertyName = propertyName[..^4].Trim();
            }

            if (!allowedSet.Contains(propertyName))
                continue;

            var propertyInfo = typeof(TEntity)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));

            if (propertyInfo == null)
                continue;

            // x.Property
            var propertyExpression = Expression.Property(parameter, propertyInfo);

            // x => x.Property
            var lambda = Expression.Lambda(propertyExpression, parameter);

            string methodName;

            if (isFirst)
            {
                methodName = descending ? "OrderByDescending" : "OrderBy";
                isFirst = false;
            }
            else
            {
                methodName = descending ? "ThenByDescending" : "ThenBy";
            }

            query = ApplyOrder(query, lambda, methodName);
        }

        return query;
    }
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="source"></param>
    /// <param name="keySelector"></param>
    /// <param name="methodName"></param>
    /// <returns></returns>
    private static IQueryable<TEntity> ApplyOrder<TEntity>(IQueryable<TEntity> source,
        LambdaExpression keySelector, string methodName)
    {
        var resultExpression = Expression.Call(
            typeof(Queryable),
            methodName,
            new Type[] { typeof(TEntity), keySelector.Body.Type },
            source.Expression,
            Expression.Quote(keySelector));

        return source.Provider.CreateQuery<TEntity>(resultExpression);
    }
    /// <summary>
    /// Applies pagination to the specified queryable source based on the provided paging options, returning a subset of entities
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="query"></param>
    /// <param name="pagingOptions"></param>
    /// <returns></returns>
    public static IQueryable<TEntity> UsePaging<TEntity>(this IQueryable<TEntity> query, PagingOptions pagingOptions)
    {
        var skip = (pagingOptions.PageNumber - 1) * pagingOptions.PageSize;
        var result = query.Skip(skip)
            .Take(pagingOptions.PageSize);
        return result;
    }
    /// <summary>
    /// Filters the elements of an IQueryable based on a predicate if a specified condition is true.
    /// </summary>
    /// <remarks>This method is useful for conditionally applying filters to queries in a fluent manner, such
    /// as when building dynamic queries based on user input.</remarks>
    /// <typeparam name="TEntity">The type of the elements in the query.</typeparam>
    /// <param name="query">The source IQueryable to filter.</param>
    /// <param name="condition">A value indicating whether to apply the predicate filter. If set to <see langword="true"/>, the predicate is
    /// applied; otherwise, the original query is returned unfiltered.</param>
    /// <param name="predicate">An expression that defines the filter to apply to the elements if the condition is <see langword="true"/>.</param>
    /// <returns>An IQueryable that contains elements from the input sequence that satisfy the predicate if the condition is <see
    /// langword="true"/>; otherwise, the original query.</returns>
    public static IQueryable<TEntity> WhereIf<TEntity>(this IQueryable<TEntity> query, bool condition, Expression<Func<TEntity, bool>> predicate)
    {
        return condition ? query.Where(predicate) : query;
    }
    /// <summary>
    /// Asynchronously executes the specified query with pagination and returns a <see cref="PagedResult{TEntity}"/> 
    /// containing the items for the requested page
    /// </summary>
    /// <remarks>The method executes the query twice: once to retrieve the paged items and once to count the
    /// total number of items. The returned <see cref="PagedResult{TEntity}"/> includes the total count, current page
    /// number, page size, and the items for the current page.</remarks>
    /// <typeparam name="TEntity">The type of the entities in the query.</typeparam>
    /// <param name="query">The source query to paginate. Must not be null.</param>
    /// <param name="pagingOptions">The paging options to apply to the query. If null, default paging options are used.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="PagedResult{TEntity}"/>
    /// with the items for the requested page and the total item count.</returns>
    public static async Task<PagedResult<TEntity>> ToPagedResultAsync<TEntity>(this IQueryable<TEntity> query, PagingOptions pagingOptions = null)
    {
        pagingOptions ??= new PagingOptions();

        // Apply paging to get only the current page items
        var pagedQuery = query.UsePaging(pagingOptions);

        // Materialize the current page items
        var items = pagedQuery.ToList();

        // Count total records (without paging)
        var totalCount = query.Count();

        return new PagedResult<TEntity>(
            totalCount,
            pagingOptions.PageNumber,
            pagingOptions.PageSize, items);
    }
}


