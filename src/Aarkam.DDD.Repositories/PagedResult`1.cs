using System;
using System.Collections.Generic;

namespace Aarkam.Domain.Repositories;

/// <summary>
/// Specifies the direction in which a collection is sorted.
/// </summary>
public enum SortingDirection
{
    Ascending = 1,
    Descending
}

/// <summary>
/// Specifies the logical operation to use when combining multiple filters.
/// </summary>
public enum FilterOperation
{
    And = 1,
    Or = 2,
}

/// <summary>
/// Configuration for paginated queries.
/// </summary>
public class PagingOptions
{
    /// <summary>
    /// Default number of items per page.
    /// </summary>
    public const int DefaultPageSize = 20;
    private const int _minimumPageNumber = 1;
    private const int _minimumPageSize = 1;
    private int _pageNumber = _minimumPageNumber;
    private int _pageSize = DefaultPageSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="PagingOptions"/> class with specified page number and size.
    /// </summary>
    /// <param name="pageNumber">The numeric page number to retrieve.</param>
    /// <param name="pageSize">The number of items per page.</param>
    public PagingOptions(int pageNumber, int pageSize)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PagingOptions"/> class with default values.
    /// </summary>
    public PagingOptions()
    {
    }

    /// <summary>
    /// Gets or sets the one-based page number.
    /// </summary>
    public int PageNumber
    {
        get => _pageNumber;
        set
        {
            if (value < _minimumPageNumber)
            {
                throw new ArgumentException($"{nameof(PageNumber)} should be above one.");
            }

            _pageNumber = value;
        }
    }

    /// <summary>
    /// Gets or sets the number of items per page.
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        set
        {
            _pageSize = Math.Max(value, _minimumPageSize);
        }
    }
}

/// <summary>
/// Model for requesting a paginated, filtered, and sorted collection of entities.
/// </summary>
public class PaginationModel
{
    /// <summary>
    /// Gets or sets the paging configuration.
    /// </summary>
    public PagingOptions PagingOptions { get; set; } = new PagingOptions(1, 10);

    /// <summary>
    /// Gets or sets the search expression (e.g., "PropName = Value").
    /// </summary>
    public string? SearchExpression { get; set; }

    /// <summary>
    /// Gets or sets multiple filter expressions.
    /// </summary>
    public IEnumerable<FilterItem>? Filters { get; set; }

    /// <summary>
    /// Gets or sets the sorting configuration.
    /// </summary>
    public SortingField SortingField { get; set; } = new SortingField()
    {
        Fields = "Id",
        Direction = SortingDirection.Ascending
    };
}

/// <summary>
/// Represents a container for a paginated subset of data.
/// </summary>
/// <typeparam name="TEntity">The type of entities in the collection.</typeparam>
public class PagedResult<TEntity>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PagedResult{TEntity}"/> class.
    /// </summary>
    /// <param name="totalCount">Total number of items matching the query.</param>
    /// <param name="currentPage">The current page number (one-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="items">The items for the current page.</param>
    public PagedResult(int totalCount, int currentPage, int pageSize, IReadOnlyCollection<TEntity>? items)
    {
        TotalCount = totalCount < 0 ? 0 : totalCount;
        CurrentPage = currentPage < 1 ? 1 : currentPage;
        PageSize = pageSize < 1 ? PagingOptions.DefaultPageSize : pageSize;
        Items = items ?? Array.Empty<TEntity>();
    }

    /// <summary>
    /// Gets an empty paged result.
    /// </summary>
    public static PagedResult<TEntity> Empty => new PagedResult<TEntity>(0, 1, PagingOptions.DefaultPageSize, Array.Empty<TEntity>());

    /// <summary>
    /// Gets the total count of items across all pages.
    /// </summary>
    public int TotalCount { get; }

    /// <summary>
    /// Gets the current page number.
    /// </summary>
    public int CurrentPage { get; }

    /// <summary>
    /// Gets the current page number (synonym for <see cref="CurrentPage"/>).
    /// </summary>
    public int PageNumber => CurrentPage;

    /// <summary>
    /// Gets the number of items per page.
    /// </summary>
    public int PageSize { get; }

    /// <summary>
    /// Gets the total number of pages calculated from <see cref="TotalCount"/> and <see cref="PageSize"/>.
    /// </summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>
    /// Gets a value indicating whether there is a previous page available.
    /// </summary>
    public bool HasPreviousPage => CurrentPage > 1 && TotalPages > 0;

    /// <summary>
    /// Gets a value indicating whether there is a next page available.
    /// </summary>
    public bool HasNextPage => CurrentPage < TotalPages;

    /// <summary>
    /// Gets the collection of items for the current page.
    /// </summary>
    public IReadOnlyCollection<TEntity> Items { get; }
}

/// <summary>
/// Represents a field and direction to use when sorting data.
/// </summary>
public class SortingField
{
    public SortingField() { }

    /// <summary>
    /// Initializes a new instance of the SortingField class with the specified fields and sorting direction.
    /// </summary>
    public SortingField(string fields, SortingDirection direction)
    {
        Fields = fields;
        Direction = direction;
    }

    /// <summary>
    /// Gets or sets a comma-separated list of field names to sort by.
    /// </summary>
    public string Fields { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the direction in which sorting is applied.
    /// </summary>
    public SortingDirection Direction { get; set; } = SortingDirection.Ascending;
}
