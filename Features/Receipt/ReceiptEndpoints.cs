using App.Features.AI.Data;
using App.Features.Receipt.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using System.Security.Claims;

namespace App.Features.Receipt;

public static class ReceiptEndpoints
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    public static void MapReceiptEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/receipts").WithTags("Receipt").RequireAuthorization();

        group.MapPost("/upload", Upload)
            .WithName("UploadReceipt")
            .WithSummary("영수증 이미지 업로드 및 OCR/AI 처리")
            .WithDescription("multipart/form-data로 영수증 이미지를 업로드하면 OCR 후 AI 카테고리를 추천합니다. 영수증이 아닌 이미지는 422로 거부됩니다.")
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<UploadReceiptResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/", GetList)
            .WithName("GetReceiptList")
            .WithSummary("영수증 목록 조회")
            .Produces<ReceiptListResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/{receiptId:guid}", GetDetail)
            .WithName("GetReceiptDetail")
            .WithSummary("영수증 단건 조회")
            .Produces<ReceiptDetail>(StatusCodes.Status200OK)
            .Produces<ApiError>(StatusCodes.Status404NotFound)
            .Produces<ApiError>(StatusCodes.Status401Unauthorized);

        group.MapGet("/{receiptId:guid}/image", GetImage)
            .WithName("GetReceiptImage")
            .WithSummary("영수증 이미지 다운로드")
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg", additionalContentTypes: ["image/png", "image/webp"])
            .Produces<ApiError>(StatusCodes.Status404NotFound)
            .Produces<ApiError>(StatusCodes.Status401Unauthorized);

        group.MapPut("/{receiptId:guid}", Update)
            .WithName("UpdateReceipt")
            .WithSummary("영수증 기초 정보 수정 (상호명·금액·날짜·카테고리)")
            // [이슈 3 수정] Swagger 예시 날짜 포맷 ISO 8601로 수정 (슬래시 → T 구분자)
            .WithDescription("""
                잘못 올라간 영수증의 기초 정보를 수정합니다.
                null로 보낸 필드는 변경되지 않습니다 (Partial Update).

                요청 예시:
                {
                  "storeName": "스타벅스",
                  "amount": 4500,
                  "purchasedAt": "2026-01-10T12:00:00+09:00",
                  "category": "카페"
                }

                성공 응답 예시(200):
                {
                  "receiptId": "11111111-1111-1111-1111-111111111111",
                  "storeName": "스타벅스",
                  "amount": 4500,
                  "purchasedAt": "2026-01-10T03:00:00+00:00",
                  "category": "카페",
                  "status": "AiCategorized",
                  "updatedAt": "2026-01-10T05:00:00+00:00"
                }
                """)
            .Accepts<UpdateReceiptRequest>("application/json")
            .Produces<UpdateReceiptResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces<ApiError>(StatusCodes.Status404NotFound)
            .Produces<ApiError>(StatusCodes.Status401Unauthorized);

        group.MapDelete("/{receiptId:guid}", Delete)
            .WithName("DeleteReceipt")
            .WithSummary("영수증 삭제 (이미지 파일 포함)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiError>(StatusCodes.Status404NotFound)
            .Produces<ApiError>(StatusCodes.Status401Unauthorized);

        group.MapPost("/{receiptId:guid}/confirm", ConfirmCategory)
            .WithName("ConfirmReceiptCategory")
            .WithSummary("영수증 카테고리 확정")
            .WithDescription("AI가 추천한 카테고리를 사용자가 최종 확정합니다.")
            .Accepts<ConfirmReceiptCategoryRequest>("application/json")
            .Produces<ConfirmReceiptCategoryResult>(StatusCodes.Status200OK)
            .Produces<ApiError>(StatusCodes.Status404NotFound)
            .Produces<ApiError>(StatusCodes.Status401Unauthorized);
    }

    private static JsonHttpResult<ApiError> Unauthorized() =>
        TypedResults.Json(new ApiError("인증이 필요합니다."), statusCode: StatusCodes.Status401Unauthorized);

    private static JsonHttpResult<ApiError> NotFoundJson(string message) =>
        TypedResults.Json(new ApiError(message), statusCode: StatusCodes.Status404NotFound);

    private static async Task<Results<Created<UploadReceiptResult>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult>> Upload(
        IFormFile file,
        ClaimsPrincipal principal,
        ReceiptService receiptService,
        Kernel kernel,
        AppDbContext db,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
            return TypedResults.Unauthorized();

        var errors = ValidateFile(file);
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        var result = await receiptService.ProcessAsync(userId, file, kernel, db, ct);
        if (result is null)
        {
            return TypedResults.Problem(
                detail: "업로드된 이미지가 영수증으로 인식되지 않았습니다. 영수증 이미지를 다시 업로드해 주세요.",
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Not a receipt");
        }

        logger.LogInformation("영수증 업로드 완료. ReceiptId={ReceiptId}, Status={Status}, Warnings={WarningCount}",
            result.ReceiptId, result.Status, result.Warnings.Count);

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

    // [이슈 4 수정] 검증 레이어를 엔드포인트 한 곳으로 통일, 서비스의 truncate 제거
    private static async Task<Results<Ok<UpdateReceiptResult>, ValidationProblem, JsonHttpResult<ApiError>>> Update(
        Guid receiptId,
        UpdateReceiptRequest request,
        ClaimsPrincipal principal,
        ReceiptService receiptService,
        AppDbContext db,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
            return Unauthorized();

        // 수정할 필드가 하나도 없으면 거부
        if (request.StoreName is null && request.Amount is null
            && request.PurchasedAt is null && request.Category is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = ["수정할 필드를 하나 이상 입력하세요."]
            });
        }

        // 상호명 200자 초과 → 400 (서비스에서 truncate 제거했으므로 여기서만 검증)
        if (request.StoreName is not null && request.StoreName.Trim().Length > 200)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.StoreName)] = ["상호명은 200자 이하여야 합니다."]
            });
        }

        // 음수 금액 → 400 (서비스에서 조건 제거했으므로 여기서만 검증)
        if (request.Amount is < 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Amount)] = ["금액은 0 이상이어야 합니다."]
            });
        }

        var result = await receiptService.UpdateAsync(receiptId, userId, request, db, ct);
        if (result is null)
            return NotFoundJson("영수증을 찾을 수 없습니다.");

        logger.LogInformation(
            "영수증 수정 완료. ReceiptId={ReceiptId}, UserId={UserId}, UpdatedAt={UpdatedAt}",
            receiptId, userId, result.UpdatedAt);

        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<ConfirmReceiptCategoryResult>, JsonHttpResult<ApiError>>> ConfirmCategory(
        Guid receiptId,
        ConfirmReceiptCategoryRequest request,
        ClaimsPrincipal principal,
        ReceiptService receiptService,
        AppDbContext db,
        ILogger<Program> logger)
    {
        if (!TryGetUserId(principal, out var userId))
            return Unauthorized();

        var result = await receiptService.ConfirmCategoryAsync(receiptId, userId, request.FinalCategory, db);
        if (result is null)
            return NotFoundJson("영수증 카테고리 확정에 실패했습니다.");

        logger.LogInformation("영수증 카테고리 확정. ReceiptId={ReceiptId}, Category={Category}, AiCorrect={AiCorrect}",
            receiptId, result.FinalCategory, result.AiWasCorrect);

        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<ReceiptDetail>, JsonHttpResult<ApiError>>> GetDetail(
        Guid receiptId,
        ClaimsPrincipal principal,
        ReceiptService receiptService,
        AppDbContext db)
    {
        if (!TryGetUserId(principal, out var userId))
            return Unauthorized();

        var detail = await receiptService.GetDetailAsync(receiptId, userId, db);
        return detail is null
            ? NotFoundJson("영수증 조회에 실패했습니다.")
            : TypedResults.Ok(detail);
    }

    private static async Task<Results<PhysicalFileHttpResult, JsonHttpResult<ApiError>>> GetImage(
        Guid receiptId,
        ClaimsPrincipal principal,
        ReceiptService receiptService,
        AppDbContext db,
        HttpContext http)
    {
        if (!TryGetUserId(principal, out var userId))
            return Unauthorized();

        var image = await receiptService.GetImageAsync(receiptId, userId, db);
        if (image is null) return NotFoundJson("영수증 이미지를 찾을 수 없습니다.");

        http.Response.Headers.CacheControl = "private, max-age=3600";
        return TypedResults.PhysicalFile(image.Value.AbsolutePath, image.Value.ContentType);
    }

    private static async Task<Results<NoContent, JsonHttpResult<ApiError>>> Delete(
        Guid receiptId,
        ClaimsPrincipal principal,
        ReceiptService receiptService,
        AppDbContext db,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
            return Unauthorized();

        var deleted = await receiptService.DeleteAsync(receiptId, userId, db, ct);
        if (!deleted) return NotFoundJson("영수증 삭제에 실패했습니다.");

        logger.LogInformation("영수증 삭제 완료. ReceiptId={ReceiptId}, UserId={UserId}", receiptId, userId);
        return TypedResults.NoContent();
    }

    private static Dictionary<string, string[]> ValidateFile(IFormFile? file)
    {
        var errors = new Dictionary<string, string[]>();
        if (file is null || file.Length == 0)
        {
            errors["file"] = ["영수증 이미지 파일은 필수입니다."];
            return errors;
        }
        if (file.Length > MaxFileSizeBytes)
            errors["file"] = [$"파일 크기는 {MaxFileSizeBytes / (1024 * 1024)}MB 이하여야 합니다."];
        else if (!AllowedContentTypes.Contains(file.ContentType))
            errors["file"] = ["허용되는 형식: image/jpeg, image/png, image/webp"];
        else if (!AllowedExtensions.Contains(Path.GetExtension(file.FileName)))
            errors["file"] = ["허용되는 확장자: .jpg, .jpeg, .png, .webp"];
        return errors;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        userId = Guid.Empty;
        var raw = principal.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return raw is not null && Guid.TryParse(raw, out userId);
    }
}