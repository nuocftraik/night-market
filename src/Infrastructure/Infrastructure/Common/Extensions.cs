using System.Reflection;
using NightMarket.WebApi.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace NightMarket.WebApi.Infrastructure.Common;

internal static class Extensions
{
    /// <summary>
    /// Auto-register all services implementing ITransientService or IScopedService
    /// </summary>
    internal static IServiceCollection AddServices(this IServiceCollection services) =>
        services
            .AddServices(typeof(ITransientService), ServiceLifetime.Transient)
            .AddServices(typeof(IScopedService), ServiceLifetime.Scoped);

    /// <summary>
    /// Scan assemblies and register services implementing specified marker interface
    /// </summary>
    internal static IServiceCollection AddServices(
        this IServiceCollection services,
        Type markerInterfaceType,
        ServiceLifetime lifetime)
    {
        // Get all assemblies in current AppDomain
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        // Scan for types implementing marker interface
        var implementationTypes = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type =>
                type.IsClass &&
                !type.IsAbstract &&
                !type.IsGenericType &&
                markerInterfaceType.IsAssignableFrom(type))
            .ToList();

        foreach (var implementationType in implementationTypes)
        {
            // Get business interfaces (exclude marker interfaces)
            var serviceInterfaces = implementationType.GetInterfaces()
                .Where(i => i != markerInterfaceType &&
                    !typeof(ITransientService).IsAssignableFrom(i) &&
                    !typeof(IScopedService).IsAssignableFrom(i))
                .ToList();

            // Register with first business interface found
            if (serviceInterfaces.Any())
            {
                var serviceInterface = serviceInterfaces.First();
                services.Add(new ServiceDescriptor(
                    serviceInterface,
                    implementationType,
                    lifetime));
            }
            else
            {
                // Unconventional: No business interface, just register the class itself
                services.Add(new ServiceDescriptor(
                    implementationType,
                    implementationType,
                    lifetime));
            }
        }

        return services;
    }
}
