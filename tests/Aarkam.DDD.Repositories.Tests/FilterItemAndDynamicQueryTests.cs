using Aarkam.DDD.Repositories.Tests.Fakes;
using Aarkam.Domain.Repositories;
using Xunit;

namespace Aarkam.DDD.Repositories.Tests;

public class FilterItemAndDynamicQueryTests
{
    private static IQueryable<TestProductEntity> GetSampleProducts()
    {
        var fixedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var guid1 = new Guid("11111111-1111-1111-1111-111111111111");
        var guid2 = new Guid("22222222-2222-2222-2222-222222222222");

        return new List<TestProductEntity>
        {
            new TestProductEntity(1, "Laptop Pro", "Electronics", 1200.50m, 15, true)
            {
                Rating = 4.8,
                NullableScore = 95.5,
                CreatedAt = fixedDate.AddDays(1),
                ExpirationDate = null,
                CorrelationId = guid1,
                ParentId = null
            },
            new TestProductEntity(2, "Wireless Mouse", "Electronics", 25.00m, 150, true)
            {
                Rating = 4.2,
                NullableScore = 80.0,
                CreatedAt = fixedDate.AddDays(5),
                ExpirationDate = fixedDate.AddYears(1),
                CorrelationId = guid2,
                ParentId = guid1
            },
            new TestProductEntity(3, "Office Chair", "Furniture", 250.00m, 8, true)
            {
                Rating = 3.9,
                NullableScore = null,
                CreatedAt = fixedDate.AddDays(10),
                ExpirationDate = null,
                CorrelationId = Guid.NewGuid(),
                ParentId = null
            },
            new TestProductEntity(4, "Standing Desk", "Furniture", 450.00m, 0, false)
            {
                Rating = 4.9,
                NullableScore = 98.0,
                CreatedAt = fixedDate.AddDays(15),
                ExpirationDate = fixedDate.AddYears(2),
                CorrelationId = Guid.NewGuid(),
                ParentId = guid1
            },
            new TestProductEntity(5, "USB-C Cable", "Electronics", 9.99m, 300, true)
            {
                Rating = 4.0,
                NullableScore = null,
                CreatedAt = fixedDate.AddDays(20),
                ExpirationDate = null,
                CorrelationId = Guid.NewGuid(),
                ParentId = null
            }
        }.AsQueryable();
    }

    private static readonly string[] FullWhitelist =
    [
        "Id", "Name", "Category", "Price", "Stock", "IsActive", "Rating",
        "NullableScore", "CreatedAt", "ExpirationDate", "CorrelationId", "ParentId"
    ];

    // =========================================================================
    // 1. String Operator Tests (EQ, NEQ, CONTAINS, STARTSWITH, ENDSWITH)
    // =========================================================================

    [Theory]
    [InlineData("EQ", "Laptop Pro", 1)]
    [InlineData("=", "Wireless Mouse", 1)]
    [InlineData("EQUAL", "Office Chair", 1)]
    [InlineData("EQUALS", "Non-Existent", 0)]
    public void AddFilter_StringEquality_ShouldMatch(string op, string value, int expectedCount)
    {
        var filter = new FilterItem { Property = "Name", Operator = op, Value = value };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(expectedCount, result.Count);
    }

    [Theory]
    [InlineData("CONTAINS", "Pro", 1)]
    [InlineData("CONTAINS", "e", 4)] // "Wireless Mouse", "Office Chair", "Standing Desk", "USB-C Cable"
    [InlineData("CONTAINS", "Desk", 1)]
    [InlineData("CONTAINS", "XYZ", 0)]
    public void AddFilter_StringContains_ShouldMatch(string op, string value, int expectedCount)
    {
        var filter = new FilterItem { Property = "Name", Operator = op, Value = value };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(expectedCount, result.Count);
    }

    [Theory]
    [InlineData("STARTSWITH", "Laptop", 1)]
    [InlineData("STARTS", "Wireless", 1)]
    [InlineData("STARTSWITH", "USB", 1)]
    [InlineData("STARTS", "NonExistent", 0)]
    public void AddFilter_StringStartsWith_ShouldMatch(string op, string value, int expectedCount)
    {
        var filter = new FilterItem { Property = "Name", Operator = op, Value = value };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(expectedCount, result.Count);
    }

