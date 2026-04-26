using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Yarp.ReverseProxy.Transforms;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

var categoryOptions = new[] { "식비", "카페", "교통", "쇼핑", "생활", "기타" };
var categoryOptionsText = string.Join(", ", categoryOptions);

// 팀장님의 핵심 기능: 영수증 텍스트를 받아서 AI 카테고리 제안
app.MapPost("/api/ai/suggest-category", async Task<IResult> (SuggestCategoryRequest request, Kernel k, App.Data.AppDbContext db, ILogger<Program> logger) =>
{
    if (request.ReceiptId == Guid.Empty)
    {
        logger.LogWarning("suggest-category 요청 거부: receiptId가 비어 있음");
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.ReceiptId)] = ["receiptId는 비어 있을 수 없습니다."]
        });
    }

    if (string.IsNullOrWhiteSpace(request.OcrText))
    {
        logger.LogWarning("suggest-category 요청 거부: ocrText가 비어 있음");
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.OcrText)] = ["ocrText는 비어 있을 수 없습니다."]
        });
    }

    var promptTemplate = """
        당신은 가계부 정리 전문가입니다. 
        아래의 영수증 텍스트를 분석하여 [{CATEGORY_OPTIONS}] 중 가장 적절한 카테고리 하나를 추천하세요.
        응답은 반드시 아래 JSON 형식으로만 하세요.
        { "category": "카테고리명", "confidence": 0.0~1.0 사이의 숫자 }

        영수증 내용:
        """;

    promptTemplate = promptTemplate.Replace("{CATEGORY_OPTIONS}", categoryOptionsText);

    var prompt = promptTemplate + request.OcrText;

    var result = await k.InvokePromptAsync(prompt);
    var responseText = result.ToString();

    try
    {
        var parsed = JsonSerializer.Deserialize<SuggestCategoryAiResponse>(responseText);

        if (parsed is null || string.IsNullOrWhiteSpace(parsed.Category))
        {
            logger.LogWarning("AI 응답 파싱 실패: category 누락 또는 null. raw={ResponseText}", responseText);
            return Results.Problem(
                detail: "AI 응답을 해석할 수 없습니다.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        var category = parsed.Category.Trim();
        if (category.Length > 200)
        {
            category = category[..200];
        }

        var confidence = Math.Clamp(parsed.Confidence, 0d, 1d);

        var log = new App.Models.AiInferenceLog
        {
            ReceiptId = request.ReceiptId,
            SuggestedCategory = category,
            Confidence = confidence
        };

        db.AiInferenceLogs.Add(log);
        await db.SaveChangesAsync();

        logger.LogInformation("AI 추천 로그 저장 완료. LogId={LogId}, ReceiptId={ReceiptId}, Category={Category}, Confidence={Confidence}",
            log.Id, request.ReceiptId, category, confidence);

        return Results.Ok(new SuggestCategoryResult(
            log.Id,
            category,
            confidence));
    }
    catch (DbUpdateException)
    {
        logger.LogError("AI 추천 로그 DB 저장 실패. ReceiptId={ReceiptId}", request.ReceiptId);
        return Results.Problem(
            detail: "AI 추천 로그 저장 중 오류가 발생했습니다.",
            statusCode: StatusCodes.Status500InternalServerError);
    }
    catch (JsonException)
    {
        logger.LogWarning("AI 응답 JSON 형식 오류. raw={ResponseText}", responseText);
        return Results.Problem(
            detail: "AI 응답 형식이 올바르지 않습니다.",
            statusCode: StatusCodes.Status502BadGateway);
    }
})
.WithName("SuggestCategory")
.WithSummary("영수증 OCR 텍스트 기반 AI 카테고리 추천")
.WithDescription(
    "OCR 텍스트를 기반으로 AI가 카테고리와 신뢰도를 추천하고, 추천 결과를 AiInferenceLogs에 저장합니다.\n\n"
    + "요청 예시:\n"
    + "{\n"
    + "  \"receiptId\": \"11111111-1111-1111-1111-111111111111\",\n"
    + "  \"ocrText\": \"스타벅스 아메리카노 4500원\"\n"
    + "}\n\n"
    + "성공 응답 예시(200):\n"
    + "{\n"
    + "  \"logId\": \"22222222-2222-2222-2222-222222222222\",\n"
    + "  \"category\": \"카페\",\n"
    + "  \"confidence\": 0.93\n"
    + "}")
.Accepts<SuggestCategoryRequest>("application/json")
.Produces<SuggestCategoryResult>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.ProducesValidationProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status500InternalServerError)
.ProducesProblem(StatusCodes.Status502BadGateway);

//GateWay 실행

// 모든 API 요청을 설정된 마이크로서비스(Auth, Receipt 등)로 전달
app.MapReverseProxy();

app.Run();

public sealed record SuggestCategoryRequest(Guid ReceiptId, string OcrText);
public sealed record SuggestCategoryAiResponse(string Category, double Confidence);
public sealed record SuggestCategoryResult(Guid LogId, string Category, double Confidence);
