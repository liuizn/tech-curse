namespace TechCurse.src.Application.DTOs;

public record PaginationParamsDto
{
    private const int MaxPageSize = 50;

    public int PageNumber { get; init; } = 1;

    private int _pageSize = 10;
    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = (value > MaxPageSize) ? MaxPageSize : value;
    }

    public string SortBy { get; init; } = "Id";
    public string SortDirection { get; init; } = "asc";
}

public record CoursePaginationParamsDto : PaginationParamsDto
{
    public string? Categoria { get; init; }
}

public class PagedResultDto<T>
{
    public IEnumerable<T?> Items { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int) Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public PagedResultDto(IEnumerable<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }
}