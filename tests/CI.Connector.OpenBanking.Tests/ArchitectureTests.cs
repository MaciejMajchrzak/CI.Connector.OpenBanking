using System.Reflection;
using CI.Kernel.ArchTests;
using CI.Connector.OpenBanking.API.Controllers;
using CI.Connector.OpenBanking.Core.Handlers;
using CI.Connector.OpenBanking.Domain.Entities;
using CI.Connector.OpenBanking.Infrastructure;

namespace CI.Connector.OpenBanking.Tests;

public sealed class OpenBankingArchitectureTests : ModuleArchitectureTests
{
    protected override Assembly DomainAssembly => typeof(BankConnection).Assembly;
    protected override Assembly CoreAssembly   => typeof(ConnectBankHandler).Assembly;
    protected override Assembly InfraAssembly  => typeof(OpenBankingDbContext).Assembly;
    protected override Assembly ApiAssembly    => typeof(ConnectionsController).Assembly;

    protected override bool ExcludeFromBaseEntityCheck(Type t) =>
        t == typeof(BankStatementLine) || t == typeof(OpenBankingOutboxMessage);
}
