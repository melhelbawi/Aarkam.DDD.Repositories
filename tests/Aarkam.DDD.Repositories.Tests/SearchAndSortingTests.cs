using Aarkam.DDD.Repositories.Tests.Fakes;
using Aarkam.Domain.Repositories;
using Xunit;

namespace Aarkam.DDD.Repositories.Tests;

public class SearchAndSortingTests
{
    private static IQueryable<TestProductEntity> GetSampleProducts()
    {
        return new List<TestProductEntity>
        {
            new TestProductEntity(1, "Alpha Laptop", "Computers", 1500m, 10),
            new TestProductEntity(2, "Beta Mouse", "Peripherals", 30m, 50),
            new TestProductEntity(3, "Gamma Monitor", "Peripherals", 300m, 20),
            new TestProductEntity(4, "Delta Keyboard", "Peripherals", 80m, 35),
            new TestProductEntity(5, "Epsilon Laptop", "Computers", 1200m, 5),
            new TestProductEntity(6, "Zeta Cable", "Accessories", 15m, 100)
        }.AsQueryable();
    }

    private static readonly string[] SearchWhitelist = ["Name", "Category"];
    private static readonly string[] SortWhitelist = ["Name", "Category", "Price", "Stock", "Id"];

    // =========================================================================
    // 1. AddSearch Tests
    // =========================================================================

    [Theory]
    [InlineData("Name=Laptop", 2)]       // Alpha Laptop, Epsilon Laptop
    [InlineData("name=laptop", 2)]       // Case-insensitive
    [InlineData("NAME=LAPTOP", 2)]
    [InlineData("Name=Mouse", 1)]
    [InlineData("Name=Keyboard", 1)]
    [InlineData("Name=NonExistent", 0)]
    public void AddSearch_SingleFieldContains_ShouldMatch(string searchExpr, int expectedCount)
    {
        var result = GetSampleProducts().AddSearch(searchExpr, SearchWhitelist).ToList();
        Assert.Equal(expectedCount, result.Count);
    }

    [Theory]
    [InlineData("Name=Alpha Laptop,Exact=true", 1)]
    [InlineData("Name=Alpha,Exact=true", 0)]
    [InlineData("Name=Alpha Laptop,Exact=1", 1)]
    [InlineData("Name=Beta Mouse,Exact=true", 1)]
    public void AddSearch_ExactMatch_ShouldEnforceEquality(string searchExpr, int expectedCount)
    {
        var result = GetSampleProducts().AddSearch(searchExpr, SearchWhitelist).ToList();
        Assert.Equal(expectedCount, result.Count);
    }

