using CI.Kernel;
using CI.Kernel.Http;
using CI.Connector.OpenBanking.Core.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CI.Connector.OpenBanking.API.Controllers;

[ApiController]
[Route("api/openbanking/connections")]
[Authorize]
[AllowWithoutModule]
public sealed class ConnectionsController(ICommandBus commandBus) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid tenantId,
        [FromQuery] int  page     = 1,
        [FromQuery] int  pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await commandBus.SendAsync(new ListConnectionsQuery(tenantId, page, pageSize), ct);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, [FromQuery] Guid tenantId, CancellationToken ct = default)
    {
        var result = await commandBus.SendAsync(new GetConnectionQuery(id, tenantId), ct);
        if (!result.IsSuccess) return NotFound();
        if (HttpContext.IsNotModified(result.Value!.RowVersion)) return StatusCode(304);
        Response.Headers.ETag = ETagHelper.ToETag(result.Value.RowVersion);
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Connect([FromBody] ConnectBankRequest req, CancellationToken ct = default)
    {
        var result = await commandBus.SendAsync(
            new ConnectBankCommand(req.TenantId, req.CreatedBy, req.Name, req.BankCode, req.AccountIban,
                req.PlainAccessToken, req.PlainRefreshToken, req.ExpiresAt), ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value }, result.Value)
            : Conflict(result.ErrorCode);
    }

    [HttpPost("{id:guid}/disconnect")]
    public async Task<IActionResult> Disconnect(Guid id, [FromBody] DisconnectBankRequest req, CancellationToken ct = default)
    {
        var result = await commandBus.SendAsync(new DisconnectBankCommand(id, req.TenantId), ct);
        return result.IsSuccess              ? NoContent()
            : result.ErrorCode == ErrorCodes.NOT_FOUND ? NotFound()
            : Conflict(result.ErrorCode);
    }

    [HttpPut("{id:guid}/refresh")]
    public async Task<IActionResult> Refresh(Guid id, [FromBody] RefreshConnectionRequest req, CancellationToken ct = default)
    {
        var result = await commandBus.SendAsync(
            new RefreshConnectionCommand(id, req.TenantId, req.UpdatedBy, req.PlainNewAccessToken, req.ExpiresAt), ct);
        return result.IsSuccess              ? NoContent()
            : result.ErrorCode == ErrorCodes.NOT_FOUND ? NotFound()
            : Conflict(result.ErrorCode);
    }
}

public record ConnectBankRequest(
    Guid            TenantId,
    Guid            CreatedBy,
    string          Name,
    string          BankCode,
    string          AccountIban,
    string          PlainAccessToken,
    string?         PlainRefreshToken,
    DateTimeOffset? ExpiresAt);

public record DisconnectBankRequest(Guid TenantId);

public record RefreshConnectionRequest(
    Guid            TenantId,
    Guid            UpdatedBy,
    string          PlainNewAccessToken,
    DateTimeOffset? ExpiresAt);
