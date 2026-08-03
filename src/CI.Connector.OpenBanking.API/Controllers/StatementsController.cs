using CI.Kernel;
using CI.Kernel.Http;
using CI.Connector.OpenBanking.Core.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CI.Connector.OpenBanking.API.Controllers;

[ApiController]
[Route("api/openbanking/statements")]
[Authorize]
[AllowWithoutModule]
public sealed class StatementsController(ICommandBus commandBus) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid      tenantId,
        [FromQuery] int       page         = 1,
        [FromQuery] int       pageSize     = 20,
        [FromQuery] Guid?     connectionId = null,
        [FromQuery] DateOnly? from         = null,
        [FromQuery] DateOnly? to           = null,
        CancellationToken ct = default)
    {
        var result = await commandBus.SendAsync(
            new ListStatementsQuery(tenantId, page, pageSize, connectionId, from, to), ct);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, [FromQuery] Guid tenantId, CancellationToken ct = default)
    {
        var result = await commandBus.SendAsync(new GetStatementQuery(id, tenantId), ct);
        if (!result.IsSuccess) return NotFound();
        if (HttpContext.IsNotModified(result.Value!.RowVersion)) return StatusCode(304);
        Response.Headers.ETag = ETagHelper.ToETag(result.Value.RowVersion);
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Import([FromBody] ImportStatementRequest req, CancellationToken ct = default)
    {
        var lines = req.Lines
            .Select(l => new StatementLineInput(l.ValueDate, l.Amount, l.Currency, l.Description, l.Reference))
            .ToList();
        var result = await commandBus.SendAsync(
            new ImportStatementCommand(req.ConnectionId, req.TenantId, req.CreatedBy,
                req.StatementDate, req.AccountIban, req.Currency,
                req.OpeningBalance, req.ClosingBalance, lines), ct);
        return result.IsSuccess                              ? CreatedAtAction(nameof(Get), new { id = result.Value }, result.Value)
            : result.ErrorCode == ErrorCodes.NOT_FOUND      ? NotFound()
            : Conflict(result.ErrorCode);
    }

    [HttpPost("{statementId:guid}/lines/{lineId:guid}/match")]
    public async Task<IActionResult> MatchLine(
        Guid statementId, Guid lineId,
        [FromBody] MatchLineRequest req,
        CancellationToken ct = default)
    {
        var result = await commandBus.SendAsync(
            new MatchStatementLineCommand(lineId, statementId, req.TenantId, req.MatchedInvoiceId, req.MatchedPaymentId), ct);
        return result.IsSuccess                                ? NoContent()
            : result.ErrorCode == ErrorCodes.NOT_FOUND        ? NotFound()
            : result.ErrorCode == ErrorCodes.VALIDATION       ? BadRequest(result.ErrorCode)
            : Conflict(result.ErrorCode);
    }

    [HttpPost("{statementId:guid}/lines/{lineId:guid}/ignore")]
    public async Task<IActionResult> IgnoreLine(
        Guid statementId, Guid lineId,
        [FromBody] IgnoreLineRequest req,
        CancellationToken ct = default)
    {
        var result = await commandBus.SendAsync(
            new IgnoreStatementLineCommand(lineId, statementId, req.TenantId), ct);
        return result.IsSuccess                         ? NoContent()
            : result.ErrorCode == ErrorCodes.NOT_FOUND  ? NotFound()
            : Conflict(result.ErrorCode);
    }
}

public record ImportStatementRequest(
    Guid                          ConnectionId,
    Guid                          TenantId,
    Guid                          CreatedBy,
    DateOnly                      StatementDate,
    string                        AccountIban,
    string                        Currency,
    decimal                       OpeningBalance,
    decimal                       ClosingBalance,
    List<StatementLineInputRequest> Lines);

public record StatementLineInputRequest(
    DateOnly ValueDate,
    decimal  Amount,
    string   Currency,
    string   Description,
    string?  Reference);

public record MatchLineRequest(Guid TenantId, Guid? MatchedInvoiceId, Guid? MatchedPaymentId);
public record IgnoreLineRequest(Guid TenantId);
