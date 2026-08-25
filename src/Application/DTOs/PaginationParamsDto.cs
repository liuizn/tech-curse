namespace TechCurse.Application.DTOs;

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

/// <summary>
/// Resultado paginado. Imutável de propósito: as propriedades só têm getter, e o
/// construtor é o único caminho de entrada. Antes, <c>Items</c> era não-anulável
/// com setter público — a garantia de não-nulidade dependia de ninguém atribuir
/// <c>null</c> depois, o que o compilador não verifica em um setter público.
/// Sendo somente-leitura, a garantia passa a ser estrutural.
/// <para>
/// O <c>System.Text.Json</c> desserializa pelo construtor (é o único público, e
/// os nomes dos parâmetros casam com os das propriedades), então o round-trip
/// pelo cache Redis continua funcionando.
/// </para>
/// </summary>
public class PagedResultDto<T>
{
    public IEnumerable<T> Items { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public int TotalCount { get; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public PagedResultDto(IEnumerable<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items ?? throw new ArgumentNullException(nameof(items), "A coleção de itens da página não pode ser nula.");
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }
}
