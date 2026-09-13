using Products.Models;
using Products.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IProductCatalog, InMemoryProductCatalog>();
builder.Services.AddSingleton<InstanceDescriptor>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

// Stamp every response with the serving instance so load balancing is visible in `curl -i`,
// in browser dev-tools and in the gateway logs without parsing the body.
app.Use(async (context, next) =>
{
    var instance = context.RequestServices.GetRequiredService<InstanceDescriptor>();
    context.Response.Headers["X-Instance-Id"] = instance.Id;
    await next(context);
});

// Optional artificial latency (Instance:LatencyMs). Slowing one instance down is the fastest
// way to see load-aware policies such as LeastRequests or PowerOfTwoChoices react.
var latency = TimeSpan.FromMilliseconds(builder.Configuration.GetValue("Instance:LatencyMs", 0));

app.MapGet("/api/products", async (IProductCatalog catalog, InstanceDescriptor instance, CancellationToken cancellationToken) =>
    {
        if (latency > TimeSpan.Zero)
        {
            await Task.Delay(latency, cancellationToken);
        }

        return Results.Ok(new ProductsResponse(instance.Snapshot(), catalog.GetAll()));
    })
    .WithName("GetProducts")
    .WithSummary("Returns the product catalogue together with the identity of the serving instance.")
    .Produces<ProductsResponse>();

// Probed by the gateway's active health checks.
app.MapGet("/health", (InstanceDescriptor instance) =>
        Results.Ok(new { status = "Healthy", instance = instance.Id, timestampUtc = DateTimeOffset.UtcNow }))
    .WithName("HealthCheck")
    .ExcludeFromDescription();

app.Run();
