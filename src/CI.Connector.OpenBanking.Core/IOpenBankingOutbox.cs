using CI.Kernel;

namespace CI.Connector.OpenBanking.Core;

public interface IOpenBankingOutbox
{
    Task WriteAsync(Guid tenantId, IEvent evt, CancellationToken ct = default);
}
