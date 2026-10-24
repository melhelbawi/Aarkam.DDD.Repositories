using Aarkam.Domain.Repositories;
using Xunit;

namespace Aarkam.DDD.Repositories.Tests;

public class PagedResultTests
{
    // =========================================================================
    // 1. PagingOptions Tests
    // =========================================================================

    [Fact]
    public void PagingOptions_DefaultConstructor_ShouldHaveDefaults()
    {
        var options = new PagingOptions();

        Assert.Equal(1, options.PageNumber);
        Assert.Equal(20, options.PageSize);
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 25)]
    [InlineData(5, 50)]
    [InlineData(100, 100)]
    public void PagingOptions_CustomConstructor_ShouldSetValues(int pageNumber, int pageSize)
    {
        var options = new PagingOptions(pageNumber, pageSize);

        Assert.Equal(pageNumber, options.PageNumber);
        Assert.Equal(pageSize, options.PageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void PagingOptions_InvalidPageNumber_ShouldThrowArgumentException(int invalidPageNumber)
    {
        Assert.Throws<ArgumentException>(() => new PagingOptions(invalidPageNumber, 20));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50)]
    public void PagingOptions_InvalidPageSize_ShouldClampToMinimum(int invalidPageSize)
    {
        var options = new PagingOptions(1, invalidPageSize);

        Assert.True(options.PageSize >= 1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void PagingOptions_PropertySetter_InvalidPageNumber_ShouldThrow(int invalidValue)
    {
        var options = new PagingOptions();
        Assert.Throws<ArgumentException>(() => options.PageNumber = invalidValue);
    }

    // =========================================================================
    // 2. PagedResult TotalPages Calculation Tests
    // =========================================================================

    [Theory]
    [InlineData(0, 10, 0)]      // 0 items -> 0 pages
    [InlineData(1, 10, 1)]      // 1 item -> 1 page
    [InlineData(9, 10, 1)]      // 9 items -> 1 page
    [InlineData(10, 10, 1)]     // 10 items, size 10 -> 1 page
    [InlineData(11, 10, 2)]     // 11 items -> 2 pages
    [InlineData(19, 10, 2)]     // 19 items -> 2 pages
    [InlineData(20, 10, 2)]     // 20 items -> 2 pages
    [InlineData(21, 10, 3)]     // 21 items -> 3 pages
    [InlineData(99, 20, 5)]     // 99 items, size 20 -> 5 pages
    [InlineData(100, 20, 5)]    // 100 items, size 20 -> 5 pages
    [InlineData(101, 20, 6)]    // 101 items, size 20 -> 6 pages
    [InlineData(1000, 50, 20)]  // 1000 items, size 50 -> 20 pages
    [InlineData(1001, 50, 21)]  // 1001 items, size 50 -> 21 pages
    public void PagedResult_TotalPages_ShouldCalculateCorrectly(int totalCount, int pageSize, int expectedTotalPages)
    {
        var result = new PagedResult<int>(totalCount, 1, pageSize, new List<int>());

        Assert.Equal(expectedTotalPages, result.TotalPages);
        Assert.Equal(totalCount, result.TotalCount);
        Assert.Equal(pageSize, result.PageSize);
    }

    // =========================================================================
    // 3. PagedResult Navigation Flags Tests (HasPreviousPage, HasNextPage)
    // =========================================================================

    [Theory]
    [InlineData(1, 5, false, true)]   // Page 1 of 5 -> No previous, has next
    [InlineData(2, 5, true, true)]    // Page 2 of 5 -> Has previous, has next
    [InlineData(3, 5, true, true)]    // Page 3 of 5 -> Has previous, has next
    [InlineData(4, 5, true, true)]    // Page 4 of 5 -> Has previous, has next
    [InlineData(5, 5, true, false)]   // Page 5 of 5 -> Has previous, no next
    [InlineData(6, 5, true, false)]   // Page 6 of 5 (out of range) -> Has previous, no next
    [InlineData(1, 1, false, false)]  // Single page -> No previous, no next
    [InlineData(1, 0, false, false)]  // Zero total pages -> No previous, no next
    public void PagedResult_NavigationFlags_ShouldEvaluateCorrectly(
        int pageNumber, 
        int totalPages, 
        bool expectedHasPrevious, 
        bool expectedHasNext)
    {
        int totalCount = totalPages * 10;
        var result = new PagedResult<string>(totalCount, pageNumber, 10, new List<string>());

        Assert.Equal(expectedHasPrevious, result.HasPreviousPage);
        Assert.Equal(expectedHasNext, result.HasNextPage);
    }

    // =========================================================================
    // 4. PagedResult Edge Cases & Invariance Tests
    // =========================================================================

    [Fact]
    public void PagedResult_NullItems_ShouldDefaultToEmptyCollection()
    {
        var result = new PagedResult<string>(100, 1, 10, null!);

        Assert.NotNull(result.Items);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void PagedResult_WithItems_ShouldPreserveItemsAndCount()
    {
        var items = new List<string> { "Item 1", "Item 2", "Item 3" };
        var result = new PagedResult<string>(50, 1, 10, items);

        Assert.Equal(3, result.Items.Count);
        Assert.Equal("Item 1", result.Items.ElementAt(0));
        Assert.Equal("Item 2", result.Items.ElementAt(1));
        Assert.Equal("Item 3", result.Items.ElementAt(2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void PagedResult_NegativeTotalCount_ShouldClampToZero(int negativeCount)
    {
        var result = new PagedResult<int>(negativeCount, 1, 10, new List<int>());

        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void PagedResult_InvalidPageNumber_ShouldClampToOne(int invalidPageNumber)
    {
        var result = new PagedResult<int>(100, invalidPageNumber, 10, new List<int>());

        Assert.Equal(1, result.PageNumber);
        Assert.Equal(1, result.CurrentPage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-20)]
    public void PagedResult_InvalidPageSize_ShouldClampToDefault(int invalidPageSize)
    {
        var result = new PagedResult<int>(100, 1, invalidPageSize, new List<int>());

        Assert.Equal(PagingOptions.DefaultPageSize, result.PageSize);
    }

    [Fact]
    public void PagedResult_EmptyStatic_ShouldBeConsistent()
    {
        var empty = PagedResult<string>.Empty;

        Assert.Equal(0, empty.TotalCount);
        Assert.Equal(1, empty.CurrentPage);
        Assert.Equal(1, empty.PageNumber);
        Assert.Equal(0, empty.TotalPages);
        Assert.False(empty.HasPreviousPage);
        Assert.False(empty.HasNextPage);
        Assert.Empty(empty.Items);
    }

    // =========================================================================
    // 5. Enums Verification Tests
    // =========================================================================

    [Theory]
    [InlineData(SortingDirection.Ascending, 1)]
    [InlineData(SortingDirection.Descending, 2)]
    public void SortingDirection_Values_ShouldMatchExpectedIntegers(SortingDirection direction, int expectedValue)
    {
        Assert.Equal(expectedValue, (int)direction);
    }

    [Theory]
    [InlineData(FilterOperation.And, 1)]
    [InlineData(FilterOperation.Or, 2)]
    public void FilterOperation_Values_ShouldMatchExpectedIntegers(FilterOperation op, int expectedValue)
    {
        Assert.Equal(expectedValue, (int)op);
    }
}
