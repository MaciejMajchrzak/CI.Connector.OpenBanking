using CI.Kernel;

namespace CI.Connector.OpenBanking.Domain.Entities;

public sealed class BankStatement : BaseEntity
{
    public Guid            ConnectionId     { get; init; }
    public DateOnly        StatementDate    { get; init; }
    public string          AccountIban      { get; init; } = string.Empty;
    public string          Currency         { get; init; } = string.Empty;
    public decimal         OpeningBalance   { get; init; }
    public decimal         ClosingBalance   { get; init; }
    public int             TransactionCount { get; init; }
    public DateTimeOffset  ImportedAt       { get; init; } = DateTimeOffset.UtcNow;

    public BankConnection?          Connection { get; init; }
    public List<BankStatementLine>  Lines      { get; init; } = [];
}
