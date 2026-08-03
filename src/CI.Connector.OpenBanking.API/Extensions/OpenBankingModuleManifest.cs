using System.Reflection;
using CI.Kernel;
using CI.Connector.OpenBanking.Core.Commands;
using CI.Connector.OpenBanking.Domain.Events;

namespace CI.Connector.OpenBanking.API.Extensions;

public sealed class OpenBankingModuleManifest : IModuleManifest
{
    public ModuleDescriptor Describe()
    {
        var events   = Scan<IEvent>(typeof(BankConnectionEstablishedEvent).Assembly);
        var commands = ScanCommands(typeof(ConnectBankCommand).Assembly);
        return new ModuleDescriptor("openbanking", "OpenBanking Connector", "1.0.0", events, commands, Array.Empty<QueryDescriptor>());
    }

    private static IReadOnlyList<EventDescriptor> Scan<T>(Assembly assembly) =>
        assembly.GetTypes()
            .Where(t => t.IsAssignableTo(typeof(T)) && t is { IsInterface: false, IsAbstract: false })
            .Select(t => new EventDescriptor(t.Name, t.FullName ?? t.Name, GetProperties(t)))
            .ToArray();

    private static IReadOnlyList<CommandDescriptor> ScanCommands(Assembly assembly) =>
        assembly.GetTypes()
            .Where(t => t.IsAssignableTo(typeof(ICommand)) && t is { IsInterface: false, IsAbstract: false })
            .Select(t => new CommandDescriptor(t.Name, t.FullName ?? t.Name, GetProperties(t)))
            .ToArray();

    private static IReadOnlyList<PropertyDescriptor> GetProperties(Type t) =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
         .Select(p => new PropertyDescriptor(p.Name, p.PropertyType.Name,
             !p.PropertyType.IsGenericType || p.PropertyType.GetGenericTypeDefinition() != typeof(Nullable<>)))
         .ToArray();
}
