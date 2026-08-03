using CI.Kernel;
using CI.Connector.OpenBanking.Core;
using CI.Connector.OpenBanking.Core.DTOs;
using CI.Connector.OpenBanking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CI.Connector.OpenBanking.Infrastructure;

public sealed class OpenBankingRepository(OpenBankingDbContext db) : IOpenBankingRepository
{
    public Task<BankConnection?> FindConnectionAsync(Guid connectionId, Guid tenantId, CancellationToken ct = default) =>
        db.Connections
          .Where(c => c.Id == connectionId && c.TenantId == tenantId)
          .FirstOrDefaultAsync(ct);

    public async Task<PagedResult<ConnectionSummaryDto>> ListConnectionsAsync(
        Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        var q = db.Connections.Where(c => c.TenantId == tenantId);
        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ConnectionSummaryDto(
                c.Id, c.Name, c.BankCode, c.AccountIban,
                c.IsActive, c.LastSyncAt, c.LastSyncStatus))
            .ToListAsync(ct);
        return new PagedResult<ConnectionSummaryDto>(items, page, pageSize, total);
    }

    public Task AddConnectionAsync(BankConnection connection, CancellationToken ct = default)
    {
        db.Connections.Add(connection);
        return Task.CompletedTask;
    }

    public Task<BankStatement?> FindStatementAsync(Guid statementId, Guid tenantId, CancellationToken ct = default) =>
        db.Statements
          .Include(s => s.Lines)
          .Where(s => s.Id == statementId && s.TenantId == tenantId)
          .FirstOrDefaultAsync(ct);

    public async Task<PagedResult<StatementSummaryDto>> ListStatementsAsync(
        Guid tenantId, int page, int pageSize,
        Guid? connectionId, DateOnly? from, DateOnly? to,
        CancellationToken ct = default)
    {
        var q = db.Statements.Where(s => s.TenantId == tenantId);
        if (connectionId.HasValue) q = q.Where(s => s.ConnectionId == connectionId.Value);
        if (from.HasValue)         q = q.Where(s => s.StatementDate >= from.Value);
        if (to.HasValue)           q = q.Where(s => s.StatementDate <= to.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(s => s.StatementDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StatementSummaryDto(
                s.Id, s.StatementDate, s.AccountIban, s.Currency,
                s.OpeningBalance, s.ClosingBalance, s.TransactionCount, s.ImportedAt))
            .ToListAsync(ct);
        return new PagedResult<StatementSummaryDto>(items, page, pageSize, total);
    }

    public Task AddStatementAsync(BankStatement statement, CancellationToken ct = default)
    {
        db.Statements.Add(statement);
        return Task.CompletedTask;
    }

    public Task<BankStatementLine?> FindStatementLineAsync(
        Guid lineId, Guid statementId, Guid tenantId, CancellationToken ct = default) =>
        db.StatementLines
          .Where(l => l.Id == lineId && l.StatementId == statementId && l.TenantId == tenantId)
          .FirstOrDefaultAsync(ct);

    public async Task<Result> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(ErrorCodes.ROWVERSION_CONFLICT);
        }
    }
}
