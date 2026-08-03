using CI.Kernel;
using CI.Connector.OpenBanking.Core.Commands;
using CI.Connector.OpenBanking.Core.DTOs;
using CI.Connector.OpenBanking.Domain.Entities;
using CI.Connector.OpenBanking.Domain.Events;

namespace CI.Connector.OpenBanking.Core.Handlers;

[NoEvent("event written to outbox")]
public sealed class ImportStatementHandler(IOpenBankingRepository repo, IOpenBankingOutbox outbox)
    : ICommandHandler<ImportStatementCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(ImportStatementCommand cmd, CancellationToken ct = default)
    {
        var connection = await repo.FindConnectionAsync(cmd.ConnectionId, cmd.TenantId, ct);
        if (connection is null) return Result<Guid>.Failure(ErrorCodes.NOT_FOUND);

        var statement = new BankStatement
        {
            TenantId         = cmd.TenantId,
            CreatedBy        = cmd.CreatedBy,
            UpdatedBy        = cmd.CreatedBy,
            ConnectionId     = cmd.ConnectionId,
            StatementDate    = cmd.StatementDate,
            AccountIban      = cmd.AccountIban,
            Currency         = cmd.Currency,
            OpeningBalance   = cmd.OpeningBalance,
            ClosingBalance   = cmd.ClosingBalance,
            TransactionCount = cmd.Lines.Count,
            ImportedAt       = DateTimeOffset.UtcNow,
        };

        var lines = cmd.Lines.Select(l => new BankStatementLine
        {
            StatementId = statement.Id,
            TenantId    = cmd.TenantId,
            ValueDate   = l.ValueDate,
            Amount      = l.Amount,
            Currency    = l.Currency,
            Description = l.Description,
            Reference   = l.Reference,
            Status      = "Unmatched",
        }).ToList();

        statement.Lines.AddRange(lines);

        await repo.AddStatementAsync(statement, ct);

        connection.LastSyncAt     = DateTimeOffset.UtcNow;
        connection.LastSyncStatus = "Success";
        connection.UpdatedAt      = DateTimeOffset.UtcNow;

        await outbox.WriteAsync(cmd.TenantId,
            new BankStatementImportedEvent(statement.Id, cmd.TenantId, cmd.ConnectionId, cmd.StatementDate, cmd.Lines.Count), ct);

        var save = await repo.SaveChangesAsync(ct);
        if (!save.IsSuccess) return Result<Guid>.Failure(save.ErrorCode!);

        return Result<Guid>.Success(statement.Id);
    }
}

[NoEvent("event written to outbox")]
public sealed class MatchStatementLineHandler(IOpenBankingRepository repo, IOpenBankingOutbox outbox)
    : ICommandHandler<MatchStatementLineCommand>
{
    public async Task<Result> HandleAsync(MatchStatementLineCommand cmd, CancellationToken ct = default)
    {
        // Exactly one of MatchedInvoiceId or MatchedPaymentId must be set (XOR)
        var hasInvoice = cmd.MatchedInvoiceId.HasValue;
        var hasPayment = cmd.MatchedPaymentId.HasValue;
        if (hasInvoice == hasPayment)
            return Result.Failure(ErrorCodes.VALIDATION);

        var line = await repo.FindStatementLineAsync(cmd.LineId, cmd.StatementId, cmd.TenantId, ct);
        if (line is null) return Result.Failure(ErrorCodes.NOT_FOUND);

        line.Status           = "Matched";
        line.MatchedInvoiceId = cmd.MatchedInvoiceId;
        line.MatchedPaymentId = cmd.MatchedPaymentId;

        await outbox.WriteAsync(cmd.TenantId,
            new BankStatementLineMatchedEvent(line.Id, cmd.StatementId, cmd.TenantId, cmd.MatchedInvoiceId, cmd.MatchedPaymentId), ct);

        return await repo.SaveChangesAsync(ct);
    }
}

[NoEvent("ignore action — no event needed")]
public sealed class IgnoreStatementLineHandler(IOpenBankingRepository repo)
    : ICommandHandler<IgnoreStatementLineCommand>
{
    public async Task<Result> HandleAsync(IgnoreStatementLineCommand cmd, CancellationToken ct = default)
    {
        var line = await repo.FindStatementLineAsync(cmd.LineId, cmd.StatementId, cmd.TenantId, ct);
        if (line is null) return Result.Failure(ErrorCodes.NOT_FOUND);

        line.Status = "Ignored";

        return await repo.SaveChangesAsync(ct);
    }
}

[NoEvent("query — read only")]
public sealed class GetStatementHandler(IOpenBankingRepository repo)
    : ICommandHandler<GetStatementQuery, StatementDto>
{
    public async Task<Result<StatementDto>> HandleAsync(GetStatementQuery cmd, CancellationToken ct = default)
    {
        var s = await repo.FindStatementAsync(cmd.StatementId, cmd.TenantId, ct);
        if (s is null) return Result<StatementDto>.Failure(ErrorCodes.NOT_FOUND);

        var lines = s.Lines.Select(l => new StatementLineDto(
            l.Id, l.StatementId, l.ValueDate, l.Amount, l.Currency,
            l.Description, l.Reference, l.Status, l.MatchedInvoiceId, l.MatchedPaymentId)).ToList();

        return Result<StatementDto>.Success(new StatementDto(
            s.Id, s.TenantId, s.ConnectionId, s.StatementDate, s.AccountIban,
            s.Currency, s.OpeningBalance, s.ClosingBalance, s.TransactionCount,
            s.ImportedAt, lines, s.CreatedAt, s.RowVersion));
    }
}

[NoEvent("query — read only")]
public sealed class ListStatementsHandler(IOpenBankingRepository repo)
    : ICommandHandler<ListStatementsQuery, PagedResult<StatementSummaryDto>>
{
    public async Task<Result<PagedResult<StatementSummaryDto>>> HandleAsync(
        ListStatementsQuery cmd, CancellationToken ct = default)
    {
        var result = await repo.ListStatementsAsync(
            cmd.TenantId, cmd.Page, cmd.PageSize, cmd.ConnectionId, cmd.From, cmd.To, ct);
        return Result<PagedResult<StatementSummaryDto>>.Success(result);
    }
}