    [Theory]
    [InlineData("ENDSWITH", "Pro", 1)]
    [InlineData("ENDS", "Mouse", 1)]
    [InlineData("ENDSWITH", "Cable", 1)]
    [InlineData("ENDS", "Desk", 1)]
    [InlineData("ENDS", "Chair", 1)]
    [InlineData("ENDS", "XYZ", 0)]
    public void AddFilter_StringEndsWith_ShouldMatch(string op, string value, int expectedCount)
    {
        var filter = new FilterItem { Property = "Name", Operator = op, Value = value };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(expectedCount, result.Count);
    }

    // =========================================================================
    // 2. Numeric Comparison Tests (GT, GTE, LT, LTE)
    // =========================================================================

    [Theory]
    [InlineData("GT", "100", 3)]      // 1200.50, 250.00, 450.00
    [InlineData(">", "450", 1)]       // 1200.50
    [InlineData("GREATER", "1200.50", 0)]
    [InlineData("GTE", "450", 2)]     // 450.00, 1200.50
    [InlineData(">=", "25.00", 4)]    // 25.00, 250.00, 450.00, 1200.50
    [InlineData("GREATEROREQUAL", "1200.50", 1)]
    public void AddFilter_DecimalGreaterThan_ShouldMatch(string op, string value, int expectedCount)
    {
        var filter = new FilterItem { Property = "Price", Operator = op, Value = value };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(expectedCount, result.Count);
    }

    [Theory]
    [InlineData("LT", "25", 1)]       // 9.99
    [InlineData("<", "10", 1)]        // 9.99
    [InlineData("LESS", "9.99", 0)]
    [InlineData("LTE", "25", 2)]      // 9.99, 25.00
    [InlineData("<=", "250", 3)]      // 9.99, 25.00, 250.00
    [InlineData("LESSOREQUAL", "9.99", 1)]
    public void AddFilter_DecimalLessThan_ShouldMatch(string op, string value, int expectedCount)
    {
        var filter = new FilterItem { Property = "Price", Operator = op, Value = value };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(expectedCount, result.Count);
    }

    [Theory]
    [InlineData("GT", "15", 2)]      // 150, 300
    [InlineData("GTE", "15", 3)]     // 15, 150, 300
    [InlineData("LT", "8", 1)]       // 0
    [InlineData("LTE", "8", 2)]      // 0, 8
    [InlineData("EQ", "0", 1)]       // 0
    [InlineData("NEQ", "0", 4)]      // not 0
    public void AddFilter_IntegerComparisons_ShouldMatch(string op, string value, int expectedCount)
    {
        var filter = new FilterItem { Property = "Stock", Operator = op, Value = value };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(expectedCount, result.Count);
    }

    // =========================================================================
    // 3. Null & Not Null Operator Tests
    // =========================================================================

    [Theory]
    [InlineData("ISNULL")]
    [InlineData("NULL")]
    [InlineData("IS NULL")]
    public void AddFilter_IsNull_OnNullableDouble_ShouldMatchNulls(string op)
    {
        var filter = new FilterItem { Property = "NullableScore", Operator = op };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(2, result.Count); // Product 3 and 5 have NullableScore == null
    }

    [Theory]
    [InlineData("ISNOTNULL")]
    [InlineData("NOTNULL")]
    [InlineData("IS NOT NULL")]
    public void AddFilter_IsNotNull_OnNullableDouble_ShouldMatchNonNulls(string op)
    {
        var filter = new FilterItem { Property = "NullableScore", Operator = op };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(3, result.Count); // Product 1, 2, 4
    }

    [Fact]
    public void AddFilter_IsNull_OnNullableGuid_ShouldMatch()
    {
        var filter = new FilterItem { Property = "ParentId", Operator = "ISNULL" };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(3, result.Count); // Product 1, 3, 5
    }

    [Fact]
    public void AddFilter_IsNotNull_OnNullableGuid_ShouldMatch()
    {
        var filter = new FilterItem { Property = "ParentId", Operator = "ISNOTNULL" };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(2, result.Count); // Product 2, 4
    }

    // =========================================================================
    // 4. Boolean Special Operators Tests
    // =========================================================================

    [Theory]
    [InlineData("TRUE")]
    [InlineData("ISTRUE")]
    [InlineData("IS TRUE")]
    [InlineData("EQ", "true")]
    [InlineData("=", "True")]
    public void AddFilter_BooleanTrue_ShouldMatchActiveProducts(string op, string? val = null)
    {
        var filter = new FilterItem { Property = "IsActive", Operator = op, Value = val };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(4, result.Count); // Products 1, 2, 3, 5 are active
    }

