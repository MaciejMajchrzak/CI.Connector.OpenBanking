using CI.Kernel;
using CI.Connector.OpenBanking.Core.Commands;
using CI.Connector.OpenBanking.Core.Handlers;
using CI.Connector.OpenBanking.Domain.Events;
using CI.Connector.OpenBanking.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CI.Connector.OpenBanking.Tests;

public sealed class OpenBankingHandlerTests : IDisposable
{
    private static readonly Guid TenantId = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid UserId   = new("bbbbbbbb-0000-0000-0000-000000000001");

    private readonly OpenBankingDbContext       _db;
    private readonly OpenBankingOutbox          _outbox;
    private readonly OpenBankingRepository      _repo;
    private readonly ConnectBankHandler         _connect;
    private readonly DisconnectBankHandler      _disconnect;
    private readonly RefreshConnectionHandler   _refresh;
    private readonly GetConnectionHandler       _getConnection;
    private readonly ListConnectionsHandler     _listConnections;
    private readonly ImportStatementHandler     _importStatement;
    private readonly MatchStatementLineHandler  _matchLine;
    private readonly IgnoreStatementLineHandler _ignoreLine;
    private readonly GetStatementHandler        _getStatement;
    private readonly ListStatementsHandler      _listStatements;

    public OpenBankingHandlerTests()
    {
        var opts = new DbContextOptionsBuilder<OpenBankingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db              = new OpenBankingDbContext(opts);
        _outbox          = new OpenBankingOutbox(_db);
        _repo            = new OpenBankingRepository(_db);
        _connect         = new ConnectBankHandler(_repo, _outbox);
        _disconnect      = new DisconnectBankHandler(_repo, _outbox);
        _refresh         = new RefreshConnectionHandler(_repo);
        _getConnection   = new GetConnectionHandler(_repo);
        _listConnections = new ListConnectionsHandler(_repo);
        _importStatement = new ImportStatementHandler(_repo, _outbox);
        _matchLine       = new MatchStatementLineHandler(_repo, _outbox);
        _ignoreLine      = new IgnoreStatementLineHandler(_repo);
        _getStatement    = new GetStatementHandler(_repo);
        _listStatements  = new ListStatementsHandler(_repo);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> ConnectBankAsync(string bankCode = "PKO", string iban = "PL12345678901234567890123456")
    {
        var result = await _connect.HandleAsync(
            new ConnectBankCommand(TenantId, UserId, "My Bank", bankCode, iban,
                "plaintoken12345678", null, null));
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private async Task<Guid> ImportStatementAsync(Guid connectionId, IEnumerable<StatementLineInput>? lines = null)
    {
        var lineList = (lines ?? [new StatementLineInput(DateOnly.FromDateTime(DateTime.Today), 100m, "PLN", "Payment", null)]).ToList();
        var result = await _importStatement.HandleAsync(
            new ImportStatementCommand(
                connectionId, TenantId, UserId,
                DateOnly.FromDateTime(DateTime.Today),
                "PL12345678901234567890123456", "PLN",
                1000m, 900m, lineList));
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    // ── ConnectBank ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ConnectBank_returns_id()
    {
        var result = await _connect.HandleAsync(
            new ConnectBankCommand(TenantId, UserId, "PKO Bank", "PKO",
                "PL12345678901234567890123456", "plaintoken12345678", null, null));

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);
    }

    [Fact]
    public async Task ConnectBank_hashes_token_not_stored_plain()
    {
        const string plainToken = "plaintoken12345678";
        var id = await ConnectBankAsync();

        var dto = await _getConnection.HandleAsync(new GetConnectionQuery(id, TenantId));
        Assert.True(dto.IsSuccess);
        // The DTO has no AccessTokenHash field — only AccessTokenPrefix; verify via DB directly
        var conn = _db.Connections.First(c => c.Id == id);
        Assert.NotEqual(plainToken, conn.AccessTokenHash);
        Assert.Equal(64, conn.AccessTokenHash.Length); // SHA-256 hex = 64 chars
    }

    [Fact]
    public async Task ConnectBank_prefix_is_8_chars()
    {
        var id = await ConnectBankAsync();

        var dto = (await _getConnection.HandleAsync(new GetConnectionQuery(id, TenantId))).Value!;
        Assert.Equal(8, dto.AccessTokenPrefix.Length);
    }

    [Fact]
    public async Task ConnectBank_publishes_established_event()
    {
        await ConnectBankAsync();

        var count = _db.OutboxMessages.Count(m => m.EventType == typeof(BankConnectionEstablishedEvent).FullName);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ConnectBank_connection_is_active()
    {
        var id = await ConnectBankAsync();

        var dto = (await _getConnection.HandleAsync(new GetConnectionQuery(id, TenantId))).Value!;
        Assert.True(dto.IsActive);
    }

    // ── DisconnectBank ────────────────────────────────────────────────────────

    [Fact]
    public async Task DisconnectBank_sets_IsActive_false()
    {
        var id = await ConnectBankAsync();

        var result = await _disconnect.HandleAsync(new DisconnectBankCommand(id, TenantId));

        Assert.True(result.IsSuccess);
        var dto = (await _getConnection.HandleAsync(new GetConnectionQuery(id, TenantId))).Value!;
        Assert.False(dto.IsActive);
    }

    [Fact]
    public async Task DisconnectBank_publishes_disabled_event()
    {
        var id = await ConnectBankAsync();
        await _disconnect.HandleAsync(new DisconnectBankCommand(id, TenantId));

        var count = _db.OutboxMessages.Count(m => m.EventType == typeof(BankConnectionDisabledEvent).FullName);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task DisconnectBank_NOT_FOUND_for_unknown()
    {
        var result = await _disconnect.HandleAsync(new DisconnectBankCommand(Guid.NewGuid(), TenantId));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NOT_FOUND, result.ErrorCode);
    }

    // ── RefreshConnection ─────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshConnection_updates_token_hash_and_prefix()
    {
        var id = await ConnectBankAsync();
        var before = _db.Connections.First(c => c.Id == id);
        var oldHash   = before.AccessTokenHash;
        var oldPrefix = before.AccessTokenPrefix;

        var result = await _refresh.HandleAsync(
            new RefreshConnectionCommand(id, TenantId, UserId, "newtoken99999999", null));

        Assert.True(result.IsSuccess);
        var after = _db.Connections.First(c => c.Id == id);
        Assert.NotEqual(oldHash,   after.AccessTokenHash);
        Assert.NotEqual(oldPrefix, after.AccessTokenPrefix);
        Assert.Equal("newtoken", after.AccessTokenPrefix);
    }

    [Fact]
    public async Task RefreshConnection_NOT_FOUND_for_unknown()
    {
        var result = await _refresh.HandleAsync(
            new RefreshConnectionCommand(Guid.NewGuid(), TenantId, UserId, "newtoken", null));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NOT_FOUND, result.ErrorCode);
    }

    // ── ImportStatement ───────────────────────────────────────────────────────

    [Fact]
    public async Task ImportStatement_creates_statement()
    {
        var connId = await ConnectBankAsync();

        var stmtId = await ImportStatementAsync(connId);

        Assert.NotEqual(Guid.Empty, stmtId);
    }

    [Fact]
    public async Task ImportStatement_TransactionCount_equals_lines_count()
    {
        var connId = await ConnectBankAsync();
        var lines = new[]
        {
            new StatementLineInput(DateOnly.FromDateTime(DateTime.Today), 100m, "PLN", "Payment 1", null),
            new StatementLineInput(DateOnly.FromDateTime(DateTime.Today), 200m, "PLN", "Payment 2", "REF-002"),
            new StatementLineInput(DateOnly.FromDateTime(DateTime.Today), -50m, "PLN", "Fee",       null),
        };
        var stmtId = await ImportStatementAsync(connId, lines);

        var dto = (await _getStatement.HandleAsync(new GetStatementQuery(stmtId, TenantId))).Value!;
        Assert.Equal(3, dto.TransactionCount);
        Assert.Equal(3, dto.Lines.Count);
    }

    [Fact]
    public async Task ImportStatement_all_lines_status_Unmatched()
    {
        var connId = await ConnectBankAsync();
        var lines = new[]
        {
            new StatementLineInput(DateOnly.FromDateTime(DateTime.Today), 100m, "PLN", "A", null),
            new StatementLineInput(DateOnly.FromDateTime(DateTime.Today), 200m, "PLN", "B", null),
        };
        var stmtId = await ImportStatementAsync(connId, lines);

        var dto = (await _getStatement.HandleAsync(new GetStatementQuery(stmtId, TenantId))).Value!;
        Assert.All(dto.Lines, l => Assert.Equal("Unmatched", l.Status));
    }

    [Fact]
    public async Task ImportStatement_publishes_BankStatementImportedEvent()
    {
        var connId = await ConnectBankAsync();
        await ImportStatementAsync(connId);

        var count = _db.OutboxMessages.Count(m => m.EventType == typeof(BankStatementImportedEvent).FullName);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ImportStatement_updates_LastSyncAt()
    {
        var connId = await ConnectBankAsync();
        await ImportStatementAsync(connId);

        var conn = _db.Connections.First(c => c.Id == connId);
        Assert.NotNull(conn.LastSyncAt);
        Assert.Equal("Success", conn.LastSyncStatus);
    }

    // ── MatchStatementLine ────────────────────────────────────────────────────

    [Fact]
    public async Task MatchStatementLine_sets_Status_Matched()
    {
        var connId = await ConnectBankAsync();
        var stmtId = await ImportStatementAsync(connId);
        var lineId = (await _getStatement.HandleAsync(new GetStatementQuery(stmtId, TenantId))).Value!.Lines[0].Id;

        var result = await _matchLine.HandleAsync(
            new MatchStatementLineCommand(lineId, stmtId, TenantId, Guid.NewGuid(), null));

        Assert.True(result.IsSuccess);
        var line = (await _getStatement.HandleAsync(new GetStatementQuery(stmtId, TenantId))).Value!.Lines[0];
        Assert.Equal("Matched", line.Status);
    }

    [Fact]
    public async Task MatchStatementLine_stores_MatchedInvoiceId()
    {
        var connId    = await ConnectBankAsync();
        var stmtId    = await ImportStatementAsync(connId);
        var lineId    = (await _getStatement.HandleAsync(new GetStatementQuery(stmtId, TenantId))).Value!.Lines[0].Id;
        var invoiceId = Guid.NewGuid();

        await _matchLine.HandleAsync(new MatchStatementLineCommand(lineId, stmtId, TenantId, invoiceId, null));

        var line = (await _getStatement.HandleAsync(new GetStatementQuery(stmtId, TenantId))).Value!.Lines[0];
        Assert.Equal(invoiceId, line.MatchedInvoiceId);
        Assert.Null(line.MatchedPaymentId);
    }

    [Fact]
    public async Task MatchStatementLine_publishes_event()
    {
        var connId = await ConnectBankAsync();
        var stmtId = await ImportStatementAsync(connId);
        var lineId = (await _getStatement.HandleAsync(new GetStatementQuery(stmtId, TenantId))).Value!.Lines[0].Id;

        await _matchLine.HandleAsync(new MatchStatementLineCommand(lineId, stmtId, TenantId, Guid.NewGuid(), null));

        var count = _db.OutboxMessages.Count(m => m.EventType == typeof(BankStatementLineMatchedEvent).FullName);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task MatchStatementLine_NOT_FOUND_for_wrong_statementId()
    {
        var connId = await ConnectBankAsync();
        var stmtId = await ImportStatementAsync(connId);
        var lineId = (await _getStatement.HandleAsync(new GetStatementQuery(stmtId, TenantId))).Value!.Lines[0].Id;

        var result = await _matchLine.HandleAsync(
            new MatchStatementLineCommand(lineId, Guid.NewGuid(), TenantId, Guid.NewGuid(), null));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NOT_FOUND, result.ErrorCode);
    }

    [Fact]
    public async Task MatchStatementLine_VALIDATION_if_both_invoice_and_payment_set()
    {
        var connId = await ConnectBankAsync();
        var stmtId = await ImportStatementAsync(connId);
        var lineId = (await _getStatement.HandleAsync(new GetStatementQuery(stmtId, TenantId))).Value!.Lines[0].Id;

        var result = await _matchLine.HandleAsync(
            new MatchStatementLineCommand(lineId, stmtId, TenantId, Guid.NewGuid(), Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.VALIDATION, result.ErrorCode);
    }

    [Fact]
    public async Task MatchStatementLine_VALIDATION_if_neither_set()
    {
        var connId = await ConnectBankAsync();
        var stmtId = await ImportStatementAsync(connId);
        var lineId = (await _getStatement.HandleAsync(new GetStatementQuery(stmtId, TenantId))).Value!.Lines[0].Id;

        var result = await _matchLine.HandleAsync(
            new MatchStatementLineCommand(lineId, stmtId, TenantId, null, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.VALIDATION, result.ErrorCode);
    }

    // ── IgnoreStatementLine ───────────────────────────────────────────────────

    [Fact]
    public async Task IgnoreStatementLine_sets_Status_Ignored()
    {
        var connId = await ConnectBankAsync();
        var stmtId = await ImportStatementAsync(connId);
        var lineId = (await _getStatement.HandleAsync(new GetStatementQuery(stmtId, TenantId))).Value!.Lines[0].Id;

        var result = await _ignoreLine.HandleAsync(new IgnoreStatementLineCommand(lineId, stmtId, TenantId));

        Assert.True(result.IsSuccess);
        var line = (await _getStatement.HandleAsync(new GetStatementQuery(stmtId, TenantId))).Value!.Lines[0];
        Assert.Equal("Ignored", line.Status);
    }

    [Fact]
    public async Task IgnoreStatementLine_NOT_FOUND_for_wrong_tenant()
    {
        var connId = await ConnectBankAsync();
        var stmtId = await ImportStatementAsync(connId);
        var lineId = (await _getStatement.HandleAsync(new GetStatementQuery(stmtId, TenantId))).Value!.Lines[0].Id;

        var result = await _ignoreLine.HandleAsync(new IgnoreStatementLineCommand(lineId, stmtId, Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NOT_FOUND, result.ErrorCode);
    }

    // ── GetConnection ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetConnection_DTO_has_AccessTokenPrefix_no_hash()
    {
        var id = await ConnectBankAsync();

        var dto = (await _getConnection.HandleAsync(new GetConnectionQuery(id, TenantId))).Value!;
        Assert.Equal(8, dto.AccessTokenPrefix.Length);
        // Confirm DTO does not expose the hash (ConnectionDto has no AccessTokenHash field)
        Assert.Equal("plaintok", dto.AccessTokenPrefix);
    }

    [Fact]
    public async Task GetConnection_NOT_FOUND_for_wrong_tenant()
    {
        var id = await ConnectBankAsync();

        var result = await _getConnection.HandleAsync(new GetConnectionQuery(id, Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NOT_FOUND, result.ErrorCode);
    }

    // ── GetStatement ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatement_DTO_includes_Lines()
    {
        var connId = await ConnectBankAsync();
        var lines = new[]
        {
            new StatementLineInput(DateOnly.FromDateTime(DateTime.Today), 100m, "PLN", "Desc", "REF-001"),
        };
        var stmtId = await ImportStatementAsync(connId, lines);

        var dto = (await _getStatement.HandleAsync(new GetStatementQuery(stmtId, TenantId))).Value!;
        Assert.Single(dto.Lines);
        Assert.Equal("REF-001", dto.Lines[0].Reference);
    }

    [Fact]
    public async Task GetStatement_wrong_tenant_NOT_FOUND()
    {
        var connId = await ConnectBankAsync();
        var stmtId = await ImportStatementAsync(connId);

        var result = await _getStatement.HandleAsync(new GetStatementQuery(stmtId, Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NOT_FOUND, result.ErrorCode);
    }

    // ── ListConnections ───────────────────────────────────────────────────────

    [Fact]
    public async Task ListConnections_filters_by_tenant()
    {
        await ConnectBankAsync("PKO", "PL11111111111111111111111111");
        await ConnectBankAsync("ING", "PL22222222222222222222222222");
        // Different tenant
        await _connect.HandleAsync(new ConnectBankCommand(
            Guid.NewGuid(), UserId, "Other", "MBANK",
            "PL33333333333333333333333333", "othertoken12345678", null, null));

        var result = await _listConnections.HandleAsync(new ListConnectionsQuery(TenantId, 1, 20));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.TotalCount);
    }

    // ── ListStatements ────────────────────────────────────────────────────────

    [Fact]
    public async Task ListStatements_filters_by_tenant()
    {
        var connId = await ConnectBankAsync();
        await ImportStatementAsync(connId);
        await ImportStatementAsync(connId);

        // Different tenant — import won't work because connection belongs to TenantId
        // so create a separate connection for other tenant manually
        var otherTenant = Guid.NewGuid();
        var otherConnResult = await _connect.HandleAsync(new ConnectBankCommand(
            otherTenant, UserId, "Other", "ING",
            "PL99999999999999999999999999", "othertoken12345678", null, null));
        await _importStatement.HandleAsync(new ImportStatementCommand(
            otherConnResult.Value, otherTenant, UserId,
            DateOnly.FromDateTime(DateTime.Today),
            "PL99999999999999999999999999", "PLN", 0m, 0m,
            [new StatementLineInput(DateOnly.FromDateTime(DateTime.Today), 1m, "PLN", "x", null)]));

        var result = await _listStatements.HandleAsync(new ListStatementsQuery(TenantId, 1, 20, null, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.TotalCount);
    }

    [Fact]
    public async Task ListStatements_filters_by_connectionId()
    {
        var connId1 = await ConnectBankAsync("PKO", "PL11111111111111111111111111");
        var connId2 = await ConnectBankAsync("ING", "PL22222222222222222222222222");
        await ImportStatementAsync(connId1);
        await ImportStatementAsync(connId2);

        var result = await _listStatements.HandleAsync(new ListStatementsQuery(TenantId, 1, 20, connId1, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.TotalCount);
    }

    [Fact]
    public async Task ListStatements_filters_by_date_range()
    {
        var connId = await ConnectBankAsync();

        // Import three statements on different dates by manipulating the statement date
        var today     = DateOnly.FromDateTime(DateTime.Today);
        var yesterday = today.AddDays(-1);
        var lastWeek  = today.AddDays(-7);

        await _importStatement.HandleAsync(new ImportStatementCommand(
            connId, TenantId, UserId, today, "PL12345678901234567890123456", "PLN", 0m, 0m,
            [new StatementLineInput(today, 1m, "PLN", "today", null)]));
        await _importStatement.HandleAsync(new ImportStatementCommand(
            connId, TenantId, UserId, yesterday, "PL12345678901234567890123456", "PLN", 0m, 0m,
            [new StatementLineInput(yesterday, 1m, "PLN", "yesterday", null)]));
        await _importStatement.HandleAsync(new ImportStatementCommand(
            connId, TenantId, UserId, lastWeek, "PL12345678901234567890123456", "PLN", 0m, 0m,
            [new StatementLineInput(lastWeek, 1m, "PLN", "last week", null)]));

        // Filter: only yesterday and today
        var result = await _listStatements.HandleAsync(
            new ListStatementsQuery(TenantId, 1, 20, null, yesterday, today));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.TotalCount);
    }
}
