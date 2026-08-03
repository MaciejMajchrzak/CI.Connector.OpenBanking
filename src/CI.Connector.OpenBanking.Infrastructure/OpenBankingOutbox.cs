using System.Text.Json;
using CI.Kernel;
using CI.Connector.OpenBanking.Core;
using CI.Connector.OpenBanking.Domain.Entities;

namespace CI.Connector.OpenBanking.Infrastructure;

public sealed class OpenBankingOutbox(OpenBankingDbContext db) : IOpenBankingOutbox
{
    public async Task WriteAsync(Guid tenantId, IEvent evt, CancellationToken ct = default)
    {
        var msg = new OpenBankingOutboxMessage
        {
            TenantId  = tenantId,
            EventType = evt.GetType().FullName!,
            Payload   = JsonSerializer.Serialize(evt, evt.GetType()),
        };
        await db.OutboxMessages.AddAsync(msg, ct);
        // NO SaveChangesAsync — participates in handler's transaction
    }
}