    [Theory]
    [InlineData("FALSE")]
    [InlineData("ISFALSE")]
    [InlineData("IS FALSE")]
    [InlineData("EQ", "false")]
    [InlineData("=", "False")]
    public void AddFilter_BooleanFalse_ShouldMatchInactiveProducts(string op, string? val = null)
    {
        var filter = new FilterItem { Property = "IsActive", Operator = op, Value = val };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Single(result); // Product 4 is inactive
        Assert.Equal(4, result[0].Id);
    }

    // =========================================================================
    // 5. IN Operator Tests
    // =========================================================================

    [Fact]
    public void AddFilter_InOperator_StringValues_ShouldMatch()
    {
        var filter = new FilterItem
        {
            Property = "Category",
            Operator = "IN",
            Values = ["Furniture", "NonExistent"]
        };

        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(2, result.Count); // Product 3, 4
    }

    [Fact]
    public void AddFilter_InOperator_IntValues_ShouldMatch()
    {
        var filter = new FilterItem
        {
            Property = "Id",
            Operator = "IN",
            Values = ["1", "3", "5"]
        };

        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(3, result.Count);
        Assert.Contains(result, p => p.Id == 1);
        Assert.Contains(result, p => p.Id == 3);
        Assert.Contains(result, p => p.Id == 5);
    }

    [Fact]
    public void AddFilter_InOperator_EmptyValues_ShouldReturnAll()
    {
        var filter = new FilterItem
        {
            Property = "Id",
            Operator = "IN",
            Values = []
        };

        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(5, result.Count); // Filter skipped
    }

    // =========================================================================
    // 6. Security Whitelist & Fail-Closed Tests
    // =========================================================================

    [Fact]
    public void AddFilter_NullAllowedProperties_ShouldFailClosedAndReturnUnfiltered()
    {
        var filter = new FilterItem { Property = "Name", Operator = "EQ", Value = "Laptop Pro" };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, allowedProperties: null).ToList();

