namespace NightMarket.WebApi.Application.Identity.Functions;

public class CreateOrUpdateFunctionRequest
{
    public string? Id { get; set; }
    public string Name { get; set; } = default!;
    public string Url { get; set; } = default!;
    public int SortOrder { get; set; }
    public string? ParentId { get; set; }
    public List<string>? ActionIds { get; set; }
}
