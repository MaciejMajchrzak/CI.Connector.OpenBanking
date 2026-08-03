using System.Security.Cryptography;
using System.Text;
using CI.Kernel;
using CI.Connector.OpenBanking.Core.Commands;
using CI.Connector.OpenBanking.Core.DTOs;
using CI.Connector.OpenBanking.Domain.Entities;
using CI.Connector.OpenBanking.Domain.Events;

namespace CI.Connector.OpenBanking.Core.Handlers;

[NoEvent("event written to outbox")]
public sealed class ConnectBankHandler(IOpenBankingRepository repo, IOpenBankingOutbox outbox)
    : ICommandHandler<ConnectBankCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(ConnectBankCommand cmd, CancellationToken ct = default)
    {
        var connection = new BankConnection
        {
            TenantId          = cmd.TenantId,
            CreatedBy         = cmd.CreatedBy,
            UpdatedBy         = cmd.CreatedBy,
            Name              = cmd.Name,
            BankCode          = cmd.BankCode,
            AccountIban       = cmd.AccountIban,
            AccessTokenHash   = HashToken(cmd.PlainAccessToken),
            AccessTokenPrefix = TokenPrefix(cmd.PlainAccessToken),
            RefreshTokenHash  = cmd.PlainRefreshToken is not null ? HashToken(cmd.PlainRefreshToken) : null,
            ExpiresAt         = cmd.ExpiresAt,
            IsActive          = true,
            InstalledBy       = cmd.CreatedBy,
        };

        await repo.AddConnectionAsync(connection, ct);
        await outbox.WriteAsync(cmd.TenantId,
            new BankConnectionEstablishedEvent(connection.Id, cmd.TenantId, cmd.BankCode, cmd.AccountIban), ct);
        var save = await repo.SaveChangesAsync(ct);
        if (!save.IsSuccess) return Result<Guid>.Failure(save.ErrorCode!);

        return Result<Guid>.Success(connection.Id);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }

    private static string TokenPrefix(string token) =>
        token.Length >= 8 ? token[..8] : token;
}

[NoEvent("event written to outbox")]
public sealed class DisconnectBankHandler(IOpenBankingRepository repo, IOpenBankingOutbox outbox)
    : ICommandHandler<DisconnectBankCommand>
{
    public async Task<Result> HandleAsync(DisconnectBankCommand cmd, CancellationToken ct = default)
    {
        var connection = await repo.FindConnectionAsync(cmd.ConnectionId, cmd.TenantId, ct);
        if (connection is null) return Result.Failure(ErrorCodes.NOT_FOUND);

        connection.IsActive  = false;
        connection.UpdatedAt = DateTimeOffset.UtcNow;

        await outbox.WriteAsync(cmd.TenantId,
            new BankConnectionDisabledEvent(connection.Id, cmd.TenantId, connection.BankCode), ct);
        var save = await repo.SaveChangesAsync(ct);
        if (!save.IsSuccess) return save;

        return Result.Success();
    }
}

[NoEvent("internal token refresh — no domain event")]
public sealed class RefreshConnectionHandler(IOpenBankingRepository repo)
    : ICommandHandler<RefreshConnectionCommand>
{
    public async Task<Result> HandleAsync(RefreshConnectionCommand cmd, CancellationToken ct = default)
    {
        var connection = await repo.FindConnectionAsync(cmd.ConnectionId, cmd.TenantId, ct);
        if (connection is null) return Result.Failure(ErrorCodes.NOT_FOUND);

        connection.AccessTokenHash   = HashToken(cmd.PlainNewAccessToken);
        connection.AccessTokenPrefix = TokenPrefix(cmd.PlainNewAccessToken);
        connection.ExpiresAt         = cmd.ExpiresAt;
        connection.UpdatedAt         = DateTimeOffset.UtcNow;
        connection.UpdatedBy         = cmd.UpdatedBy;

        return await repo.SaveChangesAsync(ct);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }

    private static string TokenPrefix(string token) =>
        token.Length >= 8 ? token[..8] : token;
}

[NoEvent("query — read only")]
public sealed class GetConnectionHandler(IOpenBankingRepository repo)
    : ICommandHandler<GetConnectionQuery, ConnectionDto>
{
    public async Task<Result<ConnectionDto>> HandleAsync(GetConnectionQuery cmd, CancellationToken ct = default)
    {
        var c = await repo.FindConnectionAsync(cmd.ConnectionId, cmd.TenantId, ct);
        if (c is null) return Result<ConnectionDto>.Failure(ErrorCodes.NOT_FOUND);

        return Result<ConnectionDto>.Success(new ConnectionDto(
            c.Id, c.TenantId, c.Name, c.BankCode, c.AccountIban,
            c.AccessTokenPrefix, c.ExpiresAt, c.IsActive,
            c.LastSyncAt, c.LastSyncStatus, c.CreatedAt, c.RowVersion));
    }
}

[NoEvent("query — read only")]
public sealed class ListConnectionsHandler(IOpenBankingRepository repo)
    : ICommandHandler<ListConnectionsQuery, PagedResult<ConnectionSummaryDto>>
{
    public async Task<Result<PagedResult<ConnectionSummaryDto>>> HandleAsync(
        ListConnectionsQuery cmd, CancellationToken ct = default)
    {
        var result = await repo.ListConnectionsAsync(cmd.TenantId, cmd.Page, cmd.PageSize, ct);
        return Result<PagedResult<ConnectionSummaryDto>>.Success(result);
    }
}
