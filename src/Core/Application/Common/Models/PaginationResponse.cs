namespace NightMarket.WebApi.Application.Common.Models;

public class PaginationResponse<T>
{
    public List<T> Data { get; set; }
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage => CurrentPage < TotalPages;
    public bool HasPreviousPage => CurrentPage > 1;

    public PaginationResponse(List<T> data, int count, int pageNumber, int pageSize)
    {
        Data = data;
        CurrentPage = pageNumber;
        PageSize = pageSize;
        TotalPages = pageSize > 0 ? (int)Math.Ceiling(count / (double)pageSize) : 0;
        TotalCount = count;
    }
}
