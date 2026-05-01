using System.Text;
using App.Features.AI.Data;
using App.Features.AI.Services;
using App.Features.Analysis;
using App.Features.Auth;
using App.Features.Receipt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;

#pragma warning disable SKEXP0070 // Google connector is preview

namespace App.Features.Bootstrap;

public static class BootstrapServiceCollectionExtensions
{
    public static IServiceCollection AddAppBootstrap(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // Swagger
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { Title = "No More Receipts API", Version = "v1" });
        });

        // Gateway (YARP)
        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"));

        // JWT Authentication
        var jwtSecret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret이 appsettings.json에 설정되어 있지 않습니다.");
        var jwtIssuer = configuration["Jwt:Issuer"] ?? "NoMoreReceipts";
        var jwtAudience = configuration["Jwt:Audience"] ?? "NoMoreReceiptsUsers";

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
                };
            });

        services.AddAuthorization();

        // Semantic Kernel (Gemini via Google AI)
        var geminiKey = configuration["AI:GeminiKey"]
            ?? throw new InvalidOperationException("AI:GeminiKey가 설정되어 있지 않습니다. dotnet user-secrets로 설정하세요.");
        var geminiModel = configuration["AI:GeminiModel"] ?? "gemini-2.5-flash-lite";

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddGoogleAIGeminiChatCompletion(modelId: geminiModel, apiKey: geminiKey);
        var kernel = kernelBuilder.Build();
        services.AddSingleton(kernel);

        // Services - AI
        services.AddScoped<AiAccuracyService>();
        services.AddScoped<AiSuggestionService>();
        services.AddScoped<AiConfirmationService>();
        services.AddScoped<AiLogQueryService>();
        services.AddScoped<AiDashboardService>();

        // Services - Analysis
        services.AddScoped<AnalysisService>();

        // Services - Auth
        services.AddScoped<AuthService>();

        // Services - Receipt
        services.AddScoped<OcrService>();
        services.AddScoped<ReceiptService>();

        return services;
    }
}