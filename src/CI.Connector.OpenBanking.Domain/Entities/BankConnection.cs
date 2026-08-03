using CI.Kernel;

namespace CI.Connector.OpenBanking.Domain.Entities;

public sealed class BankConnection : BaseEntity
{
    public string            Name              { get; set; } = string.Empty;
    public string            BankCode          { get; set; } = string.Empty;
    public string            AccountIban       { get; set; } = string.Empty;
    public string            AccessTokenHash   { get; set; } = string.Empty;
    public string            AccessTokenPrefix { get; set; } = string.Empty;
    public string?           RefreshTokenHash  { get; set; }
    public DateTimeOffset?   ExpiresAt         { get; set; }
    public bool              IsActive          { get; set; }
    public Guid              InstalledBy       { get; set; }
    public DateTimeOffset?   LastSyncAt        { get; set; }
    public string?           LastSyncStatus    { get; set; }

    public List<BankStatement> Statements { get; init; } = [];
}
