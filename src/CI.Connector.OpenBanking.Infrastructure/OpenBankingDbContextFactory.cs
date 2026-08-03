using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CI.Connector.OpenBanking.Infrastructure;

public sealed class OpenBankingDbContextFactory : IDesignTimeDbContextFactory<OpenBankingDbContext>
{
    public OpenBankingDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<OpenBankingDbContext>()
            .UseNpgsql("Host=localhost;Database=ci_openbanking;Username=ci;Password=ci")
            .Options;
        return new OpenBankingDbContext(opts);
    }
}
