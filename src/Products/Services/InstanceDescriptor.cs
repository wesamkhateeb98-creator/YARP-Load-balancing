using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Products.Models;

namespace Products.Services;

/// <summary>
/// Resolves the identity of the running instance. The listening port is discovered lazily
/// because Kestrel only publishes its bound addresses after the server has started.
/// </summary>
public sealed class InstanceDescriptor
{
    private readonly Lazy<int> _port;

    public InstanceDescriptor(IServer server, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(configuration);

        _port = new Lazy<int>(
            () => ResolvePort(server, configuration),
            LazyThreadSafetyMode.ExecutionAndPublication);

        Host = Environment.MachineName;
        ProcessId = Environment.ProcessId;
        StartedAtUtc = DateTimeOffset.UtcNow;
        Name = configuration["Instance:Name"] ?? string.Empty;
    }

    /// <summary>Machine hosting this process.</summary>
    public string Host { get; }

    /// <summary>OS process identifier.</summary>
    public int ProcessId { get; }

    /// <summary>UTC timestamp captured when the process came up.</summary>
    public DateTimeOffset StartedAtUtc { get; }

    /// <summary>Port Kestrel is bound to, or <c>0</c> when it cannot be determined.</summary>
    public int Port => _port.Value;

    /// <summary>
    /// Logical instance name. Falls back to <c>products-{port}</c> when the
    /// <c>Instance:Name</c> setting (or <c>INSTANCE__NAME</c> variable) is not supplied.
    /// </summary>
    public string Id => string.IsNullOrWhiteSpace(Name) ? $"products-{Port}" : Name;

    private string Name { get; }

    /// <summary>Projects the current identity into a serialisable snapshot.</summary>
    public InstanceInfo Snapshot() => new(Id, Host, Port, ProcessId, StartedAtUtc);

    private static int ResolvePort(IServer server, IConfiguration configuration)
    {
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;

        if (addresses is not null && TryReadPort(addresses, out var boundPort))
        {
            return boundPort;
        }

        // Fallback for hosts that do not expose bound addresses (e.g. in-memory test servers).
        var configured = configuration["Urls"] ?? configuration["ASPNETCORE_URLS"];
        return configured is not null && TryReadPort(configured.Split(';'), out var configuredPort)
            ? configuredPort
            : 0;
    }

    private static bool TryReadPort(IEnumerable<string> addresses, out int port)
    {
        foreach (var address in addresses)
        {
            // Wildcard bindings are not valid URI hosts; normalise before parsing.
            var normalised = address.Replace("*", "localhost", StringComparison.Ordinal)
                                    .Replace("+", "localhost", StringComparison.Ordinal);

            if (Uri.TryCreate(normalised, UriKind.Absolute, out var uri) && uri.Port > 0)
            {
                port = uri.Port;
                return true;
            }
        }

        port = 0;
        return false;
    }
}