    [Fact]
    public void AddSearch_MultiFieldSearch_ShouldApplyLogicalAnd()
    {
        // Name contains Laptop AND Category contains Computers
        var result = GetSampleProducts()
            .AddSearch("Name=Laptop,Category=Computers", SearchWhitelist)
            .ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void AddSearch_UnwhitelistedField_ShouldIgnoreSearch()
    {
        var result = GetSampleProducts()
            .AddSearch("Secret=Value", ["Name"])
            .ToList();

        Assert.Equal(6, result.Count); // Search safely ignored
    }

    [Fact]
    public void AddSearch_EmptyWhitelist_ShouldBlockSearch()
    {
        var result = GetSampleProducts()
            .AddSearch("Name=Laptop", allowedProperties: [])
            .ToList();

        Assert.Equal(6, result.Count); // Blocked
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddSearch_NullOrEmptyExpression_ShouldReturnOriginalQuery(string? expr)
    {
        var result = GetSampleProducts().AddSearch(expr!, SearchWhitelist).ToList();
        Assert.Equal(6, result.Count);
    }

    // =========================================================================
    // 2. AddSorting Tests
    // =========================================================================

    [Fact]
    public void AddSorting_Ascending_ShouldSortCorrectly()
    {
        var sort = new SortingField("Price ASC", SortingDirection.Ascending);
        var result = GetSampleProducts().AddSorting(sort, SortWhitelist).ToList();

        Assert.Equal(15m, result.First().Price);
        Assert.Equal(1500m, result.Last().Price);
    }

    [Fact]
    public void AddSorting_Descending_ShouldSortCorrectly()
    {
        var sort = new SortingField("Price DESC", SortingDirection.Descending);
        var result = GetSampleProducts().AddSorting(sort, SortWhitelist).ToList();

        Assert.Equal(1500m, result.First().Price);
        Assert.Equal(15m, result.Last().Price);
    }

    [Fact]
    public void AddSorting_MultiFieldSorting_ShouldApplySubsequentOrder()
    {
        // Category ASC, then Price DESC
        var sort = new SortingField("Category ASC,Price DESC", SortingDirection.Ascending);
        var result = GetSampleProducts().AddSorting(sort, SortWhitelist).ToList();

        // Accessories -> Computers (1500, then 1200) -> Peripherals
        Assert.Equal("Accessories", result[0].Category);
        Assert.Equal("Computers", result[1].Category);
        Assert.Equal(1500m, result[1].Price);
        Assert.Equal("Computers", result[2].Category);
        Assert.Equal(1200m, result[2].Price);
    }

    [Fact]
    public void AddSorting_UnwhitelistedField_ShouldIgnoreSorting()
    {
        var sort = new SortingField("NonExistent DESC", SortingDirection.Descending);
        var result = GetSampleProducts().AddSorting(sort, ["Price"]).ToList();

        // Order preserved as original
        Assert.Equal(1, result.First().Id);
    }

    [Fact]
    public void AddSorting_NullSortOrEmptyFields_ShouldReturnOriginalQuery()
    {
        var result1 = GetSampleProducts().AddSorting(null!, SortWhitelist).ToList();
        var result2 = GetSampleProducts().AddSorting(new SortingField("", SortingDirection.Ascending), SortWhitelist).ToList();

        Assert.Equal(6, result1.Count);
        Assert.Equal(6, result2.Count);
    }

    [Fact]
    public void AddSorting_NullSource_ShouldThrowArgumentNullException()
    {
        IQueryable<TestProductEntity>? nullSource = null;
        var sort = new SortingField("Price", SortingDirection.Ascending);

        Assert.Throws<ArgumentNullException>(() => nullSource!.AddSorting(sort, SortWhitelist));
    }

    // =========================================================================
    // 3. UsePaging Tests
    // =========================================================================

    [Theory]
    [InlineData(1, 2, 2, 1)]  // Page 1, Size 2 -> 2 items, First ID = 1
    [InlineData(2, 2, 2, 3)]  // Page 2, Size 2 -> 2 items, First ID = 3
    [InlineData(3, 2, 2, 5)]  // Page 3, Size 2 -> 2 items, First ID = 5
    [InlineData(4, 2, 0, 0)]  // Page 4, Size 2 -> 0 items (out of range)
    [InlineData(1, 10, 6, 1)] // Page 1, Size 10 -> all 6 items
    public void UsePaging_VariousPages_ShouldPaginateProperly(int pageNumber, int pageSize, int expectedCount, int firstExpectedId)
    {
        var options = new PagingOptions(pageNumber, pageSize);
        var paged = GetSampleProducts().UsePaging(options).ToList();

        Assert.Equal(expectedCount, paged.Count);
        if (expectedCount > 0)
        {
            Assert.Equal(firstExpectedId, paged.First().Id);
        }
    }

    // =========================================================================
    // 4. WhereIf Tests
    // =========================================================================

    [Theory]
    [InlineData(true, 2)]   // Filter applied: Price > 1000 -> 2 items (1500, 1200)
    [InlineData(false, 6)]  // Filter skipped: 6 items
    public void WhereIf_Condition_ShouldApplyFilterConditionally(bool condition, int expectedCount)
    {
        var result = GetSampleProducts().WhereIf(condition, p => p.Price > 1000m).ToList();
        Assert.Equal(expectedCount, result.Count);
    }

    // =========================================================================
    // 5. ToPagedResultAsync Tests
    // =========================================================================

    [Fact]
    public async Task ToPagedResultAsync_ValidQuery_ShouldReturnPagedResult()
    {
        var query = GetSampleProducts();
        var options = new PagingOptions(1, 3);

        var pagedResult = await query.ToPagedResultAsync(options);

        Assert.NotNull(pagedResult);
        Assert.Equal(6, pagedResult.TotalCount);
        Assert.Equal(1, pagedResult.CurrentPage);
        Assert.Equal(3, pagedResult.PageSize);
        Assert.Equal(2, pagedResult.TotalPages);
        Assert.Equal(3, pagedResult.Items.Count);
        Assert.False(pagedResult.HasPreviousPage);
        Assert.True(pagedResult.HasNextPage);
    }

    [Fact]
    public async Task ToPagedResultAsync_NullPagingOptions_ShouldUseDefaultPaging()
    {
        var query = GetSampleProducts();
        var pagedResult = await query.ToPagedResultAsync(null);

        Assert.NotNull(pagedResult);
        Assert.Equal(6, pagedResult.TotalCount);
        Assert.Equal(1, pagedResult.CurrentPage);
        Assert.Equal(PagingOptions.DefaultPageSize, pagedResult.PageSize);
    }

    // =========================================================================
    // 6. Advanced Sorting Combinations & Case Insensitivity
    // =========================================================================

    [Theory]
    [InlineData("price desc", 1500.00)]
    [InlineData("PRICE DESC", 1500.00)]
    [InlineData("Price desc", 1500.00)]
    [InlineData("price asc", 15.00)]
    [InlineData("PRICE ASC", 15.00)]
    public void AddSorting_CaseInsensitiveSortExpressions_ShouldWork(string sortExpression, decimal expectedFirstPrice)
    {
        var sorted = GetSampleProducts().AddSorting(new SortingField(sortExpression, SortingDirection.Ascending), ["Price"]).ToList();
        Assert.Equal(expectedFirstPrice, sorted.First().Price);
    }

    [Fact]
    public void AddSorting_MultiFieldMixedSorting_ShouldOrderCorrectly()
    {
        // Category ASC, then Price DESC
        var sorted = GetSampleProducts()
            .AddSorting(new SortingField("Category ASC, Price DESC", SortingDirection.Ascending), ["Category", "Price"])
            .ToList();

        // Accessories first: Zeta Cable (15)
        Assert.Equal("Accessories", sorted[0].Category);
        Assert.Equal(15.00m, sorted[0].Price);

        // Computers next: Alpha Laptop (1500), Epsilon Laptop (1200)
        Assert.Equal("Computers", sorted[1].Category);
        Assert.Equal(1500.00m, sorted[1].Price);
        Assert.Equal("Computers", sorted[2].Category);
        Assert.Equal(1200.00m, sorted[2].Price);

        // Peripherals next: Gamma Monitor (300), Delta Keyboard (80), Beta Mouse (30)
        Assert.Equal("Peripherals", sorted[3].Category);
        Assert.Equal(300.00m, sorted[3].Price);
        Assert.Equal("Peripherals", sorted[4].Category);
        Assert.Equal(80.00m, sorted[4].Price);
        Assert.Equal("Peripherals", sorted[5].Category);
        Assert.Equal(30.00m, sorted[5].Price);
    }

    [Fact]
    public void AddSorting_DisallowedPropertyInMultiSort_ShouldApplyAllowedOnly()
    {
        // Secret is not whitelisted, Price is
        var sorted = GetSampleProducts()
            .AddSorting(new SortingField("Secret DESC, Price ASC", SortingDirection.Ascending), ["Price"])
            .ToList();

        Assert.Equal(15.00m, sorted.First().Price);
        Assert.Equal(1500.00m, sorted.Last().Price);
    }

    [Theory]
    [InlineData("NonExistentProperty ASC")]
    [InlineData("InvalidProperty DESC")]
    [InlineData("   ")]
    [InlineData("=,=")]
    public void AddSorting_InvalidOrUnwhitelistedProperties_ShouldSafelyReturnOriginalQuery(string sortExpr)
    {
        var result = GetSampleProducts().AddSorting(new SortingField(sortExpr, SortingDirection.Ascending), ["Price"]).ToList();
        Assert.Equal(6, result.Count);
    }

    // =========================================================================
    // 7. Advanced Search Combinations & Edge Cases
    // =========================================================================

    [Theory]
    [InlineData("Name=Alpha Laptop,Exact=true", 1)]
    [InlineData("Exact=true,Name=Alpha Laptop", 1)]
    [InlineData("Name=Alpha Laptop,Exact=1", 1)]
    [InlineData("Name=Alpha,Exact=true", 0)] // Partial match fails on exact
    [InlineData("Name=Alpha,Exact=false", 1)] // Partial match succeeds
    public void AddSearch_ExactMatchFlagPositions_ShouldWorkUniformly(string searchExpr, int expectedCount)
    {
        var result = GetSampleProducts().AddSearch(searchExpr, ["Name"]).ToList();
        Assert.Equal(expectedCount, result.Count);
    }

    [Theory]
    [InlineData("Name=Laptop,Category=Computers", 2)]
    [InlineData("Category=Peripherals,Name=Mouse", 1)]
    [InlineData("Category=Accessories,Name=NonExistent", 0)]
    public void AddSearch_MultiFieldSearch_ShouldIntersect(string searchExpr, int expectedCount)
    {
        var result = GetSampleProducts().AddSearch(searchExpr, ["Name", "Category"]).ToList();
        Assert.Equal(expectedCount, result.Count);
    }

    [Theory]
    [InlineData("====")]
    [InlineData(",,,,")]
    [InlineData("PropWithoutValue=")]
    [InlineData("=ValWithoutProp")]
    [InlineData("Name=")]
    public void AddSearch_MalformedSearchExpressions_ShouldBeSafelyIgnored(string malformed)
    {
        var result = GetSampleProducts().AddSearch(malformed, ["Name"]).ToList();
        Assert.Equal(6, result.Count);
    }

    // =========================================================================
    // 8. Chained WhereIf & Paging Edge Cases
    // =========================================================================

    [Theory]
    [InlineData(true, true, 1)]   // Computers & Price > 1300 -> Alpha Laptop (1500)
    [InlineData(true, false, 2)]  // Computers only -> Alpha Laptop, Epsilon Laptop
    [InlineData(false, true, 1)]  // Price > 1300 only -> Alpha Laptop (1500)
    [InlineData(false, false, 6)] // Neither -> all 6
    public void WhereIf_ChainedConditions_ShouldEvaluateCorrectly(bool filterCategory, bool filterPrice, int expectedCount)
    {
        var query = GetSampleProducts()
            .WhereIf(filterCategory, p => p.Category == "Computers")
            .WhereIf(filterPrice, p => p.Price > 1300);

        var result = query.ToList();
        Assert.Equal(expectedCount, result.Count);
    }

    [Theory]
    [InlineData(1, 2, 2, 3)] // Page 1 of 2 items: 2 items returned, 3 total pages
    [InlineData(2, 2, 2, 3)] // Page 2 of 2 items: 2 items returned, 3 total pages
    [InlineData(3, 2, 2, 3)] // Page 3 of 2 items: 2 items returned, 3 total pages
    [InlineData(4, 2, 0, 3)] // Page 4 of 2 items: 0 items returned (past end)
    [InlineData(1, 10, 6, 1)] // Page 1 of 10 items: 6 items returned, 1 total page
    public void UsePaging_VariousPageSizesAndNumbers_ShouldSliceCorrectly(int page, int pageSize, int expectedItems, int expectedTotalPages)
    {
        var options = new PagingOptions(page, pageSize);
        var pagedItems = GetSampleProducts().UsePaging(options).ToList();

        Assert.Equal(expectedItems, pagedItems.Count);
        Assert.Equal(expectedTotalPages, (int)Math.Ceiling(6.0 / pageSize));
    }
}
