using System.Runtime.CompilerServices;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;

namespace YarpGateway.LoadBalancing;

/// <summary>
/// Round robin weighted by a per-destination <c>Weight</c> metadata value, giving larger
/// instances a proportionally larger share of traffic. YARP ships no weighted policy, so this
/// is the canonical example of extending the proxy through <see cref="ILoadBalancingPolicy"/>.
/// </summary>
/// <remarks>
/// Selection is a stride over the cumulative weight range, which is O(n) per request and
/// allocation free. It distributes traffic exactly in proportion to weight but emits it in
/// bursts (A A A B C rather than A B A C A); see docs/algorithms/custom.md.
/// </remarks>
public sealed class WeightedRoundRobinLoadBalancingPolicy : ILoadBalancingPolicy
{
    /// <summary>Name referenced by <c>ReverseProxy:Clusters:*:LoadBalancingPolicy</c>.</summary>
    public const string PolicyName = "WeightedRoundRobin";

    /// <summary>Destination metadata key holding the relative weight.</summary>
    public const string WeightMetadataKey = "Weight";

    private const int DefaultWeight = 1;

    // Keyed on ClusterState so counters are collected when a cluster is removed from config.
    private readonly ConditionalWeakTable<ClusterState, RequestCounter> _counters = new();

    /// <inheritdoc />
    public string Name => PolicyName;

    /// <inheritdoc />
    public DestinationState? PickDestination(
        HttpContext context,
        ClusterState cluster,
        IReadOnlyList<DestinationState> availableDestinations)
    {
        if (availableDestinations.Count == 0)
        {
            return null;
        }

        var totalWeight = 0;
        foreach (var destination in availableDestinations)
        {
            totalWeight += GetWeight(destination);
        }

        if (totalWeight <= 0)
        {
            return availableDestinations[0];
        }

        var counter = _counters.GetOrCreateValue(cluster);
        var offset = (int)(counter.Next() % (uint)totalWeight);

        foreach (var destination in availableDestinations)
        {
            offset -= GetWeight(destination);
            if (offset < 0)
            {
                return destination;
            }
        }

        // Unreachable while totalWeight is the sum of the weights above; kept as a safe fallback.
        return availableDestinations[^1];
    }

    private static int GetWeight(DestinationState destination)
    {
        var metadata = destination.Model.Config.Metadata;

        return metadata is not null
               && metadata.TryGetValue(WeightMetadataKey, out var raw)
               && int.TryParse(raw, out var weight)
               && weight > 0
            ? weight
            : DefaultWeight;
    }

    private sealed class RequestCounter
    {
        private long _value = -1;

        public uint Next() => (uint)(Interlocked.Increment(ref _value) & 0x7FFFFFFF);
    }
}
