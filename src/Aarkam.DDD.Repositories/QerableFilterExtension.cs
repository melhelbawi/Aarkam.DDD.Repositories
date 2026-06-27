using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Aarkam.Domain.Repositories
{
    /// <summary>
    /// Represents a single filter request from the client.
    /// </summary>
    public class FilterItem
    {
        /// <summary>
        /// Name of the property to filter on (e.g. "Name", "Email", "Status").
        /// </summary>
        public string Property { get; set; } = string.Empty;

        /// <summary>
        /// Value to filter by.
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Filter operator. Supported values: EQ, NEQ, CONTAINS, STARTSWITH, ENDSWITH.
        /// Defaults to "EQ" (equals).
        /// </summary>
        public string Operator { get; set; } = "EQ";

        // Optional: For IN operator (comma-separated values)
        public string[] Values { get; set; }
    }

    public static class QerableFilterExtension
    {
        public static int MaxValueLength = 500;

        /// <summary>
        /// Securely applies dynamic filters to an <see cref="IQueryable{TEntity}"/> using FilterItem DTOs.
        /// Supports a wide range of operators including string, numeric, DateTime, and boolean comparisons.
        /// </summary>
        /// <remarks>
        /// Always use the allowedProperties parameter in production 
        /// to prevent users from searching on sensitive fields (like PasswordHash, Secret, SSN, etc.).
        /// <para>
        /// Security features:
        /// - Property whitelisting (prevents unauthorized property access)
        /// - Maximum value length protection (prevents DoS via extremely large inputs)
        /// - Safe type conversion with fallback
        /// - Graceful handling of null, empty, or whitespace values
        /// - All values are properly parameterized by EF Core
        /// </para>
        /// </remarks>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="source"></param>
        /// <param name="filters"></param>
        /// <param name="filterOperation"></param>
        /// <param name="allowedProperties"></param>
        /// <returns></returns>
        public static IQueryable<TEntity> AddFilter<TEntity>(this IQueryable<TEntity> source,
            IEnumerable<FilterItem> filters = null,
            FilterOperation filterOperation = FilterOperation.And,
            IReadOnlyList<string> allowedProperties = null)
        {
            if (source == null || filters == null || !filters.Any())
                return source;

            // Security: Fail-closed
            if (allowedProperties == null || allowedProperties.Count == 0)
                return source;

            var parameter = Expression.Parameter(typeof(TEntity), "x");
            Expression combinedExpression = null;

            foreach (var filter in filters)
            {
                if (filter == null || string.IsNullOrWhiteSpace(filter.Property))
                    continue;

                if (!allowedProperties.Contains(filter.Property))
                    continue;

                var propertyInfo = typeof(TEntity).GetProperty(
                    filter.Property,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (propertyInfo == null || !propertyInfo.CanRead)
                    continue;

                if (IsUnsupportedType(propertyInfo.PropertyType))
                    continue;

                var propertyExpr = Expression.Property(parameter, propertyInfo);
                var filterExpr = BuildFilterExpression(propertyExpr, filter);

                if (filterExpr == null)
                    continue;

                combinedExpression = combinedExpression == null
                    ? filterExpr
                    : filterOperation == FilterOperation.Or
                        ? Expression.OrElse(combinedExpression, filterExpr)
                        : Expression.AndAlso(combinedExpression, filterExpr);
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
        /// <param name="propertyExpr"></param>
        /// <param name="rawValue"></param>
        /// <param name="operatorStr"></param>
        /// <returns></returns>
        private static Expression BuildFilterExpression(MemberExpression propertyExpr, FilterItem filter)
        {
            var operatorStr = (filter.Operator?.ToUpperInvariant() ?? "EQ").Trim();
            var propertyType = propertyExpr.Type;

            // ====================== NULL Checks ======================
            if (operatorStr is "ISNULL" or "NULL" or "IS NULL")
                return Expression.Equal(propertyExpr, Expression.Constant(null, propertyType));

            if (operatorStr is "ISNOTNULL" or "NOTNULL" or "IS NOT NULL")
                return Expression.NotEqual(propertyExpr, Expression.Constant(null, propertyType));

            // ====================== IN Operator ======================
            if (operatorStr == "IN" && filter.Values != null && filter.Values.Length > 0)
                return BuildInExpression(propertyExpr, filter.Values, propertyType);

            // ====================== Boolean Special Handling ======================
            if (propertyType == typeof(bool) || propertyType == typeof(bool?))
            {
                switch (operatorStr)
                {
                    case "TRUE" or "ISTRUE" or "IS TRUE":
                        return Expression.Equal(propertyExpr, Expression.Constant(true));

                    case "FALSE" or "ISFALSE" or "IS FALSE":
                        return Expression.Equal(propertyExpr, Expression.Constant(false));

                    case "EQ" or "=":
                        // Allow "true"/"false" as string fallback
                        if (bool.TryParse(filter.Value, out bool boolValue))
                            return Expression.Equal(propertyExpr, Expression.Constant(boolValue));
                        break;
                }
            }

            // ====================== Normal Value Operators ======================
            if (string.IsNullOrWhiteSpace(filter.Value))
                return null;

            if (filter.Value.Length > MaxValueLength)
                return null;

            object convertedValue = ConvertValue(filter.Value, propertyType);
            if (convertedValue == null && propertyType != typeof(string))
                return null;

            var constant = Expression.Constant(convertedValue, propertyType);

            return operatorStr switch
            {
                "EQ" or "=" or "EQUAL" or "EQUALS" => Expression.Equal(propertyExpr, constant),
                "NEQ" or "!=" or "NOTEQUAL" => Expression.NotEqual(propertyExpr, constant),

                "GT" or ">" or "GREATER" => Expression.GreaterThan(propertyExpr, constant),
                "GTE" or ">=" or "GREATEROREQUAL" => Expression.GreaterThanOrEqual(propertyExpr, constant),
                "LT" or "<" or "LESS" => Expression.LessThan(propertyExpr, constant),
                "LTE" or "<=" or "LESSOREQUAL" => Expression.LessThanOrEqual(propertyExpr, constant),

                "CONTAINS" when propertyType == typeof(string) => Expression.Call(propertyExpr,
                    typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!,
                    Expression.Constant(filter.Value)),

                "STARTSWITH" or "STARTS" when propertyType == typeof(string) => Expression.Call(propertyExpr,
                    typeof(string).GetMethod(nameof(string.StartsWith), new[] { typeof(string) })!,
                    Expression.Constant(filter.Value)),

                "ENDSWITH" or "ENDS" when propertyType == typeof(string) => Expression.Call(propertyExpr,
                    typeof(string).GetMethod(nameof(string.EndsWith), new[] { typeof(string) })!,
                    Expression.Constant(filter.Value)),

                _ => null
            };
        }
        /// <summary>
        /// Converts the specified string value to an object of the given target type, if possible.
        /// </summary>
        /// <remarks>If the conversion is not supported or fails, the method returns null. This method
        /// supports common primitive types and their nullable counterparts. For unsupported types, it attempts to use
        /// Convert.ChangeType.</remarks>
        /// <param name="rawValue">The string representation of the value to convert.</param>
        /// <param name="targetType">The type to which the value should be converted. Supported types include string, int, long, decimal, double,
        /// bool, Guid, and DateTime, including their nullable forms.</param>
        /// <returns>An object of the specified target type representing the converted value, or null if the conversion fails.</returns>
        private static object ConvertValue(string rawValue, Type targetType)
        {
            if (targetType == typeof(string))
                return rawValue;

            try
            {
                return targetType switch
                {
                    var t when t == typeof(int) || t == typeof(int?) => int.TryParse(rawValue, out var i) ? i : null,
                    var t when t == typeof(long) || t == typeof(long?) => long.TryParse(rawValue, out var l) ? l : null,
                    var t when t == typeof(decimal) || t == typeof(decimal?) => decimal.TryParse(rawValue, out var d) ? d : null,
                    var t when t == typeof(double) || t == typeof(double?) => double.TryParse(rawValue, out var dbl) ? dbl : null,
                    var t when t == typeof(bool) || t == typeof(bool?) => bool.TryParse(rawValue, out var b) ? b : null,
                    var t when t == typeof(Guid) || t == typeof(Guid?) => Guid.TryParse(rawValue, out var g) ? g : null,
                    var t when t == typeof(DateTime) || t == typeof(DateTime?) => DateTime.TryParse(rawValue, out var dt) ? dt : null,
                    _ => Convert.ChangeType(rawValue, targetType)
                };
            }
            catch
            {
                return null; // Conversion failed → skip this filter
            }
        }
        /// <summary>
        /// Determines whether the specified type is considered unsupported for the current operation.
        /// </summary>
        /// <param name="type">The type to evaluate for support.</param>
        /// <returns>true if the specified type is a byte array or a non-string reference type; otherwise, false.</returns>
        private static bool IsUnsupportedType(Type type)
        {
            return type == typeof(byte[]) ||
                   (type.IsClass && type != typeof(string) && !type.IsValueType);
        }
        /// <summary>
        /// Builds an expression that determines whether a property value is contained within a specified set of values.
        /// </summary>
        /// <remarks>The returned expression can be used in LINQ queries to filter entities where the
        /// property matches any of the specified values. If all values are empty or cannot be converted to the property
        /// type, the method returns null.</remarks>
        /// <param name="propertyExpr">The member expression representing the property to compare against the set of values.</param>
        /// <param name="values">An array of string values to check for membership. Empty or whitespace-only values are ignored.</param>
        /// <param name="propertyType">The type of the property represented by the member expression. Used to convert string values to the
        /// appropriate type.</param>
        /// <returns>An expression representing a call to Enumerable.Contains for the property and the provided values, or null
        /// if no valid values are supplied.</returns>
        private static Expression BuildInExpression(MemberExpression propertyExpr, string[] values, Type propertyType)
        {
            if (values.Length == 0) return null;

            var convertedValues = new List<object>(values.Length);

            foreach (var val in values)
            {
                if (string.IsNullOrWhiteSpace(val)) continue;
                var converted = ConvertValue(val.Trim(), propertyType);
                if (converted != null || propertyType == typeof(string))
                    convertedValues.Add(converted ?? val.Trim());
            }

            if (convertedValues.Count == 0)
                return null;

            var arrayType = propertyType.MakeArrayType();
            var arrayConstant = Expression.Constant(convertedValues.ToArray(), arrayType);

            var containsMethod = typeof(Enumerable)
                .GetMethods()
                .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                .MakeGenericMethod(propertyType);

            return Expression.Call(containsMethod, arrayConstant, propertyExpr);
        }
    }
}
