using NightMarket.WebApi.Application.Common.Interfaces;
using NightMarket.WebApi.Application.Identity.Roles;

namespace NightMarket.WebApi.Application.Identity.Functions;

public interface IFunctionService : ITransientService
{
    Task<List<FunctionDto>> GetListAsync(CancellationToken cancellationToken);
    Task<FunctionDto> GetByIdAsync(string id);
    Task<string> CreateOrUpdateAsync(CreateOrUpdateFunctionRequest request);
    Task<string> DeleteAsync(string id);
}
