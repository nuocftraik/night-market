namespace NightMarket.WebApi.Host.Configurations;

internal static class Startup
{
    internal static WebApplicationBuilder AddConfigurations(
        this WebApplicationBuilder builder)
    {
        const string configurationsDirectory = "Configurations";
        var env = builder.Environment;

        builder.Configuration
            // Base configurations
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)

            // Module configurations
            .AddJsonFile($"{configurationsDirectory}/database.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"{configurationsDirectory}/database.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)

            // Logger configuration
            .AddJsonFile($"{configurationsDirectory}/logger.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"{configurationsDirectory}/logger.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)

            // Security configuration
            .AddJsonFile($"{configurationsDirectory}/security.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"{configurationsDirectory}/security.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)

            // Environment variables (override JSON)
            .AddEnvironmentVariables();

        return builder;
    }
}
