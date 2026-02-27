using Ardalis.Specification.EntityFrameworkCore;
using NightMarket.WebApi.Application.Common.Persistence;
using NightMarket.WebApi.Domain.Common.Contracts;
using NightMarket.WebApi.Infrastructure.Persistence.Context;

namespace NightMarket.WebApi.Infrastructure.Persistence.Repository;

/// <summary>
/// EF Core implementation của Repository Pattern với Ardalis.Specification
/// </summary>
public class ApplicationDbRepository<T> : RepositoryBase<T>, IReadRepository<T>, IRepository<T>
    where T : class, IAggregateRoot
{
    public ApplicationDbRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }
}
