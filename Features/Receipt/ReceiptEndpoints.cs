using App.Features.AI.Data;
using App.Features.AI.Services;
using App.Features.Receipt.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.SemanticKernel;
using System.Security.Claims;

namespace App.Features.Receipt;

public static class ReceiptEndpoints
{
    public static void MapReceiptEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/receipts").WithTags("Receipt").RequireAuthorization();

        group.MapPost("/upload", Upload)
            .WithName("UploadReceipt")
            .WithSummary("영수증 업로드 및 OCR/AI 처리")
            .WithDescription("OCR 텍스트를 입력하면 상호명/금액/날짜를 파싱하고 AI 카테고리를 추천합니다.")
            .Accepts<UploadReceiptRequest>("application/json")
            .Produces<UploadReceiptResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/", GetList)
            .WithName("GetReceiptList")
            .WithSummary("영수증 목록 조회")
            .Produces<ReceiptListResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/{receiptId:guid}/confirm", ConfirmCategory)
            .WithName("ConfirmReceiptCategory")
            .WithSummary("영수증 카테고리 확정")
            .WithDescription("AI가 추천한 카테고리를 사용자가 최종 확정합니다.")
            .Accepts<ConfirmReceiptCategoryRequest>("application/json")
            .Produces<ConfirmReceiptCategoryResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<Results<Created<UploadReceiptResult>, ValidationProblem, UnauthorizedHttpResult>> Upload(
        UploadReceiptRequest request,
        ClaimsPrincipal principal,
        ReceiptService receiptService,
        Kernel kernel,
        AppDbContext db,
        ILogger<Program> logger)
    {
        if (!TryGetUserId(principal, out var userId))
            return TypedResults.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.RawText))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.RawText)] = ["OCR 텍스트는 필수입니다."]
            });
        }

        var result = await receiptService.ProcessAsync(userId, request, kernel, db);
        logger.LogInformation("영수증 업로드 완료. ReceiptId={ReceiptId}, Status={Status}", result.ReceiptId, result.Status);

        return TypedResults.Created($"/api/receipts/{result.ReceiptId}", result);
    }

    private static async Task<Results<Ok<ReceiptListResult>, ValidationProblem, UnauthorizedHttpResult>> GetList(
        ClaimsPrincipal principal,
        ReceiptService receiptService,
        AppDbContext db,
        int page = 1,
        int pageSize = 20)
    {
        if (!TryGetUserId(principal, out var userId))
            return TypedResults.Unauthorized();

        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["pagination"] = ["page >= 1, pageSize는 1~100 사이여야 합니다."]
            });
        }

        var result = await receiptService.GetListAsync(userId, page, pageSize, db);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<ConfirmReceiptCategoryResult>, NotFound, UnauthorizedHttpResult>> ConfirmCategory(
        Guid receiptId,
        ConfirmReceiptCategoryRequest request,
        ClaimsPrincipal principal,
        ReceiptService receiptService,
        AppDbContext db,
        ILogger<Program> logger)
    {
        if (!TryGetUserId(principal, out var userId))
            return TypedResults.Unauthorized();

        var result = await receiptService.ConfirmCategoryAsync(receiptId, userId, request.FinalCategory, db);
        if (result is null)
            return TypedResults.NotFound();

        logger.LogInformation("영수증 카테고리 확정. ReceiptId={ReceiptId}, Category={Category}, AiCorrect={AiCorrect}",
            receiptId, result.FinalCategory, result.AiWasCorrect);

        return TypedResults.Ok(result);
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        userId = Guid.Empty;
        var raw = principal.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return raw is not null && Guid.TryParse(raw, out userId);
    }
}