        Assert.Equal(5, result.Count); // Blocked by fail-closed security
    }

    [Fact]
    public void AddFilter_EmptyAllowedProperties_ShouldFailClosedAndReturnUnfiltered()
    {
        var filter = new FilterItem { Property = "Name", Operator = "EQ", Value = "Laptop Pro" };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, allowedProperties: []).ToList();

        Assert.Equal(5, result.Count); // Blocked
    }

    [Fact]
    public void AddFilter_UnwhitelistedProperty_ShouldIgnoreFilter()
    {
        var filter = new FilterItem { Property = "SecretProperty", Operator = "EQ", Value = "hack" };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, ["Name", "Price"]).ToList();

        Assert.Equal(5, result.Count); // Filter safely ignored
    }

    [Fact]
    public void AddFilter_CaseInsensitivePropertyAndWhitelist_ShouldMatch()
    {
        var filter = new FilterItem { Property = "category", Operator = "eq", Value = "Furniture" };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, ["CATEGORY"]).ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void AddFilter_ExcessiveLengthValue_ShouldIgnoreFilterToPreventDoS()
    {
        var hugeString = new string('A', 501);
        var filter = new FilterItem { Property = "Name", Operator = "EQ", Value = hugeString };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(5, result.Count); // Blocked by MaxValueLength guard
    }

    // =========================================================================
    // 7. Logical Operation Combinations (AND vs OR)
    // =========================================================================

    [Fact]
    public void AddFilter_MultipleFilters_WithAnd_ShouldSatisfyAll()
    {
        var filters = new[]
        {
            new FilterItem { Property = "Category", Operator = "EQ", Value = "Electronics" },
            new FilterItem { Property = "Stock", Operator = "GT", Value = "20" }
        };

        var result = GetSampleProducts().AddFilter(filters, FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(2, result.Count); // Wireless Mouse (150) and USB-C Cable (300)
    }

    [Fact]
    public void AddFilter_MultipleFilters_WithOr_ShouldSatisfyEither()
    {
        var filters = new[]
        {
            new FilterItem { Property = "Name", Operator = "EQ", Value = "Laptop Pro" },
            new FilterItem { Property = "Name", Operator = "EQ", Value = "Standing Desk" }
        };

        var result = GetSampleProducts().AddFilter(filters, FilterOperation.Or, FullWhitelist).ToList();

        Assert.Equal(2, result.Count); // Product 1 and Product 4
    }

    [Fact]
    public void AddFilter_NullOrEmptyFiltersCollection_ShouldReturnOriginalQuery()
    {
        var result1 = GetSampleProducts().AddFilter(null, FilterOperation.And, FullWhitelist).ToList();
        var result2 = GetSampleProducts().AddFilter([], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(5, result1.Count);
        Assert.Equal(5, result2.Count);
    }

    [Fact]
    public void AddFilter_NullSource_ShouldThrowArgumentNullException()
    {
        IQueryable<TestProductEntity>? nullSource = null;
        Assert.Throws<ArgumentNullException>(() => nullSource!.AddFilter([], FilterOperation.And, FullWhitelist));
    }

    // =========================================================================
    // 8. Comprehensive Operator Aliases & Null Handling Tests
    // =========================================================================

    [Theory]
    [InlineData("EQ", "Laptop Pro", 1)]
    [InlineData("=", "Laptop Pro", 1)]
    [InlineData("EQUAL", "Laptop Pro", 1)]
    [InlineData("EQUALS", "Laptop Pro", 1)]
    [InlineData("NEQ", "Laptop Pro", 4)]
    [InlineData("!=", "Laptop Pro", 4)]
    [InlineData("NOTEQUAL", "Laptop Pro", 4)]
    public void AddFilter_EqualityOperatorAliases_ShouldWorkUniformly(string op, string value, int expectedCount)
    {
        var filter = new FilterItem { Property = "Name", Operator = op, Value = value };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(expectedCount, result.Count);
    }

    [Theory]
    [InlineData("GT", "100", 3)]           // Laptop (1200), Chair (250), Desk (450)
    [InlineData(">", "100", 3)]
    [InlineData("GREATER", "100", 3)]
    [InlineData("GTE", "250", 3)]          // 1200, 250, 450
    [InlineData(">=", "250", 3)]
    [InlineData("GREATEROREQUAL", "250", 3)]
    [InlineData("LT", "100", 2)]           // Mouse (25), Cable (15)
    [InlineData("<", "100", 2)]
    [InlineData("LESS", "100", 2)]
    [InlineData("LTE", "25", 2)]           // 25, 15
    [InlineData("<=", "25", 2)]
    [InlineData("LESSOREQUAL", "25", 2)]
    public void AddFilter_NumericRelationalOperatorAliases_ShouldWorkUniformly(string op, string value, int expectedCount)
    {
        var filter = new FilterItem { Property = "Price", Operator = op, Value = value };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(expectedCount, result.Count);
    }

    [Theory]
    [InlineData("STARTSWITH", "Lap", 1)]
    [InlineData("STARTS", "Lap", 1)]
    [InlineData("ENDSWITH", "Pro", 1)]
    [InlineData("ENDS", "Pro", 1)]
    [InlineData("ENDSWITH", "Cable", 1)]
    [InlineData("ENDS", "Desk", 1)]
    [InlineData("CONTAINS", "Chair", 1)]
    public void AddFilter_StringPatternOperatorAliases_ShouldWorkUniformly(string op, string value, int expectedCount)
    {
        var filter = new FilterItem { Property = "Name", Operator = op, Value = value };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(expectedCount, result.Count);
    }

    [Theory]
    [InlineData("TRUE", 4)]
    [InlineData("ISTRUE", 4)]
    [InlineData("IS TRUE", 4)]
    [InlineData("FALSE", 1)]
    [InlineData("ISFALSE", 1)]
    [InlineData("IS FALSE", 1)]
    public void AddFilter_BooleanOperatorAliases_ShouldWorkUniformly(string op, int expectedCount)
    {
        var filter = new FilterItem { Property = "IsActive", Operator = op };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(expectedCount, result.Count);
    }

    [Theory]
    [InlineData("ISNULL", 3)]        // 3 products have null ExpirationDate
    [InlineData("NULL", 3)]
    [InlineData("IS NULL", 3)]
    [InlineData("ISNOTNULL", 2)]     // 2 products have non-null ExpirationDate
    [InlineData("NOTNULL", 2)]
    [InlineData("IS NOT NULL", 2)]
    public void AddFilter_NullOperatorAliases_OnNullableDate_ShouldWorkUniformly(string op, int expectedCount)
    {
        var filter = new FilterItem { Property = "ExpirationDate", Operator = op };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(expectedCount, result.Count);
    }

    [Theory]
    [InlineData("ISNULL", 2)]        // 2 products have null NullableScore (chair, cable)
    [InlineData("ISNOTNULL", 3)]     // 3 products have non-null NullableScore
    public void AddFilter_NullOperatorAliases_OnNullableNumeric_ShouldWorkUniformly(string op, int expectedCount)
    {
        var filter = new FilterItem { Property = "NullableScore", Operator = op };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(expectedCount, result.Count);
    }

    [Theory]
    [InlineData("ISNULL", 3)]        // 3 products have null ParentId (Products 1, 3, 5)
    [InlineData("ISNOTNULL", 2)]     // 2 products have non-null ParentId (Products 2, 4)
    public void AddFilter_NullOperatorAliases_OnNullableGuid_ShouldWorkUniformly(string op, int expectedCount)
    {
        var filter = new FilterItem { Property = "ParentId", Operator = op };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(expectedCount, result.Count);
    }

    // =========================================================================
    // 9. Edge Cases & Resilience Tests
    // =========================================================================

    [Fact]
    public void AddFilter_InvalidNumericConversion_ShouldGracefullySkipFilter()
    {
        var filter = new FilterItem { Property = "Price", Operator = "GT", Value = "NotANumber" };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(5, result.Count); // Filter safely ignored
    }

    [Fact]
    public void AddFilter_InvalidGuidConversion_ShouldGracefullySkipFilter()
    {
        var filter = new FilterItem { Property = "CorrelationId", Operator = "EQ", Value = "NotAGuid" };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(5, result.Count); // Filter safely ignored
    }

    [Fact]
    public void AddFilter_InvalidDateConversion_ShouldGracefullySkipFilter()
    {
        var filter = new FilterItem { Property = "CreatedAt", Operator = "GT", Value = "InvalidDateString" };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(5, result.Count); // Filter safely ignored
    }

    [Fact]
    public void AddFilter_EmptyOrWhitespaceValue_ShouldSkipFilter()
    {
        var filter1 = new FilterItem { Property = "Name", Operator = "EQ", Value = "" };
        var filter2 = new FilterItem { Property = "Name", Operator = "EQ", Value = "   " };

        var result1 = GetSampleProducts().AddFilter([filter1], FilterOperation.And, FullWhitelist).ToList();
        var result2 = GetSampleProducts().AddFilter([filter2], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(5, result1.Count);
        Assert.Equal(5, result2.Count);
    }

    [Fact]
    public void AddFilter_UnknownOperator_ShouldSkipFilter()
    {
        var filter = new FilterItem { Property = "Name", Operator = "UNKNOWN_OP", Value = "Laptop" };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void AddFilter_InOperator_WithWhitespaceAndEmptyValues_ShouldFilterValidItemsOnly()
    {
        var filter = new FilterItem
        {
            Property = "Name",
            Operator = "IN",
            Values = ["", "   ", "Laptop Pro", "  Office Chair  ", null!]
        };

        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void AddFilter_InOperator_WithNoValidValues_ShouldSkipFilter()
    {
        var filter = new FilterItem
        {
            Property = "Name",
            Operator = "IN",
            Values = ["", "   ", null!]
        };

        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void AddFilter_InOperator_NumericTypedArray_ShouldFilterCorrectly()
    {
        var filter = new FilterItem
        {
            Property = "Stock",
            Operator = "IN",
            Values = ["15", "150"]
        };

        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, FullWhitelist).ToList();

        Assert.Equal(2, result.Count); // Product 1 (15) and Product 2 (150)
    }

    [Fact]
    public void AddFilter_Security_NullAllowedProperties_ShouldFailClosed()
    {
        var filter = new FilterItem { Property = "Name", Operator = "EQ", Value = "Laptop Pro" };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, null!).ToList();

        Assert.Equal(5, result.Count); // Returned unmodified for safety
    }

    [Fact]
    public void AddFilter_Security_EmptyAllowedProperties_ShouldFailClosed()
    {
        var filter = new FilterItem { Property = "Name", Operator = "EQ", Value = "Laptop Pro" };
        var result = GetSampleProducts().AddFilter([filter], FilterOperation.And, []).ToList();

        Assert.Equal(5, result.Count); // Returned unmodified for safety
    }
}
