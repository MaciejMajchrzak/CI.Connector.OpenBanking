namespace CI.Connector.OpenBanking.Domain.Entities;

public sealed class BankStatementLine
{
    public Guid     Id                { get; init; } = Guid.NewGuid();
    public Guid     StatementId       { get; init; }
    public Guid     TenantId          { get; init; }
    public DateOnly ValueDate         { get; init; }
    public decimal  Amount            { get; init; }
    public string   Currency          { get; init; } = string.Empty;
    public string   Description       { get; init; } = string.Empty;
    public string?  Reference         { get; init; }
    public string   Status            { get; set; }  = "Unmatched";
    public Guid?    MatchedInvoiceId  { get; set; }
    public Guid?    MatchedPaymentId  { get; set; }
}
