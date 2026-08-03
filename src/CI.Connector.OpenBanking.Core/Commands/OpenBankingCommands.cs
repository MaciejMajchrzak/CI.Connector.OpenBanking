using CI.Kernel;
using CI.Connector.OpenBanking.Core.DTOs;

namespace CI.Connector.OpenBanking.Core.Commands;

public record ConnectBankCommand(
    Guid            TenantId,
    Guid            CreatedBy,
    string          Name,
    string          BankCode,
    string          AccountIban,
    string          PlainAccessToken,
    string?         PlainRefreshToken,
    DateTimeOffset? ExpiresAt
) : ICommand<Guid>;

public record DisconnectBankCommand(
    Guid ConnectionId,
    Guid TenantId
) : ICommand;

public record RefreshConnectionCommand(
    Guid            ConnectionId,
    Guid            TenantId,
    Guid            UpdatedBy,
    string          PlainNewAccessToken,
    DateTimeOffset? ExpiresAt
) : ICommand;

public record StatementLineInput(
    DateOnly ValueDate,
    decimal  Amount,
    string   Currency,
    string   Description,
    string?  Reference
);

public record ImportStatementCommand(
    Guid                     ConnectionId,
    Guid                     TenantId,
    Guid                     CreatedBy,
    DateOnly                 StatementDate,
    string                   AccountIban,
    string                   Currency,
    decimal                  OpeningBalance,
    decimal                  ClosingBalance,
    List<StatementLineInput> Lines
) : ICommand<Guid>;

public record MatchStatementLineCommand(
    Guid  LineId,
    Guid  StatementId,
    Guid  TenantId,
    Guid? MatchedInvoiceId,
    Guid? MatchedPaymentId
) : ICommand;

public record IgnoreStatementLineCommand(
    Guid LineId,
    Guid StatementId,
    Guid TenantId
) : ICommand;

public record GetConnectionQuery(Guid ConnectionId, Guid TenantId)
    : ICommand<ConnectionDto>;

public record ListConnectionsQuery(Guid TenantId, int Page, int PageSize)
    : ICommand<PagedResult<ConnectionSummaryDto>>;

public record GetStatementQuery(Guid StatementId, Guid TenantId)
    : ICommand<StatementDto>;

public record ListStatementsQuery(
    Guid     TenantId,
    int      Page,
    int      PageSize,
    Guid?    ConnectionId,
    DateOnly? From,
    DateOnly? To
) : ICommand<PagedResult<StatementSummaryDto>>;
