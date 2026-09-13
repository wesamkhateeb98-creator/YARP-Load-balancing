using Yarp.ReverseProxy;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;
using YarpGateway.LoadBalancing;

var builder = WebApplication.CreateBuilder(args);

// Routes, clusters, destinations and the load-balancing policy are all declared in
// appsettings.json. LoadFromConfig watches that section, so changing the policy and saving
// re-applies it to live traffic without restarting the gateway.
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Custom policies are plain DI registrations; YARP resolves them by their Name property.
builder.Services.AddSingleton<ILoadBalancingPolicy, WeightedRoundRobinLoadBalancingPolicy>();

builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Live snapshot of the proxy state: policy in force, destination health and in-flight request
// counts. Handy when demonstrating load-aware policies such as LeastRequests.
app.MapGet("/gateway/clusters", (IProxyStateLookup lookup) =>
{
    var clusters = lookup.GetClusters().Select(cluster => new
    {
        clusterId = cluster.ClusterId,
        loadBalancingPolicy = string.IsNullOrWhiteSpace(cluster.Model.Config.LoadBalancingPolicy)
            ? "PowerOfTwoChoices (YARP default)"
            : cluster.Model.Config.LoadBalancingPolicy,
        destinations = cluster.DestinationsState.AllDestinations.Select(destination => new
        {
            destinationId = destination.DestinationId,
            address = destination.Model.Config.Address,
            weight = destination.Model.Config.Metadata?.GetValueOrDefault(
                WeightedRoundRobinLoadBalancingPolicy.WeightMetadataKey),
            activeHealth = destination.Health.Active.ToString(),
            passiveHealth = destination.Health.Passive.ToString(),
            concurrentRequests = destination.ConcurrentRequestCount
        })
    });

    return Results.Ok(clusters);
});

app.MapGet("/gateway/health", () => Results.Ok(new { status = "Healthy", timestampUtc = DateTimeOffset.UtcNow }));

app.MapReverseProxy();

app.Run();
