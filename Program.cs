using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Yarp.ReverseProxy.Transforms;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// EF Core DbContext 등록
builder.Services.AddDbContext<App.Data.AppDbContext>(options =>
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

// 3. AI 비즈니스 로직: 카테고리 제안 엔드포인트

// 팀장님의 핵심 기능: 영수증 텍스트를 받아서 AI 카테고리 제안
app.MapPost("/api/ai/suggest-category", async (string ocrText, Kernel k) =>
{
    var promptTemplate = """
        당신은 가계부 정리 전문가입니다. 
        아래의 영수증 텍스트를 분석하여 [식비, 카페, 교통, 쇼핑, 생활, 기타] 중 가장 적절한 카테고리 하나를 추천하세요.
        응답은 반드시 아래 JSON 형식으로만 하세요.
        { "category": "카테고리명", "confidence": 0.0~1.0 사이의 숫자 }

        영수증 내용:
        """;

    var prompt = promptTemplate + ocrText;

    var result = await k.InvokePromptAsync(prompt);
    return Results.Ok(result.ToString());
})
.WithName("SuggestCategory");

//GateWay 실행

// 모든 API 요청을 설정된 마이크로서비스(Auth, Receipt 등)로 전달
app.MapReverseProxy();

app.Run();
