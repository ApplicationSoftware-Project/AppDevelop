using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.AuthService.Controllers;

/// <summary>
/// Permission 기반 접근 제어 예시 컨트롤러
/// [Authorize(Policy = "권한명")] 으로 엔드포인트별 세밀한 제어
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ResourceController : ControllerBase
{
    // "users:read" Permission이 있어야 접근 가능
    [HttpGet("users")]
    [Authorize(Policy = "users:read")]
    public IActionResult GetUsers()
        => Ok(new { message = "사용자 목록 조회 성공", requiredPermission = "users:read" });

    // "users:write" Permission이 있어야 접근 가능
    [HttpPost("users")]
    [Authorize(Policy = "users:write")]
    public IActionResult CreateUser()
        => Ok(new { message = "사용자 생성 성공", requiredPermission = "users:write" });

    // "users:delete" Permission이 있어야 접근 가능
    [HttpDelete("users/{id:guid}")]
    [Authorize(Policy = "users:delete")]
    public IActionResult DeleteUser(Guid id)
        => Ok(new { message = $"사용자 {id} 삭제 성공", requiredPermission = "users:delete" });

    // "orders:read" Permission이 있어야 접근 가능
    [HttpGet("orders")]
    [Authorize(Policy = "orders:read")]
    public IActionResult GetOrders()
        => Ok(new { message = "주문 목록 조회 성공", requiredPermission = "orders:read" });

    // "orders:write" Permission이 있어야 접근 가능
    [HttpPost("orders")]
    [Authorize(Policy = "orders:write")]
    public IActionResult CreateOrder()
        => Ok(new { message = "주문 생성 성공", requiredPermission = "orders:write" });

    // "reports:read" Permission이 있어야 접근 가능
    [HttpGet("reports")]
    [Authorize(Policy = "reports:read")]
    public IActionResult GetReports()
        => Ok(new { message = "리포트 조회 성공", requiredPermission = "reports:read" });
}
