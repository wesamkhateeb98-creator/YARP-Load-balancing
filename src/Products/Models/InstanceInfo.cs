namespace Products.Models;

/// <summary>
/// Identity of the process that served the request. Returning this in the payload is what
/// makes the gateway's load-balancing decisions observable from a plain HTTP client.
/// </summary>
/// <param name="Id">Logical instance name, e.g. <c>products-5000</c>.</param>
/// <param name="Host">Machine hosting the instance.</param>
/// <param name="Port">Kestrel port the instance is listening on.</param>
/// <param name="ProcessId">OS process identifier.</param>
/// <param name="StartedAtUtc">Process start time, useful for spotting restarts during a demo.</param>
public sealed record InstanceInfo(
    string Id,
    string Host,
    int Port,
    int ProcessId,
    DateTimeOffset StartedAtUtc);
