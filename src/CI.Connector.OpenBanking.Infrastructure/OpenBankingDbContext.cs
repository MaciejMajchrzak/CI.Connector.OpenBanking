using CI.Connector.OpenBanking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CI.Connector.OpenBanking.Infrastructure;

public sealed class OpenBankingDbContext(DbContextOptions<OpenBankingDbContext> options) : DbContext(options)
{
    public DbSet<BankConnection>          Connections    { get; init; } = null!;
    public DbSet<BankStatement>           Statements     { get; init; } = null!;
    public DbSet<BankStatementLine>       StatementLines { get; init; } = null!;
    public DbSet<OpenBankingOutboxMessage> OutboxMessages => Set<OpenBankingOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpenBankingDbContext).Assembly);

        modelBuilder.Entity<OpenBankingOutboxMessage>(b =>
        {
            b.ToTable("OpenBankingOutboxMessages");
            b.HasIndex(m => new { m.Status, m.CreatedAt });
        });

        if (Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
        {
            modelBuilder.Entity<BankConnection>()
                .Property(e => e.RowVersion)
                .HasColumnType("xid")
                .HasColumnName("xmin")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();

            modelBuilder.Entity<BankStatement>()
                .Property(e => e.RowVersion)
                .HasColumnType("xid")
                .HasColumnName("xmin")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        }
    }
}
