using App.Features.AI.Data;
using App.Features.AI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

namespace App.Features.Bootstrap;

public static class BootstrapServiceCollectionExtensions
{
    public static IServiceCollection AddAppBootstrap(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"));

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddOpenAIChatCompletion(
            modelId: "gpt-4o",
            apiKey: configuration["AI:OpenAIKey"] ?? "YOUR_API_KEY");

        var kernel = kernelBuilder.Build();
        services.AddSingleton(kernel);

        services.AddScoped<AiAccuracyService>();
        services.AddScoped<AiSuggestionService>();
        services.AddScoped<AiConfirmationService>();
        services.AddScoped<AiLogQueryService>();
        services.AddScoped<AiDashboardService>();
        services.AddScoped<AiPendingFeedbackService>();

        return services;
    }
}
