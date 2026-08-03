using CI.Kernel;
using CI.Connector.OpenBanking.Core.DTOs;
using CI.Connector.OpenBanking.Domain.Entities;

namespace CI.Connector.OpenBanking.Core;

public interface IOpenBankingRepository
{
    Task<BankConnection?>                         FindConnectionAsync(Guid connectionId, Guid tenantId, CancellationToken ct = default);
    Task<PagedResult<ConnectionSummaryDto>>        ListConnectionsAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default);
    Task                                           AddConnectionAsync(BankConnection connection, CancellationToken ct = default);

    Task<BankStatement?>                           FindStatementAsync(Guid statementId, Guid tenantId, CancellationToken ct = default);
    Task<PagedResult<StatementSummaryDto>>         ListStatementsAsync(Guid tenantId, int page, int pageSize, Guid? connectionId, DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task                                           AddStatementAsync(BankStatement statement, CancellationToken ct = default);

    Task<BankStatementLine?>                       FindStatementLineAsync(Guid lineId, Guid statementId, Guid tenantId, CancellationToken ct = default);

    Task<Result>                                   SaveChangesAsync(CancellationToken ct = default);
}
