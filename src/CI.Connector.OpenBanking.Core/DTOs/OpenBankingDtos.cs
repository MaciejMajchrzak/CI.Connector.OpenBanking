namespace CI.Connector.OpenBanking.Core.DTOs;

public record ConnectionDto(
    Guid            Id,
    Guid            TenantId,
    string          Name,
    string          BankCode,
    string          AccountIban,
    string          AccessTokenPrefix,
    DateTimeOffset? ExpiresAt,
    bool            IsActive,
    DateTimeOffset? LastSyncAt,
    string?         LastSyncStatus,
    DateTimeOffset  CreatedAt,
    uint            RowVersion
);

public record ConnectionSummaryDto(
    Guid            Id,
    string          Name,
    string          BankCode,
    string          AccountIban,
    bool            IsActive,
    DateTimeOffset? LastSyncAt,
    string?         LastSyncStatus
);

public record StatementDto(
    Guid                  Id,
    Guid                  TenantId,
    Guid                  ConnectionId,
    DateOnly              StatementDate,
    string                AccountIban,
    string                Currency,
    decimal               OpeningBalance,
    decimal               ClosingBalance,
    int                   TransactionCount,
    DateTimeOffset        ImportedAt,
    List<StatementLineDto> Lines,
    DateTimeOffset        CreatedAt,
    uint                  RowVersion
);

public record StatementSummaryDto(
    Guid           Id,
    DateOnly       StatementDate,
    string         AccountIban,
    string         Currency,
    decimal        OpeningBalance,
    decimal        ClosingBalance,
    int            TransactionCount,
    DateTimeOffset ImportedAt
);

public record StatementLineDto(
    Guid     Id,
    Guid     StatementId,
    DateOnly ValueDate,
    decimal  Amount,
    string   Currency,
    string   Description,
    string?  Reference,
    string   Status,
    Guid?    MatchedInvoiceId,
    Guid?    MatchedPaymentId
);
