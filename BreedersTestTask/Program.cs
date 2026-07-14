using System.Text.Json.Serialization;
using BreedersTestTask.Infrastructure;
using BreedersTestTask.Middleware;
using BreedersTestTask.Services.Implementation;
using BreedersTestTask.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Breeders Test Task API",
        Version = "v1"
    });

    options.AddSecurityDefinition("BreederId", new OpenApiSecurityScheme
    {
        Name = "X-Breeder-Id",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "Simulated authenticated breeder id"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference 
                { 
                    Type = ReferenceType.SecurityScheme, 
                    Id = "BreederId" 
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<BreedersDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=breeders.db"));

builder.Services.AddScoped<ILitterService, LitterService>();
builder.Services.AddScoped<INotificationService, ConsoleNotificationService>();
builder.Services.AddScoped<BreederContext>();
builder.Services.AddScoped<IBreederContext>(sp => sp.GetRequiredService<BreederContext>());

var app = builder.Build();

// Registered first so it can catch exceptions thrown anywhere later in the pipeline.
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<BreederAuthMiddleware>();

// Swagger is left enabled in all environments to make manual review of this test task easier.
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BreedersDbContext>();
    // EnsureCreated (not migrations) keeps this test task self-contained with zero setup steps.
    db.Database.EnsureCreated();
    SeedData.EnsureSeeded(db);
}

app.Run();
