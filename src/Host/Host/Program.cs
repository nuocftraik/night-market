using NightMarket.WebApi.Application;
using NightMarket.WebApi.Host.Configurations;
using NightMarket.WebApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// 1. Load configurations
builder.AddConfigurations();

// 2. Add services to DI container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 3. Add layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// 4. Add Swagger
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "NightMarket.WebApi",
        Version = "v1",
        Description = "Night Market E-Commerce API built with Clean Architecture"
    });
});

// 5. Build application
var app = builder.Build();

// 6. Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 7. Use Infrastructure middleware
app.UseInfrastructure(builder.Configuration);

// 8. Map endpoints
app.MapEndpoints();

// 9. Run application
app.Run();
