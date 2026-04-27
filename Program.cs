using Microsoft.SemanticKernel;
using Microsoft.EntityFrameworkCore;
using App.Features.AI;
using App.Features.AI.Data;
using App.Features.Gateway;
using App.Features.Health;

var builder = WebApplication.CreateBuilder(args);

// EF Core DbContext 등록
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

//  1. 서비스 등록 (Dependency Injection) 

// Swagger API
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// YARP Gateway: appsettings.json의 설정을 읽어와 라우팅 구성
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Semantic Kernel: AI 엔진 설정
var kernelBuilder = Kernel.CreateBuilder();

// OpenAI 설정 (실제 키는 appsettings.json이나 User Secrets에 넣으세요)
kernelBuilder.AddOpenAIChatCompletion(
    modelId: "gpt-4o",
    apiKey: builder.Configuration["AI:OpenAIKey"] ?? "YOUR_API_KEY");

var kernel = kernelBuilder.Build();
builder.Services.AddSingleton(kernel);

// 환경변수나 User Secrets에서 민감 정보를 읽어올 수 있도록 구성 예시
// 예: AI__OpenAIKey 환경변수를 설정하면 builder.Configuration["AI:OpenAIKey"]로 접근 가능

var app = builder.Build();

// 2. 미들웨어 설정 (Pipeline)

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapHealthEndpoints();
app.MapAiEndpoints();
app.MapGatewayEndpoints();

app.Run();
