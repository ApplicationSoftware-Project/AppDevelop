using System.Text;
using App.Features.AI.Data;
using App.Features.AI.Pipeline;
using App.Features.AI.Services;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using App.Features.Analysis;
using App.Features.Auth;
using App.Features.Auth.Grpc;
using App.Features.Auth.GrpcServices;
using App.Features.Gateway;
using App.Features.Receipt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.SemanticKernel;
using Yarp.ReverseProxy.Transforms.Builder;

#pragma warning disable SKEXP0070 // Google connector is preview

namespace App.Features.Bootstrap;

public static class BootstrapServiceCollectionExtensions
{
    public static IServiceCollection AddAppBootstrap(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        // Swagger
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { Title = "No More Receipts API", Version = "v1" });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT 토큰만 입력하세요 (앞에 'Bearer ' 붙이지 않음)."
            });

            c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", hostDocument: doc, externalResource: null)] = new List<string>()
            });
        });

        // Gateway (YARP) + JWT → 헤더 변환
        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"))
            .AddTransforms<JwtToHeaderTransformProvider>();

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
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSecret))
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

        // 멀티스텝 파이프라인 플러그인 등록
        kernel.Plugins.AddFromObject(new ReceiptAnalysisPlugin(), "ReceiptAnalysis");

        services.AddSingleton(kernel);

        // Services - AI
        services.AddScoped<AiAccuracyService>();
        services.AddScoped<AiSuggestionService>();
        services.AddScoped<AiConfirmationService>();
        services.AddScoped<AiLogQueryService>();
        services.AddScoped<AiDashboardService>();
        services.AddScoped<AiReceiptPipelineService>();

        // Services - Analysis
        services.AddScoped<AnalysisService>();

        // Services - Auth
        services.AddScoped<AuthService>();

        // Services - Receipt
        services.AddScoped<OcrService>();
        services.AddScoped<ReceiptService>();

        // gRPC 서버
        services.AddGrpc();
        services.AddGrpcReflection();

        // gRPC 클라이언트 (Receipt, Analysis 등 다른 서비스가 Auth gRPC를 호출할 때 사용)
        var grpcAuthUrl = configuration["Grpc:AuthServiceUrl"] ?? "https://localhost:65289";
        services.AddGrpcClient<UserService.UserServiceClient>(o =>
        {
            o.Address = new Uri(grpcAuthUrl);
        });

        // OpenTelemetry 분산 추적 + 메트릭
        var serviceName    = configuration["OpenTelemetry:ServiceName"]    ?? "no-more-receipts";
        var serviceVersion = configuration["OpenTelemetry:ServiceVersion"] ?? "1.0.0";

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName, serviceVersion: serviceVersion))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(o =>
                {
                    o.RecordException = true;
                    o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/swagger");
                })
                .AddHttpClientInstrumentation()
                .AddSource(AiPipelineActivitySource.Name)
                .AddConsoleExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddConsoleExporter());

        return services;
    }
}