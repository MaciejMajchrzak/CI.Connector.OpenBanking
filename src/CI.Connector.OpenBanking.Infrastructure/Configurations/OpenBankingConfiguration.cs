using CI.Connector.OpenBanking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CI.Connector.OpenBanking.Infrastructure.Configurations;

public sealed class BankConnectionConfiguration : IEntityTypeConfiguration<BankConnection>
{
    public void Configure(EntityTypeBuilder<BankConnection> b)
    {
        b.ToTable("BankConnections");
        b.HasKey(e => e.Id);
        b.Property(e => e.Name).HasMaxLength(200).IsRequired();
        b.Property(e => e.BankCode).HasMaxLength(50).IsRequired();
        b.Property(e => e.AccountIban).HasMaxLength(50).IsRequired();
        b.Property(e => e.AccessTokenHash).HasMaxLength(64).IsRequired();
        b.Property(e => e.AccessTokenPrefix).HasMaxLength(20).IsRequired();
        b.Property(e => e.RefreshTokenHash).HasMaxLength(64);
        b.Property(e => e.LastSyncStatus).HasMaxLength(20);

        b.HasIndex(e => e.TenantId);
        b.HasIndex(e => new { e.TenantId, e.BankCode, e.IsActive });

        b.HasMany(e => e.Statements)
         .WithOne(s => s.Connection)
         .HasForeignKey(s => s.ConnectionId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BankStatementConfiguration : IEntityTypeConfiguration<BankStatement>
{
    public void Configure(EntityTypeBuilder<BankStatement> b)
    {
        b.ToTable("BankStatements");
        b.HasKey(e => e.Id);
        b.Property(e => e.AccountIban).HasMaxLength(50).IsRequired();
        b.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        b.Property(e => e.OpeningBalance).HasColumnType("decimal(18,4)");
        b.Property(e => e.ClosingBalance).HasColumnType("decimal(18,4)");

        b.HasIndex(e => e.TenantId);
        b.HasIndex(e => new { e.TenantId, e.ConnectionId });
        b.HasIndex(e => new { e.TenantId, e.StatementDate });

        b.HasMany(e => e.Lines)
         .WithOne()
         .HasForeignKey(l => l.StatementId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BankStatementLineConfiguration : IEntityTypeConfiguration<BankStatementLine>
{
    public void Configure(EntityTypeBuilder<BankStatementLine> b)
    {
        b.ToTable("BankStatementLines");
        b.HasKey(e => e.Id);
        b.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        b.Property(e => e.Description).HasMaxLength(500).IsRequired();
        b.Property(e => e.Reference).HasMaxLength(200);
        b.Property(e => e.Status).HasMaxLength(20).IsRequired();
        b.Property(e => e.Amount).HasColumnType("decimal(18,4)");

        b.HasIndex(e => e.StatementId);
        b.HasIndex(e => e.TenantId);
    }
}
