using CI.Kernel;

namespace CI.Connector.OpenBanking.Domain.Events;

public record BankConnectionEstablishedEvent(
    Guid   ConnectionId,
    Guid   TenantId,
    string BankCode,
    string AccountIban) : IEvent;

public record BankConnectionDisabledEvent(
    Guid   ConnectionId,
    Guid   TenantId,
    string BankCode) : IEvent;

public record BankConnectionExpiredEvent(
    Guid   ConnectionId,
    Guid   TenantId,
    string BankCode) : IEvent;

public record BankStatementImportedEvent(
    Guid     StatementId,
    Guid     TenantId,
    Guid     ConnectionId,
    DateOnly StatementDate,
    int      TransactionCount) : IEvent;

public record BankStatementLineMatchedEvent(
    Guid  LineId,
    Guid  StatementId,
    Guid  TenantId,
    Guid? MatchedInvoiceId,
    Guid? MatchedPaymentId) : IEvent;
