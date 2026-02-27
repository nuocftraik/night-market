using NightMarket.WebApi.Application.Common.Interfaces;
using NightMarket.WebApi.Infrastructure.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace NightMarket.WebApi.Infrastructure.Common;

internal static class Startup
{
    /// <summary>
    /// Register common services
    /// </summary>
    internal static IServiceCollection AddCommonServices(this IServiceCollection services)
    {
        // Register Serializer as Transient
        // Có thể bị override bởi auto-registration nếu dùng marker interfaces, nhưng register tường minh cũng tốt.
        services.AddTransient<ISerializerService, NewtonSoftService>();

        return services;
    }
}
