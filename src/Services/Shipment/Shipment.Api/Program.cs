var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// M0: liveness probe — sẽ được ALB/ECS health check dùng ở M4.
app.MapGet("/health", () => Results.Ok(new { service = "shipment", status = "healthy" }));

app.Run();
